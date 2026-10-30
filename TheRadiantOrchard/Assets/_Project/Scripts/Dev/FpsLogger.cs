using RadiantOrchard.Core;
using UnityEngine;

namespace RadiantOrchard.Dev
{
    /// <summary>
    /// Development builds on a device only: every few seconds logs the average and worst frame rate to the device
    /// log (adb logcat, tag Unity), so frame rate can be read over USB before the dev panel's FPS counter exists
    /// (ENGINEERING_GUIDE 9.2). Silent in the Editor and absent from release builds.
    /// </summary>
    public sealed class FpsLogger : MonoBehaviour
    {
        private const float ReportSeconds = 5f;

        private int _frames;
        private float _elapsed;
        private float _worstFrame;

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

            Log.Info($"[FPS] avg {_frames / _elapsed:0.0}, worst frame {_worstFrame * 1000f:0} ms over {_elapsed:0.0} s");
            _frames = 0;
            _elapsed = 0f;
            _worstFrame = 0f;
        }
    }
}
