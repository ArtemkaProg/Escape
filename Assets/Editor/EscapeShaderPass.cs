#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Escape Stage 10: independently authored equivalents of selected Unity 4
/// unlit fixed-function shaders. Original shader source is NOT copied.
/// Local-only metadata extracted from the owner's APK supplies original IDs.
/// This is a diagnostic preview, not a playable game.
/// </summary>
public static class EscapeShaderPass
{
    [Serializable]
    private class ShaderManifest
    {
        public string unityVersion;
        public string format;
        public ShaderMaterial[] materials;
    }

    [Serializable]
    private class ShaderMaterial
    {
        public int fileId;
        public int id;
        public string name;
        public int shaderFileId;
        public int shaderId;
        public float[] color;
        public float[] color2;
    }

    [Serializable]
    private class Stage9Manifest
    {
        public MaterialTexture[] materials;
    }

    [Serializable]
    private class MaterialTexture
    {
        public int fileId;
        public int id;
        public string texturePath;
        public float[] uvScale;
        public float[] uvOffset;
    }

    [Serializable]
    private class Stage8Manifest
    {
        public RendererRecord[] meshRenderers;
    }

    [Serializable]
    private class RendererRecord
    {
        public int goId;
        public Ref[] materials;
    }

    [Serializable]
    private class Ref
    {
        public int fileId;
        public int id;
    }

    private const string SourceName = "Escape_Zone003_FRAMING_TUNED";
    private const string Destination = "Assets/Scenes/Escape_Zone003_SHADERS_PASS7.unity";
    private const string AssetRoot = "Assets/RecoveredShaderMaterialsStage10";
    private const string MeshChild = "__EscapeRecoveredMesh";

    [MenuItem("Escape Recovery/13 - Restore original unlit shader effects (pass 7)")]
    public static void Restore()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Escape Stage 10", "Exit Play Mode first.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            !scene.name.StartsWith(SourceName, StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog("Open your saved camera framing",
                "Open Escape_Zone003_FRAMING_TUNED.unity first. " +
                "This tool preserves its framing and saves a separate PASS7 scene.", "OK");
            return;
        }

        string dir = Application.streamingAssetsPath;
        string shaderPath = Path.Combine(dir, "escape_shader_manifest.json");
        string materialPath = Path.Combine(dir, "escape_material_manifest.json");
        string meshPath = Path.Combine(dir, "escape_mesh_manifest.json");
        string hierarchyPath = Path.Combine(dir, "escape_hierarchy.json");
        if (!File.Exists(shaderPath) || !File.Exists(materialPath) ||
            !File.Exists(meshPath) || !File.Exists(hierarchyPath))
        {
            EditorUtility.DisplayDialog("Stage 10 requires local metadata",
                "Extract Escape_2015_Stage10_Shaders_Local.zip into Escape root. " +
                "Expected escape_shader_manifest.json in Assets/StreamingAssets " +
                "alongside existing Stage 8/9 manifests and escape_hierarchy.json.", "OK");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("Escape Stage 10", "Validating original shader metadata...", 0f);
            var original = JsonUtility.FromJson<ShaderManifest>(File.ReadAllText(shaderPath));
            var textures = JsonUtility.FromJson<Stage9Manifest>(File.ReadAllText(materialPath));
            var meshes = JsonUtility.FromJson<Stage8Manifest>(File.ReadAllText(meshPath));
            var hierarchy = JsonUtility.FromJson<EscapeSceneRebuilder.SceneData>(
                File.ReadAllText(hierarchyPath));

            if (original == null || original.unityVersion != "4.6.5f1" ||
                original.format != "ESCSHADER1" || original.materials == null ||
                original.materials.Length != 33 ||
                textures == null || textures.materials == null || textures.materials.Length != 33 ||
                meshes == null || meshes.meshRenderers == null ||
                meshes.meshRenderers.Length != 3810 ||
                hierarchy == null || hierarchy.gameObjects == null ||
                hierarchy.gameObjects.Length != 34993)
                throw new InvalidDataException("Stage 10 data does not match the expected Escape APK.");

            var shaders = new Dictionary<int, Shader>();
            shaders[353] = Shader.Find("EscapeRecovery/SolidColor");
            shaders[354] = shaders[353];
            shaders[357] = shaders[353];
            shaders[356] = Shader.Find("EscapeRecovery/OpaqueVertexColor");
            shaders[358] = Shader.Find("EscapeRecovery/TransparentVertexColor");
            shaders[359] = Shader.Find("EscapeRecovery/InvertedTextureVertex");

            foreach (KeyValuePair<int, Shader> pair in shaders)
                if (pair.Value == null || !pair.Value.isSupported)
                    throw new InvalidOperationException(
                        "Shader for original ID " + pair.Key +
                        " is missing or unsupported. Check Unity shader import errors.");

            var stage9ByKey = new Dictionary<string, MaterialTexture>();
            foreach (MaterialTexture t in textures.materials)
            {
                string key = Key(t.fileId, t.id);
                if (stage9ByKey.ContainsKey(key))
                    throw new InvalidDataException("Duplicate Stage 9 material ID " + key);
                stage9ByKey.Add(key, t);
            }

            // Strictly map all original object IDs before touching the scene.
            var objects = EscapeMeshPass.MatchSceneAllowGeneratedChildren(hierarchy, scene);
            var originals = new Dictionary<string, ShaderMaterial>();
            var images = new Dictionary<string, Texture2D>();
            int backgrounds = 0;
            bool blackVerified = false;
            foreach (ShaderMaterial m in original.materials)
            {
                string key = Key(m.fileId, m.id);
                if (originals.ContainsKey(key) || !stage9ByKey.ContainsKey(key))
                    throw new InvalidDataException("Duplicate or unrecognized Material ID " + key);
                ValidateColor(m.color, m.name);
                ValidateColor(m.color2, m.name);
                originals.Add(key, m);

                if (m.fileId == 1 && m.id == 7)
                {
                    blackVerified = m.shaderId == 357 && m.color[0] == 0f &&
                                    m.color[1] == 0f && m.color[2] == 0f;
                }
                if (m.shaderFileId != 0 || m.shaderId != 359)
                    continue;

                backgrounds++;
                MaterialTexture t = stage9ByKey[key];
                string path = t.texturePath == null ? "" : t.texturePath.Replace('\\', '/');
                if (!path.StartsWith("Assets/RecoveredTextures/", StringComparison.Ordinal) ||
                    !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    path.Contains(".."))
                    throw new InvalidDataException("Invalid original texture path: " + path);

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null)
                    throw new FileNotFoundException("Missing original decoded PNG: " + path);
                images.Add(key, tex);
            }

            if (!blackVerified || backgrounds != 14)
                throw new InvalidDataException("Black.mat or 14 inverted background materials failed validation.");

            if (!EditorUtility.DisplayDialog("Escape Stage 10 — shader comparison",
                "Verified from original APK:\n" +
                "• Black.mat is black (the old extractor missed _Color)\n" +
                "• 14 materials use inverted RGB with alpha blending\n" +
                "• MeshRenderer to Material IDs are available\n\n" +
                "This applies independent Unity 6-compatible unlit shaders and " +
                "saves a SEPARATE scene with your chosen camera framing.\n\n" +
                "No previous scene or material asset is overwritten. " +
                "This is a visual approximation, not original gameplay.",
                "Create shader preview", "Cancel"))
                return;

            // Each run receives its own material folder; old runs remain intact.
            string output = AssetDatabase.GenerateUniqueAssetPath(Destination);
            string folderName = Path.GetFileNameWithoutExtension(output);
            if (!AssetDatabase.IsValidFolder(AssetRoot))
                AssetDatabase.CreateFolder("Assets", "RecoveredShaderMaterialsStage10");
            string materialFolder = AssetRoot + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(materialFolder))
                AssetDatabase.CreateFolder(AssetRoot, folderName);

            var replacements = new Dictionary<string, Material>();
            int materialCount = 0;
            foreach (ShaderMaterial m in original.materials)
            {
                if (m.shaderFileId != 0 || !shaders.ContainsKey(m.shaderId))
                    continue; // Unknown particle/font rendering stays unchanged.

                string key = Key(m.fileId, m.id);
                Material mat = new Material(shaders[m.shaderId]);
                mat.name = m.name + " [Stage10 compatible shader]";
                mat.SetColor("_Color", ReadColor(m.shaderId == 354 ? m.color2 : m.color));

                if (m.shaderId == 357)
                    mat.SetFloat("_Cull", 0f); // original silhouette material was two-sided
                else if (m.shaderId == 353 || m.shaderId == 354)
                    mat.SetFloat("_Cull", 2f);

                if (m.shaderId == 359)
                {
                    mat.SetTexture("_MainTex", images[key]);
                    MaterialTexture u = stage9ByKey[key];
                    if (u.uvScale != null && u.uvScale.Length == 2)
                        mat.SetTextureScale("_MainTex", new Vector2(u.uvScale[0], u.uvScale[1]));
                    if (u.uvOffset != null && u.uvOffset.Length == 2)
                        mat.SetTextureOffset("_MainTex", new Vector2(u.uvOffset[0], u.uvOffset[1]));
                }

                AssetDatabase.CreateAsset(mat, materialFolder + "/material_" + key + ".mat");
                replacements.Add(key, mat);
                materialCount++;
            }

            int changedSlots = 0, changedRenderers = 0, noRecoveredMesh = 0;
            var slotCounts = new Dictionary<int, int>();
            foreach (RendererRecord src in meshes.meshRenderers)
            {
                GameObject go;
                if (!objects.TryGetValue(src.goId, out go))
                    throw new InvalidDataException("Cannot find original GameObject " + src.goId);
                Transform child = go.transform.Find(MeshChild);
                if (child == null)
                {
                    noRecoveredMesh++;
                    continue;
                }
                MeshRenderer mr = child.GetComponent<MeshRenderer>();
                if (mr == null)
                    throw new InvalidDataException("Recovered MeshRenderer is missing on " + go.name);
                if (src.materials == null || src.materials.Length == 0)
                    continue;

                Material[] current = mr.sharedMaterials;
                bool changed = false;
                for (int slot = 0; slot < current.Length; slot++)
                {
                    Ref pointer = src.materials[Mathf.Min(slot, src.materials.Length - 1)];
                    if (pointer == null) continue;
                    string key = Key(pointer.fileId, pointer.id);
                    Material target;
                    if (!replacements.TryGetValue(key, out target))
                        continue;
                    current[slot] = target;
                    changed = true;
                    changedSlots++;
                    int shaderId = originals[key].shaderId;
                    if (!slotCounts.ContainsKey(shaderId)) slotCounts[shaderId] = 0;
                    slotCounts[shaderId]++;
                }
                if (changed)
                {
                    mr.sharedMaterials = current;
                    changedRenderers++;
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayProgressBar("Escape Stage 10", "Saving a new shader preview...", 0.96f);
            if (!EditorSceneManager.SaveScene(scene, output, false))
                throw new IOException("Unity failed to save " + output);

            string breakdown = "";
            foreach (KeyValuePair<int, int> count in slotCounts)
                breakdown += " " + count.Key + "=" + count.Value + ";";

            Debug.Log("[Escape Stage 10] New materials: " + materialCount +
                "; updated material slots: " + changedSlots +
                "; affected MeshRenderers: " + changedRenderers +
                "; objects without recovered mesh: " + noRecoveredMesh +
                "; original shader ID usage:" + breakdown +
                ". Shader effects are independently reimplemented, not original code.");
            EditorUtility.DisplayDialog("Stage 10 complete",
                "New material copies: " + materialCount +
                "\nUpdated slots: " + changedSlots +
                "\nUpdated MeshRenderers: " + changedRenderers +
                "\n\nNew scene: " + output +
                "\n\nCompare with FRAMING_TUNED in Game view (16:9). " +
                "If any shader appears wrong, the older scene is unchanged.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Stage 10 failed", ex.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static string Key(int fileId, int id)
    {
        return fileId + "_" + id;
    }

    private static Color ReadColor(float[] v)
    {
        return new Color(v[0], v[1], v[2], v[3]);
    }

    private static void ValidateColor(float[] v, string name)
    {
        if (v == null || v.Length != 4)
            throw new InvalidDataException("Missing original colour for " + name);
        foreach (float x in v)
            if (float.IsNaN(x) || float.IsInfinity(x) || x < -1 || x > 8)
                throw new InvalidDataException("Invalid original colour for " + name);
    }
}
#endif
