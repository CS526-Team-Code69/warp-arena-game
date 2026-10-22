using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace AnomalyArena.EditorTools
{

    internal static class RunXpPacingDriver
    {
        const string RequestPath = "Logs/xp-pacing.request";

        [MenuItem("Anomaly Arena/V5/Run XP Pacing Driver")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(RequestPath, DateTime.Now.ToString("s"));
            var filter = new Filter
            {
                testMode = TestMode.PlayMode,
                assemblyNames = new[] { "AnomalyArena.Tests" },
                groupNames = new[] { "XpPacingDriverTest" }
            };
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(filter));
            Debug.Log("XP pacing driver requested; results land in Logs/XpPacing-eff*.txt and Logs/TestResults.txt.");
        }
    }
}
