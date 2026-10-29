using System;
using RadiantOrchard.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadiantOrchard.App
{
    /// <summary>
    /// The one object in the Boot scene. Builds the services, loads the save, records the time away, then loads Game.
    /// Lives for the whole session: drives autosave and saves on pause and quit.
    /// </summary>
    public sealed class Bootstrapper : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private string _gameSceneName = "Game";

        private AppServices _services;
        private long _pausedAtUtc;

        /// <summary>Tests point the save at a temporary folder so they never touch the real save.</summary>
        public static string SaveDirectoryOverride { get; set; }

        private void Awake()
        {
            if (AppServices.Current != null)
            {
                Destroy(gameObject);
                return;
            }

            if (_config == null || _config.Balance == null || _config.Strings == null)
            {
                Log.Error("Bootstrapper: assign a GameConfig with Balance and Strings.", this);
                enabled = false;
                return;
            }

            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = _config.Balance.TargetFrameRate;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Telemetry.Sink = new ConsoleTelemetrySink();
#endif

            var time = new TimeService(new SystemClock());
            ApplyDevTimeOffset(time);

            string saveDirectory = string.IsNullOrEmpty(SaveDirectoryOverride) ? Application.persistentDataPath : SaveDirectoryOverride;
            var save = new SaveService(new LocalFileStore(saveDirectory), () => time.NowUtcMs, _config.Balance.AutosaveMinIntervalSeconds);
            GameState state = save.Load(_config.Balance);
            TimeSpan away = time.ElapsedSince(state.LastSaveUtc);

            _services = new AppServices(_config, time, new GameClock(_config.Balance.GameTickIntervalSeconds), save, new EventBus(), away);
            AppServices.Current = _services;

            Log.Info($"Bootstrapper: {save.LastLoadSource} save from {saveDirectory}; away {away}.");
            Telemetry.Log("session_start", save.LastLoadSource.ToString());
            SceneManager.LoadScene(_gameSceneName);
        }

        private void Update()
        {
            if (_services == null) return;
            _services.Clock.Advance(Time.unscaledDeltaTime);
            _services.Save.Tick(Time.realtimeSinceStartup);
        }

        private void OnApplicationPause(bool paused)
        {
            if (_services == null) return;

            if (paused)
            {
                _pausedAtUtc = _services.Time.NowUtcMs;
                _services.Save.SaveNow();
                Telemetry.Log("session_end", "pause");
            }
            // Android also sends a "resume" at startup; only count time away after a real pause.
            else if (_pausedAtUtc > 0)
            {
                TimeSpan away = _services.Time.ElapsedSince(_pausedAtUtc);
                _pausedAtUtc = 0;
                Telemetry.Log("session_start", "resume");
                _services.Events.Publish(new TimeAwayApplied(away));
            }
        }

        private void OnApplicationQuit()
        {
            if (_services == null) return;
            _services.Save.SaveNow();
            Telemetry.Log("session_end", "quit");
        }

        private void OnDestroy()
        {
            if (_services != null && AppServices.Current == _services)
                AppServices.Current = null;
        }

        private static void ApplyDevTimeOffset(TimeService time)
        {
            IDevTimeOffsetStore store = DevHooks.TimeOffsetStore;
            if (store == null) return;

            time.SetDevOffset(store.Load());
            time.DevOffsetChanged += store.Save;
            if (time.DevOffset != TimeSpan.Zero)
                Log.Warn($"Bootstrapper: dev time offset {time.DevOffset} is active (kept from an earlier session).");
        }
    }
}
