#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Visual diagnostic for reconstructed backgrounds (NOT original game code).
/// Duplicates PASS7 as a separate scene and gives it copies of only the
/// inverted-background materials. Black silhouettes and source assets remain
/// untouched. All tunings are explicitly subjective, not extracted from APK.
/// </summary>
public sealed class EscapeBackgroundTuningWindow : EditorWindow
{
    private const string SourcePrefix = "Escape_Zone003_SHADERS_PASS7";
    private const string PreviewPrefix = "Escape_Zone003_BG_TUNING";
    private const string PreviewPath = "Assets/Scenes/Escape_Zone003_BG_TUNING.unity";
    private const string MaterialsRoot = "Assets/RecoveredBackgroundTuning";
    private const string OriginalShader = "EscapeRecovery/InvertedTextureVertex";
    private const string TunableShader = "EscapeRecovery/InvertedTextureTunable";
    private const string BrightnessProp = "_RecoveryBrightness";
    private const string HazeProp = "_RecoveryHaze";
    private const string HazeColorProp = "_RecoveryHazeColor";

    private readonly List<Material> activeMaterials = new List<Material>();
    private int cachedSceneHandle = -1;
    private float brightness = 1.08f;
    private float haze = .06f;
    private Color hazeColor = new Color(.83f, .86f, .82f, 1f);

    [MenuItem("Escape Recovery/14 - Tune Zone003 background brightness (preview only)")]
    public static void Open()
    {
        var window = GetWindow<EscapeBackgroundTuningWindow>("Escape Background");
        window.minSize = new Vector2(410f, 315f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Escape — recovered background diagnostic", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The YouTube reference may have different exposure and video compression. " +
            "This is a manual comparison, NOT an original brightness value from the APK. " +
            "Only the recovered inverted background materials are adjusted; " +
            "black foreground silhouettes remain unchanged.", MessageType.Info);

        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Exit Play Mode first.", MessageType.Warning);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorGUILayout.HelpBox("Open a Stage 10 scene first.", MessageType.Warning);
            return;
        }

        if (scene.name.StartsWith(SourcePrefix, StringComparison.Ordinal))
        {
            EditorGUILayout.HelpBox(
                "Source: " + scene.name + ". Create a separate tuning scene before " +
                "editing. Stage 10 materials and scene will not be overwritten.",
                MessageType.Info);
            if (GUILayout.Button("Create safe background-tuning preview", GUILayout.Height(32)))
                CreatePreview(scene);
            return;
        }

        if (!scene.name.StartsWith(PreviewPrefix, StringComparison.Ordinal))
        {
            EditorGUILayout.HelpBox(
                "Open Escape_Zone003_SHADERS_PASS7.unity. " +
                "The brightness controls only work in a separate BG_TUNING preview.",
                MessageType.Warning);
            return;
        }

        CacheSceneMaterials(scene);
        if (activeMaterials.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No tuned background materials found in this scene. " +
                "Create a new preview from SHADERS_PASS7.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Scene: " + scene.name, EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Tunable background materials: " + activeMaterials.Count,
            EditorStyles.miniLabel);
        EditorGUILayout.Space(5);
        EditorGUI.BeginChangeCheck();
        float newBrightness = EditorGUILayout.Slider("Background brightness", brightness, .65f, 1.65f);
        float newHaze = EditorGUILayout.Slider("Atmospheric haze", haze, 0f, .5f);
        Color newHazeColor = EditorGUILayout.ColorField("Haze tint", hazeColor);
        if (EditorGUI.EndChangeCheck())
        {
            brightness = newBrightness;
            haze = newHaze;
            hazeColor = newHazeColor;
            Apply();
        }

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Reset to unadjusted Stage 10 appearance"))
        {
            brightness = 1f;
            haze = 0f;
            Apply();
        }
        if (GUILayout.Button("Try gentle lightening (1.08 / 0.06)"))
        {
            brightness = 1.08f;
            haze = .06f;
            Apply();
        }

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Save background-tuning preview", GUILayout.Height(30)))
        {
            AssetDatabase.SaveAssets();
            if (EditorSceneManager.SaveScene(scene))
                Debug.Log("[Escape Stage 11] Saved " + scene.path +
                    " with brightness " + brightness + ", haze " + haze);
            else
                EditorUtility.DisplayDialog("Save error", "Unable to save " + scene.path, "OK");
        }
        EditorGUILayout.HelpBox(
            "Suggested workflow: set Game view to 16:9, compare with your reference, " +
            "and adjust brightness/haze by small amounts. This does not change " +
            "the original game's exposure, global color space or other materials.",
            MessageType.None);
    }

    private static IEnumerable<MeshRenderer> AllMeshRenderers(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
                yield return mr;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string folder = path.Substring(slash + 1);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private void CreatePreview(Scene source)
    {
        Shader shader = Shader.Find(TunableShader);
        if (shader == null || !shader.isSupported)
        {
            EditorUtility.DisplayDialog("Shader unavailable",
                TunableShader + " is missing or unsupported. Check shader compile errors.", "OK");
            return;
        }

        var originals = new HashSet<Material>();
        foreach (MeshRenderer renderer in AllMeshRenderers(source))
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material.shader != null &&
                    material.shader.name == OriginalShader)
                    originals.Add(material);

        if (originals.Count == 0)
        {
            EditorUtility.DisplayDialog("No inverted backgrounds found",
                "Expected the Stage 10 inverted-background materials. " +
                "Open the completed SHADERS_PASS7 scene.", "OK");
            return;
        }

        string newPath = AssetDatabase.GenerateUniqueAssetPath(PreviewPath);
        if (!EditorUtility.DisplayDialog("Create separate background preview",
            "Found " + originals.Count + " original inverted-background materials.\n\n" +
            "Create a NEW scene: " + newPath + "\n" +
            "Only copies of these background materials will be modified. " +
            "SHADERS_PASS7 and all previously recovered assets stay unchanged.",
            "Create preview", "Cancel"))
            return;

        // Save a new scene BEFORE modifying materials; a failure cannot
        // accidentally overwrite the original Stage 10 source.
        if (!EditorSceneManager.SaveScene(source, newPath, false))
        {
            EditorUtility.DisplayDialog("Cannot create preview",
                "Unable to save " + newPath, "OK");
            return;
        }

        try
        {
            EnsureFolder(MaterialsRoot);
            string name = System.IO.Path.GetFileNameWithoutExtension(newPath);
            string folder = MaterialsRoot + "/" + name;
            EnsureFolder(folder);

            Dictionary<Material, Material> replacements = new Dictionary<Material, Material>();
            int index = 0;
            foreach (Material old in originals)
            {
                Material copy = new Material(old);
                copy.name = old.name + " [tunable background]";
                copy.shader = shader;
                copy.SetFloat(BrightnessProp, 1.08f);
                copy.SetFloat(HazeProp, .06f);
                copy.SetColor(HazeColorProp, hazeColor);
                AssetDatabase.CreateAsset(copy, folder + "/Background_" + index++ + ".mat");
                replacements.Add(old, copy);
            }

            int affectedSlots = 0;
            foreach (MeshRenderer renderer in AllMeshRenderers(SceneManager.GetActiveScene()))
            {
                Material[] current = renderer.sharedMaterials;
                bool changed = false;
                for (int slot = 0; slot < current.Length; slot++)
                {
                    Material replacement;
                    if (current[slot] != null &&
                        replacements.TryGetValue(current[slot], out replacement))
                    {
                        current[slot] = replacement;
                        changed = true;
                        affectedSlots++;
                    }
                }
                if (changed) renderer.sharedMaterials = current;
            }

            AssetDatabase.SaveAssets();
            Scene preview = SceneManager.GetActiveScene();
            if (!EditorSceneManager.SaveScene(preview))
                throw new InvalidOperationException("New preview scene could not be saved.");

            cachedSceneHandle = -1;
            CacheSceneMaterials(preview);
            Debug.Log("[Escape Stage 11] Safe preview created: " + newPath +
                "; background material copies: " + replacements.Count +
                "; MeshRenderer slots replaced: " + affectedSlots +
                ". No original shaders/materials/scenes were modified.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Background tuning failed",
                ex.Message + "\n\nThe original Stage 10 scene remains unchanged. " +
                "Reopen it before retrying.", "OK");
        }
    }

    private void CacheSceneMaterials(Scene scene)
    {
        if (cachedSceneHandle == scene.handle) return;
        cachedSceneHandle = scene.handle;
        activeMaterials.Clear();
        var unique = new HashSet<Material>();
        foreach (MeshRenderer renderer in AllMeshRenderers(scene))
            foreach (Material mat in renderer.sharedMaterials)
                if (mat != null && mat.shader != null && mat.shader.name == TunableShader &&
                    unique.Add(mat))
                    activeMaterials.Add(mat);

        if (activeMaterials.Count != 0)
        {
            brightness = activeMaterials[0].GetFloat(BrightnessProp);
            haze = activeMaterials[0].GetFloat(HazeProp);
            hazeColor = activeMaterials[0].GetColor(HazeColorProp);
        }
    }

    private void Apply()
    {
        foreach (Material mat in activeMaterials)
        {
            mat.SetFloat(BrightnessProp, brightness);
            mat.SetFloat(HazeProp, haze);
            mat.SetColor(HazeColorProp, hazeColor);
            EditorUtility.SetDirty(mat);
        }
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        Repaint();
    }
}
#endif
