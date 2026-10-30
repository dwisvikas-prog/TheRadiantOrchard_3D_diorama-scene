using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Development APK for testing on a phone or emulator (W1·22): the scenes in the build list, Development build
    /// (logging, Dev assembly, Profiler connection), debug signing. Uses build options only, so no Player Settings
    /// change; the release AAB with the upload key is a separate step (W4). Output in Builds/Android (gitignored).
    /// </summary>
    public static class AndroidDevBuild
    {
        public const string OutputPath = "Builds/Android/RadiantOrchard-dev.apk";

        [MenuItem("Radiant Orchard/Build/Android Development (APK)")]
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("AndroidDevBuild: switch the active platform to Android first (File > Build Profiles).");
                return;
            }

            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled) scenes.Add(scene.path);

            // An APK installs straight onto a device; the AAB setting is restored afterwards.
            bool appBundle = EditorUserBuildSettings.buildAppBundle;
            EditorUserBuildSettings.buildAppBundle = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(),
                    locationPathName = OutputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.Development | BuildOptions.ConnectWithProfiler,
                });

                BuildSummary s = report.summary;
                string line = $"AndroidDevBuild: {s.result} in {s.totalTime.TotalSeconds:0} s, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalSize / (1024f * 1024f):0.0} MB -> {Path.GetFullPath(OutputPath)}";
                if (s.result == BuildResult.Succeeded) Debug.Log(line);
                else Debug.LogError(line);
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = appBundle;
            }
        }
    }
}
