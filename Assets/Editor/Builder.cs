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

    // Arenas turn fog on at runtime; by default Unity strips fog shader variants the scenes do not use.
    static void KeepFogVariants()
    {
        var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
        if (gs == null) { Debug.LogWarning("GraphicsSettings not found, fog may be stripped"); return; }
        var so = new SerializedObject(gs);
        var strip = so.FindProperty("m_FogStripping");
        if (strip != null) strip.intValue = 1;   // custom
        int kept = 0;
        foreach (string n in new[] { "m_FogKeepLinear", "m_FogKeepExp", "m_FogKeepExp2" })
        {
            var q = so.FindProperty(n);
            if (q != null) { q.boolValue = true; kept++; }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log("FOG variants kept: stripping=" + (strip != null) + " modes=" + kept);
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

        // emissive material keeps the _EMISSION variant; the trail material pulls Sprites/Default into the build
        if (AssetDatabase.LoadAssetAtPath<Material>(ResDir + "/GlowMat.mat") == null)
        {
            var g = new Material(Shader.Find("Standard")) { color = Color.white };
            g.EnableKeyword("_EMISSION");
            g.SetColor("_EmissionColor", Color.white);
            g.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(g, ResDir + "/GlowMat.mat");
        }
        if (AssetDatabase.LoadAssetAtPath<Material>(ResDir + "/TrailMat.mat") == null)
            AssetDatabase.CreateAsset(new Material(Shader.Find("Sprites/Default")), ResDir + "/TrailMat.mat");
        KeepFogVariants();

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
