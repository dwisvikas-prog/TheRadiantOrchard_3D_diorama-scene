namespace RadiantOrchard.Core
{
    /// <summary>Receives telemetry events. Phase 1: console only; Phase 2 can add an analytics sink.</summary>
    public interface ITelemetrySink
    {
        void Log(string eventName, string details);
    }
}
