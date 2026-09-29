using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoatAttack;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// A repeatable in-editor smoke test. It observes a normal Play session without
// changing the scene, race configuration, camera, or quality level.
[InitializeOnLoad]
internal static class Unity6Validation
{
    private static readonly List<string> Errors = new List<string>();
    private static double nextSample;
    private static int samples;

    static Unity6Validation()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                Errors.Add(message + "\n" + stack);
        };
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) Errors.Clear();
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            samples = 0;
            nextSample = EditorApplication.timeSinceStartup + 20;
        };
        EditorApplication.update += Update;
    }

    private static void Update()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused || samples >= 3 ||
            EditorApplication.timeSinceStartup < nextSample ||
            !SceneManager.GetActiveScene().name.Equals("demo_Island", StringComparison.OrdinalIgnoreCase)) return;
        samples++;
        nextSample = EditorApplication.timeSinceStartup + 20;
        WriteReport();
    }

    [MenuItem("Tools/Boat Attack/Write Unity 6 Validation Report")]
    private static void WriteReport()
    {
        Directory.CreateDirectory("Logs/Unity6Validation");
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var transforms = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        var missingScripts = transforms.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        var materials = roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
            .SelectMany(renderer => renderer.sharedMaterials).Where(material => material).Distinct().ToArray();
        var brokenShaders = materials.Where(material => !material.shader || !material.shader.isSupported ||
            ShaderUtil.ShaderHasError(material.shader)).Select(material => material.name).ToArray();
        var boats = UnityEngine.Object.FindObjectsByType<Boat>(FindObjectsSortMode.None);
        var report = new List<string>
        {
            "Time: " + DateTime.UtcNow.ToString("O"),
            "Unity: " + Application.unityVersion,
            "Scene: " + scene.path,
            "Playing: " + EditorApplication.isPlaying,
            "Sample: " + samples,
            "Frame: " + Time.frameCount,
            "Quality: " + QualitySettings.names[QualitySettings.GetQualityLevel()],
            "Render Graph: " + !GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>().enableRenderCompatibilityMode,
            "Race started: " + RaceManager.RaceStarted,
            "Race time: " + RaceManager.RaceTime,
            "Boats: " + boats.Length,
            "Missing scripts: " + missingScripts,
            "Materials with unsupported or failed shaders: " + string.Join(", ", brokenShaders),
            "Errors observed: " + Errors.Count
        };
        foreach (var boat in boats)
            report.Add("Boat: " + boat.name + " at " + boat.transform.position.ToString("F2"));
        report.AddRange(Errors);
        File.WriteAllLines("Logs/Unity6Validation/sample-" + samples + ".txt", report);
        if (EditorApplication.isPlaying)
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/Unity6Validation/sample-" + samples + ".png"));
    }
}
