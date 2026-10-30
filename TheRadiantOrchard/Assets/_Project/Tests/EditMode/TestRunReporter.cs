using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>
    /// Runs every test suite from one menu item and writes the results to Logs/TestResults (gitignored), so a run
    /// can be checked after the fact, including PlayMode runs that reload the scripts on entering Play mode
    /// (ENGINEERING_GUIDE 9.1: run all tests before every review build). Re-registers itself after each reload.
    /// </summary>
    [InitializeOnLoad]
    public static class TestRunReporter
    {
        private const string ChainKey = "RadiantOrchard.TestRunReporter.RunPlayModeNext";
        private static readonly string ResultsFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "TestResults");

        static TestRunReporter()
        {
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Callbacks());
        }

        [MenuItem("Radiant Orchard/Dev/Run All Tests (EditMode, then PlayMode)")]
        public static void RunAll()
        {
            SessionState.SetBool(ChainKey, true);
            Run(TestMode.EditMode);
        }

        [MenuItem("Radiant Orchard/Dev/Run PlayMode Tests")]
        public static void RunPlayMode() => Run(TestMode.PlayMode);

        private static void Run(TestMode mode)
        {
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter { testMode = mode }));
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                string mode = result.Test.TestMode == TestMode.PlayMode ? "PlayMode" : "EditMode";
                Directory.CreateDirectory(ResultsFolder);
                TestRunnerApi.SaveResultToFile(result, Path.Combine(ResultsFolder, mode + ".xml"));

                var summary = new StringBuilder();
                summary.Append(mode).Append(": passed ").Append(result.PassCount).Append(", failed ").Append(result.FailCount)
                    .Append(", skipped ").Append(result.SkipCount).Append(", inconclusive ").Append(result.InconclusiveCount)
                    .Append(", ").Append(result.Duration.ToString("0.0")).AppendLine(" s");
                AppendFailures(result, summary);
                File.WriteAllText(Path.Combine(ResultsFolder, mode + "-summary.txt"), summary.ToString());

                if (result.FailCount > 0) Debug.LogError("[Tests] " + summary);
                else Debug.Log("[Tests] " + summary);

                if (mode == "EditMode" && SessionState.GetBool(ChainKey, false))
                {
                    SessionState.SetBool(ChainKey, false);
                    // Start PlayMode after this callback returns, not from inside the finishing run.
                    EditorApplication.delayCall += RunPlayMode;
                }
            }

            private static void AppendFailures(ITestResultAdaptor result, StringBuilder summary)
            {
                if (!result.HasChildren)
                {
                    if (result.TestStatus == TestStatus.Failed)
                        summary.Append("FAIL ").Append(result.FullName).Append(": ").AppendLine(result.Message);
                    return;
                }
                foreach (ITestResultAdaptor child in result.Children) AppendFailures(child, summary);
            }
        }
    }
}
