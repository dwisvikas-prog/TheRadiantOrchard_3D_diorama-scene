using RadiantOrchard.Core;
using Unity.Profiling;
using UnityEngine;

namespace RadiantOrchard.Dev
{
    /// <summary>
    /// Development builds on a device only: every few seconds logs the average and worst frame rate and the draw-call
    /// batches of the last frame to the device log (adb logcat, tag Unity), so they can be read over USB
    /// (ENGINEERING_GUIDE 7, 9.2). Silent in the Editor and absent from release builds.
    /// </summary>
    public sealed class FpsLogger : MonoBehaviour
    {
        private const float ReportSeconds = 5f;

        private int _frames;
        private float _elapsed;
        private float _worstFrame;
        private ProfilerRecorder _batches;

        private void OnEnable() => _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");

        private void OnDisable() => _batches.Dispose();

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject(nameof(FpsLogger));
            DontDestroyOnLoad(go);
            go.AddComponent<FpsLogger>();
        }
#endif

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _frames++;
            _elapsed += dt;
            if (dt > _worstFrame) _worstFrame = dt;
            if (_elapsed < ReportSeconds) return;

            string batches = _batches.Valid ? _batches.LastValue.ToString() : "n/a";
            Log.Info($"[FPS] avg {_frames / _elapsed:0.0}, worst frame {_worstFrame * 1000f:0} ms over {_elapsed:0.0} s, batches {batches}");
            _frames = 0;
            _elapsed = 0f;
            _worstFrame = 0f;
        }
    }
}
