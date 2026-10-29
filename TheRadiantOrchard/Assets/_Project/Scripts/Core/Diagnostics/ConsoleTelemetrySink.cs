namespace RadiantOrchard.Core
{
    /// <summary>Writes telemetry events to the Unity console. Only used in the Editor and development builds.</summary>
    public sealed class ConsoleTelemetrySink : ITelemetrySink
    {
        public void Log(string eventName, string details) =>
            UnityEngine.Debug.Log(string.IsNullOrEmpty(details) ? $"[Telemetry] {eventName}" : $"[Telemetry] {eventName}: {details}");
    }
}
