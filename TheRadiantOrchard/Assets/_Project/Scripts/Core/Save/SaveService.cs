using System;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// Loads and saves the one GameState. Systems change the state and call MarkDirty(); Tick() writes at most once per
    /// interval; SaveNow() is for OnApplicationPause(true) and OnApplicationQuit (wired up by the Bootstrapper).
    /// </summary>
    public sealed class SaveService
    {
        private readonly ISaveStore _store;
        private readonly Func<long> _utcNowMs;
        private readonly float _minIntervalSeconds;

        private bool _dirty;
        private float _lastWriteRealtime = float.NegativeInfinity;

        /// <param name="store">Where the JSON is kept.</param>
        /// <param name="utcNowMs">Current time in Unix ms UTC (TimeService.Now, so the dev time offset applies).</param>
        /// <param name="minIntervalSeconds">Minimum real seconds between autosaves (BalanceConfig).</param>
        public SaveService(ISaveStore store, Func<long> utcNowMs, float minIntervalSeconds)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _utcNowMs = utcNowMs ?? throw new ArgumentNullException(nameof(utcNowMs));
            _minIntervalSeconds = Mathf.Max(0f, minIntervalSeconds);
        }

        public GameState State { get; private set; }
        public SaveLoadSource LastLoadSource { get; private set; }
        public bool IsDirty => _dirty;

        /// <summary>
        /// True when save.json or save.bak was written by a newer build. Nothing is written for the rest of the session,
        /// so this older build can never replace that progress (a write would push it to save.bak, the next one erase it).
        /// </summary>
        public bool WritesBlocked { get; private set; }

        /// <summary>Loads save.json, else the backup, else starts a new game. Never throws for a bad file.</summary>
        public GameState Load(BalanceConfig balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));

            WritesBlocked = false;
            GameState state;
            if (TryParse(SafeRead(_store.Read), "save", out state))
            {
                LastLoadSource = SaveLoadSource.Main;
                _dirty = false;
            }
            else if (TryParse(SafeRead(_store.ReadBackup), "backup", out state))
            {
                LastLoadSource = SaveLoadSource.Backup;
                // Write the recovered state back promptly so the main file is good again.
                _dirty = true;
                Log.Warn("SaveService: the main save could not be read; progress was restored from the backup.");
            }
            else
            {
                state = GameState.CreateNew(balance);
                LastLoadSource = SaveLoadSource.New;
                _dirty = true;
            }

            if (WritesBlocked)
            {
                Log.Warn("SaveService: a save from a newer version of the game was found; this session will not save, so it is kept.");
            }

            state.EnsureIntegrity(balance);
            State = state;
            return state;
        }

        public void MarkDirty() => _dirty = true;

        /// <summary>Saves if there are unsaved changes and the minimum interval has passed. Returns true if it wrote.</summary>
        public bool Tick(float realtimeSeconds)
        {
            if (!_dirty || State == null) return false;
            if (realtimeSeconds - _lastWriteRealtime < _minIntervalSeconds) return false;

            _lastWriteRealtime = realtimeSeconds;
            return SaveNow();
        }

        /// <summary>Writes the state now. On failure the changes stay marked unsaved so the next attempt retries.</summary>
        public bool SaveNow()
        {
            if (State == null || WritesBlocked) return false;

            try
            {
                State.LastSaveUtc = _utcNowMs();
                _store.Write(JsonUtility.ToJson(State));
                _dirty = false;
                return true;
            }
            catch (Exception e)
            {
                _dirty = true;
                Log.Error($"SaveService: saving failed: {e.Message}");
                return false;
            }
        }

        private static string SafeRead(Func<string> read)
        {
            try
            {
                return read();
            }
            catch (Exception e)
            {
                Log.Warn($"SaveService: could not read a save file: {e.Message}");
                return null;
            }
        }

        private bool TryParse(string json, string label, out GameState state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                state = JsonUtility.FromJson<GameState>(json);
            }
            catch (Exception e)
            {
                Log.Warn($"SaveService: the {label} file is not valid JSON ({e.Message}).");
                return false;
            }

            if (state != null && state.Version > GameState.CurrentVersion) WritesBlocked = true;

            if (!SaveMigrator.TryMigrate(state, out string error))
            {
                Log.Warn($"SaveService: the {label} file was not used: {error}.");
                state = null;
                return false;
            }
            return true;
        }
    }
}
