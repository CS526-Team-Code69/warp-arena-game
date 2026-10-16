using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AnomalyArena.EditorTools
{

    public static class WebGLBuild
    {
        public const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        public const string Output = "Builds/WebGL-V2";

        public const int MaxTextureSize = 1024;

        [MenuItem("Anomaly Arena/Build WebGL (V2)")]
        public static void Build()
        {
            if (!File.Exists(Scene)) throw new FileNotFoundException("Missing scene", Scene);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            int previousMaxTextureSize = EditorUserBuildSettings.overrideMaxTextureSize;
            EditorUserBuildSettings.overrideMaxTextureSize = MaxTextureSize;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Scene },
                    locationPathName = Output,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });
            }
            finally
            {
                EditorUserBuildSettings.overrideMaxTextureSize = previousMaxTextureSize;
            }
            var summary = report.summary;
            Debug.Log($"[WebGLBuild] result={summary.result} bytes={summary.totalSize} errors={summary.totalErrors} warnings={summary.totalWarnings} seconds={summary.totalTime.TotalSeconds:0}");
            if (summary.result != BuildResult.Succeeded) throw new Exception("WebGL build failed: " + summary.result);
        }
    }
}
