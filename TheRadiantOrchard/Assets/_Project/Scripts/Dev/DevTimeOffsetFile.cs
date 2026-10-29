using System;
using System.Globalization;
using System.IO;
using RadiantOrchard.Core;
using UnityEngine;

namespace RadiantOrchard.Dev
{
    /// <summary>
    /// Keeps the dev time offset in dev_time_offset.txt next to the save, so a time skip survives a restart during
    /// multi-day pacing tests. Kept out of GameState on purpose. This assembly is only compiled into the Editor and
    /// development builds, so release builds always run on real time.
    /// </summary>
    public sealed class DevTimeOffsetFile : IDevTimeOffsetStore
    {
        public const string FileName = "dev_time_offset.txt";

        private readonly string _path;

        public DevTimeOffsetFile(string directory)
        {
            _path = Path.Combine(directory, FileName);
        }

        public TimeSpan Load()
        {
            try
            {
                if (!File.Exists(_path)) return TimeSpan.Zero;
                long ms = long.Parse(File.ReadAllText(_path).Trim(), CultureInfo.InvariantCulture);
                return TimeSpan.FromMilliseconds(ms);
            }
            catch (Exception e)
            {
                Log.Warn($"DevTimeOffsetFile: could not read {_path} ({e.Message}); using real time.");
                return TimeSpan.Zero;
            }
        }

        public void Save(TimeSpan offset)
        {
            try
            {
                if (offset == TimeSpan.Zero)
                {
                    if (File.Exists(_path)) File.Delete(_path);
                    return;
                }
                File.WriteAllText(_path, ((long)offset.TotalMilliseconds).ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                Log.Warn($"DevTimeOffsetFile: could not write {_path} ({e.Message}).");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() => DevHooks.TimeOffsetStore = new DevTimeOffsetFile(Application.persistentDataPath);
    }
}
