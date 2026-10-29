using System.Diagnostics;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// Logging wrapper. Info and Warn calls (including building their messages) are compiled out of release builds;
    /// Error always logs.
    /// </summary>
    public static class Log
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string message, Object context = null) => UnityEngine.Debug.Log(message, context);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Warn(string message, Object context = null) => UnityEngine.Debug.LogWarning(message, context);

        public static void Error(string message, Object context = null) => UnityEngine.Debug.LogError(message, context);
    }
}
