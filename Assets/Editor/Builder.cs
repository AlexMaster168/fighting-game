using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Builder
{
    const string SceneDir = "Assets/Scenes";
    const string ScenePath = SceneDir + "/Arena.unity";
    const string ResDir = "Assets/Resources";
    const string MatPath = ResDir + "/BaseMat.mat";

    [MenuItem("Shadow Arena/Build Windows x64")]
    public static void BuildWindows()
    {
        EnsureAssets();

        PlayerSettings.companyName = "Shadow Arena";
        PlayerSettings.productName = "Shadow Arena";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/ShadowArena.exe",
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary sum = report.summary;
        if (sum.result == BuildResult.Succeeded)
            Debug.Log("BUILD OK -> " + sum.outputPath + "  (" + (sum.totalSize / 1048576) + " MB)");
        else
            Debug.LogError("BUILD FAILED: " + sum.result + ", errors: " + sum.totalErrors);

        if (Application.isBatchMode)
            EditorApplication.Exit(sum.result == BuildResult.Succeeded ? 0 : 1);
    }

    // The game builds itself from code, so the player only needs one scene plus a material
    // asset that pins the shader into the build (Shader.Find alone would not include it).
    static void EnsureAssets()
    {
        if (!Directory.Exists(ResDir)) Directory.CreateDirectory(ResDir);
        if (AssetDatabase.LoadAssetAtPath<Material>(MatPath) == null)
        {
            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(sh) { color = Color.white };
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);
        if (!File.Exists(ScenePath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}
