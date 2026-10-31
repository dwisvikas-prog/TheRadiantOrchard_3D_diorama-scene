using System.Collections;
using System.Collections.Generic;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using RadiantOrchard.Input;
using RadiantOrchard.Presentation;
using RadiantOrchard.UI;
using UnityEngine;

namespace RadiantOrchard.App
{
    /// <summary>
    /// The zone unlock "wow" (CLAUDE.md 5.7, FX-11): for every stage reached in play the camera glides to the zone,
    /// colour washes over it with petals (and the sting hook, ZoneWaveStarted), a banner names what opened, and the
    /// camera glides back to where the player was. Touch input is locked throughout and open panels close (a waiting quiz
    /// stays waiting); the game itself keeps running.
    /// Stages reached together play one after another. After the final stage the Wisdom Tree bloom follows (5.9): the
    /// camera frames the tree, the bloom bursts open with petals and the verse card shows until tapped (or a time limit).
    /// Lives in App because it joins camera, input, zones and UI. Feel numbers are on the component.
    /// </summary>
    public sealed class ZoneUnlockSequence : MonoBehaviour
    {
        [SerializeField] private ZoneWaveParticles _particles;
        [Min(0f)] [SerializeField] private float _glideSeconds = 1f;
        [Min(0f)] [SerializeField] private float _returnSeconds = 0.8f;
        [Min(0f)] [SerializeField] private float _bannerSeconds = 2.5f;
        [Tooltip("Pause between the end of the colour wave and the banner.")]
        [Min(0f)] [SerializeField] private float _afterWaveSeconds = 0.2f;
        [Tooltip("Extra view around the zone when the camera frames it (0.15 = 15%).")]
        [Min(0f)] [SerializeField] private float _zoomMargin = 0.15f;
        [Tooltip("The verse card closes by itself after this long if nobody taps it.")]
        [Min(1f)] [SerializeField] private float _verseMaxSeconds = 12f;

        private readonly Queue<int> _queue = new Queue<int>();
        private GameConfig _config;
        private EventBus _events;
        private CameraController _camera;
        private GestureRecognizer _gestures;
        private ZonesPresenter _zones;
        private ZoneBannerView _banner;
        private PlantMenuView _plantMenu;
        private QuizPanelView _quizPanel;
        private WisdomTreePresenter _tree;
        private VersePanelView _verse;
        private Coroutine _running;
        private bool _subscribed;

        public bool IsPlaying => _running != null;
        /// <summary>Zone sequences played to the end since the Game scene started.</summary>
        public int Played { get; private set; }
        /// <summary>Bloom celebrations played to the end since the Game scene started.</summary>
        public int BloomsPlayed { get; private set; }
        /// <summary>Total length of one zone's sequence before the camera returns, for tests.</summary>
        public float StageSeconds => _glideSeconds + (_zones != null ? _zones.SweepSeconds : 0f) + _afterWaveSeconds + _bannerSeconds;
        public float ReturnSeconds => _returnSeconds;

        public void Init(GameConfig config, EventBus events, CameraController cameraController, GestureRecognizer gestures,
            ZonesPresenter zones, ZoneBannerView banner, PlantMenuView plantMenu, QuizPanelView quizPanel, WisdomTreePresenter tree,
            VersePanelView verse)
        {
            _config = config;
            _events = events;
            _camera = cameraController;
            _gestures = gestures;
            _zones = zones;
            _banner = banner;
            _plantMenu = plantMenu;
            _quizPanel = quizPanel;
            _tree = tree;
            _verse = verse;
            Subscribe();
        }

        private void OnStageReached(StageReached e)
        {
            _queue.Enqueue(e.Stage);
            if (_running == null && isActiveAndEnabled) _running = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // Let every listener of this StageReached (zones, vibrancy meter) see it before the sequence starts.
            yield return null;
            SetLocked(true);
            Vector2 startPosition = _camera.Position;
            float startSize = _camera.Size;

            while (_queue.Count > 0)
            {
                int stage = _queue.Dequeue();
                int zone = stage - 1;
                // A dev stage-down while waiting greys the zone again; nothing to show.
                if (zone >= 0 && zone < _zones.Count && _zones.IsPending(zone)) yield return Zone(stage, zone);
                // Stages reached together are all announced before the first plays, so the bloom is already pending;
                // it waits for the final stage's zone.
                if (stage >= _config.Balance.StageThresholds.Count && _tree.BloomPending) yield return Bloom();
            }

            yield return Glide(startPosition, startSize, _returnSeconds);
            SetLocked(false);
            _running = null;
        }

        private IEnumerator Zone(int stage, int zone)
        {
            Rect area = _zones.AreaOf(zone);
            yield return Glide(area.center, _camera.SizeToFit(area, _zoomMargin), _glideSeconds);

            _zones.Sweep(zone);
            if (_particles != null) _particles.Play(area, _zones.SweepSeconds);
            _events.Publish(new ZoneWaveStarted(stage));
            yield return Wait(_zones.SweepSeconds + _afterWaveSeconds);

            _banner.Show(StageUnlocks.For(_config, stage));
            yield return Wait(_bannerSeconds);
            _banner.Hide();
            Played++;
        }

        private IEnumerator Bloom()
        {
            Rect area = _tree.View.BloomArea;
            yield return Glide(area.center, _camera.SizeToFit(area, _zoomMargin), _glideSeconds);

            _tree.RevealBloom();
            if (_particles != null) _particles.Play(area, _tree.View.RevealSeconds);
            yield return Wait(_tree.View.RevealSeconds + _afterWaveSeconds);

            _verse.Show();
            for (float t = 0f; t < _verseMaxSeconds && _verse.IsOpen; t += Time.deltaTime) yield return null;
            if (_verse.IsOpen) _verse.Hide();
            BloomsPlayed++;
        }

        // Smooth start and stop between the current view and the target.
        private IEnumerator Glide(Vector2 position, float size, float seconds)
        {
            Vector2 fromPosition = _camera.Position;
            float fromSize = _camera.Size;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                _camera.SetView(Vector2.Lerp(fromPosition, position, k), Mathf.Lerp(fromSize, size, k));
                yield return null;
            }
            _camera.SetView(position, size);
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        private void SetLocked(bool locked)
        {
            _gestures.Locked = locked;
            _camera.Locked = locked;
            if (!locked) return;
            if (_plantMenu.IsOpen) _plantMenu.Hide();
            if (_quizPanel.IsOpen) _quizPanel.Hide();
        }

        private void Subscribe()
        {
            if (_subscribed || _events == null || !isActiveAndEnabled) return;
            _events.Subscribe<StageReached>(OnStageReached);
            _subscribed = true;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (_running != null)
            {
                // Unity stops the coroutine with the component; never leave the player's input locked.
                _running = null;
                SetLocked(false);
            }
            _queue.Clear();
            if (!_subscribed) return;
            _events.Unsubscribe<StageReached>(OnStageReached);
            _subscribed = false;
        }
    }
}
