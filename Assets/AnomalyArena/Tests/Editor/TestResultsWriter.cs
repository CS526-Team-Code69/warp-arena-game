using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace AnomalyArena.EditorTools
{

    [InitializeOnLoad]
    public static class TestResultsWriter
    {
        const string Dir = "Logs";
        const string XmlPath = Dir + "/TestResults.xml";
        const string SummaryPath = Dir + "/TestResults.txt";

        static TestResultsWriter()
        {
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Callbacks());
        }

        [MenuItem("Anomaly Arena/4. Run Play Mode Tests")]
        public static void RunPlayModeTests() => RunPlayMode(null);

        public static bool RunPlayMode(string[] groupNames)
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return false;
            SaveOpenScenesIfDirty();
            var filter = new Filter { testMode = TestMode.PlayMode, assemblyNames = new[] { "AnomalyArena.Tests" } };
            if (groupNames != null && groupNames.Length > 0) filter.groupNames = groupNames;
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(filter));
            return true;
        }

        public static void SaveOpenScenesIfDirty()
        {
            for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
                if (UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i).isDirty)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                    return;
                }
        }

        sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(SummaryPath, $"RUNNING {testsToRun.TestCaseCount} test(s)\n");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(XmlPath, result.ToXml().OuterXml);
                var sb = new StringBuilder();
                sb.AppendLine($"FINISHED {result.ResultState}: passed {result.PassCount}, failed {result.FailCount}, skipped {result.SkipCount}, inconclusive {result.InconclusiveCount}, {result.Duration:0.0}s");
                AppendCases(result, sb);
                File.WriteAllText(SummaryPath, sb.ToString());
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }

            static void AppendCases(ITestResultAdaptor node, StringBuilder sb)
            {
                if (!node.HasChildren)
                {
                    sb.AppendLine($"{node.TestStatus,-12} {node.FullName}");
                    if (node.TestStatus == TestStatus.Failed) sb.AppendLine("    " + node.Message?.Trim().Replace("\n", "\n    "));
                    return;
                }
                foreach (var child in node.Children) AppendCases(child, sb);
            }
        }
    }
}
