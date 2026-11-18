#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EscapeRocketRecoveryPass11
{
    [Serializable] private class Recovery
    {
        public string format, unityVersion, sourceTextureFormatName;
        public int sourceTextureFormat;
        public Movement movement;
        public SpriteInfo[] sprites;
    }
    [Serializable] private class Movement
    {
        public float thrustStrength, gravity, physicScale, angularAmplitudeReduction, rotationInertia;
    }
    [Serializable] private class SpriteInfo
    {
        public int id;
        public string name, png;
        public float ppu;
        public float[] pivot;
    }

    private const string Source = "Assets/Scenes/Escape_Zone003_ROCKET_DIAGNOSTIC_PASS10.unity";
    private const string Output = "Assets/Scenes/Escape_Zone003_ROCKET_RECOVERED_PASS11.unity";

    [MenuItem("Escape Recovery/19 - Restore ASTC rocket and serialized movement (pass 11)")]
    public static void Create()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Exit Play Mode and save your current scene before PASS11.");
        var data = JsonUtility.FromJson<Recovery>(File.ReadAllText("Assets/RecoveredRocketPass11Local/recovery.json"));
        if (data.format != "ESCROCKET11" || data.unityVersion != "4.6.5f1" ||
            data.sourceTextureFormat != 54 || data.sourceTextureFormatName != "ASTC_RGBA_4x4" ||
            data.sprites == null || data.sprites.Length != 16)
            throw new InvalidDataException("Run tools/recover_rocket_pass11.py with the private original data first.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var sprites = new Dictionary<int, Sprite>();
        foreach (var s in data.sprites)
        {
            if (!s.png.StartsWith("Assets/RecoveredRocketPass11Local/", StringComparison.Ordinal) ||
                s.png.Contains("..") || s.pivot == null || s.pivot.Length != 2 || s.ppu <= 0)
                throw new InvalidDataException("Invalid private sprite metadata");
            var importer = (TextureImporter)AssetImporter.GetAtPath(s.png);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = s.ppu;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(s.pivot[0], s.pivot[1]);
            importer.SetTextureSettings(settings);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            sprites.Add(s.id, AssetDatabase.LoadAssetAtPath<Sprite>(s.png));
        }

        var scene = EditorSceneManager.OpenScene(Source);
        var rocket = scene.GetRootGameObjects().Single(g => g.name == "Rocket");
        string target = AssetDatabase.GenerateUniqueAssetPath(Output);
        if (!EditorSceneManager.SaveScene(scene, target)) throw new IOException("Cannot create PASS11");
        int changed = 0;
        foreach (var sr in rocket.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;
            var match = Regex.Match(AssetDatabase.GetAssetPath(sr.sprite), @"escape_sprite_(\d+)\.png$");
            if (match.Success && sprites.TryGetValue(int.Parse(match.Groups[1].Value), out var sprite))
            {
                sr.sprite = sprite;
                changed++;
            }
        }
        if (changed == 0) throw new InvalidDataException("No rocket sprites matched");
        var keyboard = rocket.GetComponent<EscapeRocketKeyboardPreview>();
        var animator = keyboard.visualAnimator;
        UnityEngine.Object.DestroyImmediate(keyboard);
        UnityEngine.Object.DestroyImmediate(rocket.GetComponent<EscapeRocketOrientationDiagnostic>());
        var reference = new GameObject("PASS11 stationary target reference (zone scrolling pending)");
        reference.transform.position = rocket.transform.position;
        var motion = rocket.AddComponent<EscapeRecoveredRocketMotion>();
        motion.thrustStrength = data.movement.thrustStrength;
        motion.gravity = data.movement.gravity;
        motion.physicScale = data.movement.physicScale;
        motion.angularAmplitudeReduction = data.movement.angularAmplitudeReduction;
        motion.rotationInertia = data.movement.rotationInertia;
        motion.targetReference = reference.transform;
        motion.visualAnimator = animator;
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save PASS11");
        Directory.CreateDirectory("Reports");
        File.WriteAllText("Reports/pass11-scene.txt", target + "\nReplaced rocket renderers: " + changed +
            "\nColliders on rocket: " + rocket.GetComponentsInChildren<Collider2D>(true).Length +
            "\nCollider-mask sprites: " + rocket.GetComponentsInChildren<SpriteRenderer>(true)
                .Count(s => s.name.EndsWith("_collider", StringComparison.Ordinal)) + "\n");
        RenderPreview(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true))
            .Single(c => c.name == "Stage8 Zone003 diagnostic camera"), rocket);
        Debug.Log("PASS11 saved: " + target + "; repaired rocket renderers: " + changed);
    }

    private static void RenderPreview(Camera camera, GameObject rocket)
    {
        Vector3 position = camera.transform.position;
        float size = camera.orthographicSize;
        float aspect = camera.aspect;
        var previous = RenderTexture.active;
        var target = camera.targetTexture;
        var rt = new RenderTexture(1280, 720, 24);
        var png = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt;
            camera.aspect = 1280f / 720;
            Action<string> capture = filename =>
            {
                camera.Render(); RenderTexture.active = rt;
                png.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); png.Apply();
                File.WriteAllBytes(filename, png.EncodeToPNG());
            };
            capture("Reports/pass11-scene.png");
            var renderers = rocket.GetComponentsInChildren<SpriteRenderer>()
                .Where(s => s.enabled && s.color.a > .01f).ToArray();
            Bounds b = renderers[0].bounds;
            foreach (var s in renderers) b.Encapsulate(s.bounds);
            camera.transform.position = new Vector3(b.center.x, b.center.y, position.z);
            camera.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / camera.aspect) * 1.2f;
            capture("Reports/pass11-rocket-close.png");
        }
        finally
        {
            camera.transform.position = position; camera.orthographicSize = size; camera.aspect = aspect;
            camera.targetTexture = target; RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(png);
        }
    }

    // CLI: Unity -batchmode -projectPath ... -executeMethod EscapeRocketRecoveryPass11.Build -quit
    public static void Build()
    {
        try
        {
            if (!File.Exists(Output)) Create();
            else EditorSceneManager.OpenScene(Output);
            string scene = EditorSceneManager.GetActiveScene().path;
            Directory.CreateDirectory("Builds/Windows-PASS11");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = "Builds/Windows-PASS11/Escape-PASS11.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText("Reports/pass11-build.txt", report.summary.result + "\nErrors: " +
                report.summary.totalErrors + "\nSize: " + report.summary.totalSize + "\n");
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows development build failed");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }
}
#endif
