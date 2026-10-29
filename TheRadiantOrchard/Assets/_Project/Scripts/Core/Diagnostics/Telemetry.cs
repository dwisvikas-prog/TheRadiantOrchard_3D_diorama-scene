using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// Key-moment hook (session start/end, visitor healed, quiz answered, stage reached, event started).
    /// Phase 1 collects no data: with no sink set (release builds) every call does nothing; the Bootstrapper sets a
    /// console sink in the Editor and development builds only (CLAUDE.md section 9).
    /// </summary>
    public static class Telemetry
    {
        public static ITelemetrySink Sink { get; set; }

        public static void Log(string eventName, string details = null) => Sink?.Log(eventName, details);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Sink = null;
    }
}
