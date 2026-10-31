using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RadiantOrchard.App;
using RadiantOrchard.Dev;
using RadiantOrchard.Gameplay;
using RadiantOrchard.Input;
using RadiantOrchard.Presentation;
using RadiantOrchard.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// The zone unlock sequence in the running game (CLAUDE.md 5.7): a stage reached in play locks input, glides the
    /// camera to the zone, colours it, shows the banner, glides back and hands control back, once per stage.
    /// Saves go to a temporary folder.
    /// </summary>
    public class ZoneUnlockPlayTests
    {
        private string _saveDir;
        private DevGame _game;
        private ZoneUnlockSequence _sequence;
        private CameraController _camera;
        private GestureRecognizer _gestures;
        private ZoneBannerView _banner;
        private FarmView _farm;
        private List<ZoneWaveStarted> _waves;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "RadiantOrchardZoneUnlockTest", Guid.NewGuid().ToString("N"));
            yield return TestGame.Boot(_saveDir);
            _game = DevGame.Current;
            Assert.IsNotNull(_game?.Vibrancy, "GameInstaller did not finish wiring.");
            // Test code may search the scene; the no-Find rule is for runtime code.
            _sequence = Object.FindFirstObjectByType<ZoneUnlockSequence>();
            _camera = Object.FindFirstObjectByType<CameraController>();
            _gestures = Object.FindFirstObjectByType<GestureRecognizer>();
            _banner = Object.FindFirstObjectByType<ZoneBannerView>();
            _farm = Object.FindFirstObjectByType<FarmView>();
            Assert.IsNotNull(_sequence, "No ZoneUnlockSequence in Game (Radiant Orchard > Setup > Build Zone Unlock).");
            _waves = new List<ZoneWaveStarted>();
            _game.Events.Subscribe<ZoneWaveStarted>(_waves.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _game?.Events.Unsubscribe<ZoneWaveStarted>(_waves.Add);
            TestGame.Shutdown(_saveDir);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            for (float t = 0f; !condition() && t < timeout; t += Time.deltaTime) yield return null;
        }

        [UnityTest]
        public IEnumerator AStageReachedPlaysTheSequenceOnceAndReturnsControl()
        {
            Vector2 startPosition = _camera.Position;
            float startSize = _camera.Size;
            var menu = Object.FindFirstObjectByType<PlantMenuView>();
            menu.Show(0);

            DevActions.SetStage(_game, 1);
            yield return null;
            yield return null;
            Assert.IsTrue(_sequence.IsPlaying);
            Assert.IsTrue(_gestures.Locked, "No gestures during the sequence.");
            Assert.IsTrue(_camera.Locked, "No pinch or scroll during the sequence.");
            Assert.IsFalse(menu.IsOpen, "The plant menu closes.");
            Assert.AreEqual(0f, _farm.ZoneColours[0].Saturation, "Zone 1 waits grey for the camera.");

            yield return WaitUntil(() => _waves.Count > 0, 5f);
            Assert.AreEqual(1, _waves.Count);
            Assert.AreEqual(1, _waves[0].Stage, "The sting hook fires with the wave.");
            Assert.IsTrue(_farm.ZoneColours[0].IsSweeping);
            Assert.IsTrue(Vector2.Distance(startPosition, _camera.Position) > 0.01f || Mathf.Abs(startSize - _camera.Size) > 0.01f,
                "The camera framed the zone.");

            yield return WaitUntil(() => _banner.IsShowing, 5f);
            Assert.AreEqual("Stage 1!", _banner.Title);
            Assert.AreEqual(_game.Config.Strings.Get(ZoneBannerView.ZoneKey), _banner.Body);

            yield return WaitUntil(() => !_sequence.IsPlaying, 10f);
            Assert.IsFalse(_sequence.IsPlaying);
            Assert.AreEqual(1, _sequence.Played, "Played once.");
            Assert.IsFalse(_gestures.Locked, "Control is back.");
            Assert.IsFalse(_camera.Locked);
            Assert.Less(Vector2.Distance(startPosition, _camera.Position), 0.01f, "The camera is back where it was.");
            Assert.AreEqual(startSize, _camera.Size, 0.01f);
            Assert.AreEqual(1f, _farm.ZoneColours[0].Saturation, "Zone 1 in colour.");
            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(_banner.IsShowing, "The banner has faded out.");
        }

        [UnityTest]
        public IEnumerator TwoStagesAtOncePlayOneAfterTheOther()
        {
            DevActions.SetStage(_game, 2);
            yield return WaitUntil(() => _waves.Count == 1 && _banner.IsShowing, 8f);
            Assert.AreEqual("Stage 1!", _banner.Title);
            Assert.AreEqual(0f, _farm.ZoneColours[1].Saturation, "Zone 2 waits its turn.");

            yield return WaitUntil(() => _banner.IsShowing && _banner.Title == "Stage 2!", 10f);
            Assert.AreEqual("New fruit: Lemon, Grapes\n2 new plots", _banner.Body);
            Assert.IsTrue(_sequence.IsPlaying);

            yield return WaitUntil(() => !_sequence.IsPlaying, 10f);
            Assert.AreEqual(2, _sequence.Played);
            CollectionAssert.AreEqual(new[] { 1, 2 }, _waves.ConvertAll(w => w.Stage));
            Assert.AreEqual(1f, _farm.ZoneColours[0].Saturation);
            Assert.AreEqual(1f, _farm.ZoneColours[1].Saturation);
            Assert.AreEqual(0f, _farm.ZoneColours[2].Saturation);
            Assert.IsFalse(_gestures.Locked);
        }
    }
}
