using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
    /// <summary>
    /// 打包可在 GitHub Pages 上运行的 V2 网页包。Brotli + 解压回退：Pages 不发送 Content-Encoding 头，由加载器在浏览器里解压。
    /// Brotli 比 Gzip 压得更小（四个文件合计约小一成，数据文件离 GitHub 单个文件 100 MB 的上限更远），代价是加载器在浏览器里解压更久：
    /// 在一台台式机上从本地服务器打开，加载完成从约 3.0 秒变成约 4.5 秒；网速越慢，少下载的部分省下的时间越多。
    /// 菜单 "2. Build WebGL" 打的是 1.0 原型 Arena.unity，不要用它发布当前游戏。
    /// 2026-11-10 起只有一个游戏、两种语言：两个菜单项都打 ArenaV2 这一个场景，同一个包放在英文页和 /zh/ 页，语言由地址决定
    /// （ArenaLanguage）。两个输出文件夹的名字照旧，好让现有的打包脚本不用改。
    /// </summary>
    public static class WebGLBuild
    {
        public const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        public const string Output = "Builds/WebGL-V2";
        /// <summary>The same scene as Scene since 2026-11-10; kept so callers of the old name still build the one game.</summary>
        public const string SceneChinese = Scene;
        public const string OutputChinese = "Builds/WebGL-CN";
        const string Template = "Assets/WebGLTemplates/WarpArena";
        /// <summary>只在打包时把贴图上限压到1024（本机 Library 里的临时覆盖，不改资源导入设置），使 .data 小于 GitHub 单文件100MB上限。</summary>
        public const int MaxTextureSize = 1024;

        [MenuItem("Anomaly Arena/Build WebGL (V2)")]
        public static void Build() => BuildScene(Scene, Output);

        /// <summary>The same package into the folder the Chinese page's scripts read.</summary>
        [MenuItem("Anomaly Arena/Build WebGL (V2 Chinese)")]
        public static void BuildChinese() => BuildScene(SceneChinese, OutputChinese);

        static void BuildScene(string scene, string output)
        {
            if (!File.Exists(scene)) throw new FileNotFoundException("Missing scene", scene);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            // The published page uses the project's own template (Assets/WebGLTemplates/WarpArena): the canvas fills the browser window at 16:9.
            if (Directory.Exists(Template)) PlayerSettings.WebGL.template = "PROJECT:WarpArena";
            int previousMaxTextureSize = EditorUserBuildSettings.overrideMaxTextureSize;
            EditorUserBuildSettings.overrideMaxTextureSize = MaxTextureSize;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scene },
                    locationPathName = output,
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
