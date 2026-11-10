using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LangGio.EditorTools
{
    /// <summary>
    /// Build bản chạy Windows để chụp ảnh tự động. Gọi từ dòng lệnh:
    ///   Unity.exe -batchmode -quit -projectPath LangGio -executeMethod LangGio.EditorTools.TourBuild.Build
    /// Kết quả: Builds/Tour/LangGio.exe
    /// </summary>
    public static class TourBuild
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string OutPath = "Builds/Tour/LangGio.exe";

        [MenuItem("Làng Gió/Build bản chụp ảnh tự động")]
        public static void Build()
        {
            RenderSetup.Setup(false);
            EnsureScene();
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[Làng Gió] Build {s.result} · {s.totalErrors} lỗi · {s.totalTime} · {s.outputPath}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            var list = EditorBuildSettings.scenes;
            bool has = false;
            foreach (var sc in list) if (sc.path == ScenePath) has = true;
            if (!has) EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
