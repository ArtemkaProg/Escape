#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage 9: apply original _MainTex pointers to separately copied Unity
/// materials. Metadata is extracted locally from the user's Unity 4.6.5f1 APK.
/// IMPORTANT: original custom shaders, cutout/blending, and runtime animations
/// are not reproduced. This is an independently authored recovery tool.
/// </summary>
public static class EscapeMaterialPass
{
    [Serializable]
    private class MaterialManifest
    {
        public string unityVersion;
        public string format;
        public MaterialRecord[] materials;
    }

    [Serializable]
    private class MaterialRecord
    {
        public int fileId;
        public int id;
        public string name;
        public int shaderFileId;
        public int shaderId;
        public int textureFileId;
        public int textureId;
        public string texturePath;
        public string textureStatus;
        public float[] color;
        public float[] uvScale;
        public float[] uvOffset;
        public float cutoff;
    }

    [Serializable]
    private class MeshManifest
    {
        public string unityVersion;
        public string format;
        public RendererRecord[] meshRenderers;
    }

    [Serializable]
    private class RendererRecord
    {
        public int id;
        public int goId;
        public bool enabled;
        public Ref[] materials;
    }

    [Serializable]
    private class Ref
    {
        public int fileId;
        public int id;
    }

    private const string InputPrefix = "Escape_Zone003_CAMERA_MATCH";
    private const string Output = "Assets/Scenes/Escape_Zone003_MATERIALS_PASS6.unity";
    private const string LocalMaterials = "Assets/RecoveredMaterialTexturesStage9";
    private const string RecoveredMeshChild = "__EscapeRecoveredMesh";

    [MenuItem("Escape Recovery/11 - Restore original material textures (pass 6)")]
    public static void RestoreMaterials()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Escape Stage 9", "Leave Play Mode first.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            !scene.name.StartsWith(InputPrefix, StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog("Escape Stage 9: open the camera-matched scene",
                "Open Assets/Scenes/Escape_Zone003_CAMERA_MATCH.unity first " +
                "(or a numbered copy of that scene). Existing scenes are not overwritten.", "OK");
            return;
        }

        string root = Application.streamingAssetsPath;
        string materialFile = Path.Combine(root, "escape_material_manifest.json");
        string meshFile = Path.Combine(root, "escape_mesh_manifest.json");
        string hierarchyFile = Path.Combine(root, "escape_hierarchy.json");

        if (!File.Exists(materialFile) || !File.Exists(meshFile) ||
            !File.Exists(hierarchyFile))
        {
            EditorUtility.DisplayDialog("Stage 9: missing local metadata",
                "Expected all three files under Assets/StreamingAssets:\n" +
                "escape_material_manifest.json (from Stage 9 ZIP)\n" +
                "escape_mesh_manifest.json (from Stage 8 ZIP)\n" +
                "escape_hierarchy.json (from full recovery archive)", "OK");
            return;
        }

        try
        {
            var manifest = JsonUtility.FromJson<MaterialManifest>(File.ReadAllText(materialFile));
            var sourceMeshes = JsonUtility.FromJson<MeshManifest>(File.ReadAllText(meshFile));
            var sourceScene = JsonUtility.FromJson<EscapeSceneRebuilder.SceneData>(
                File.ReadAllText(hierarchyFile));
            if (manifest == null || manifest.unityVersion != "4.6.5f1" ||
                manifest.format != "ESCMATERIAL1" ||
                manifest.materials == null || manifest.materials.Length != 33 ||
                sourceMeshes == null || sourceMeshes.unityVersion != "4.6.5f1" ||
                sourceMeshes.meshRenderers == null || sourceMeshes.meshRenderers.Length != 3810 ||
                sourceScene == null || sourceScene.gameObjects == null ||
                sourceScene.transforms == null)
                throw new InvalidDataException("Stage 9 data does not match expected original APK counts.");

            // Check original GO mapping and verify all required images BEFORE
            // touching scene objects or creating derivative materials.
            Dictionary<int, GameObject> byId =
                EscapeMeshPass.MatchSceneAllowGeneratedChildren(sourceScene, scene);
            var recordByKey = new Dictionary<string, MaterialRecord>();
            var imageByKey = new Dictionary<string, Texture2D>();
            int withTexture = 0;
            int noSource = 0;
            foreach (MaterialRecord rec in manifest.materials)
            {
                string key = Key(rec.fileId, rec.id);
                if (recordByKey.ContainsKey(key))
                    throw new InvalidDataException("Duplicate material: " + key);
                recordByKey.Add(key, rec);

                if (string.IsNullOrEmpty(rec.texturePath))
                {
                    noSource++;
                    continue;
                }

                // Strict path allowlist: the APK assets must remain local.
                string path = rec.texturePath.Replace('\\', '/');
                if (!path.StartsWith("Assets/RecoveredTextures/", StringComparison.Ordinal) ||
                    !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    path.Contains(".."))
                    throw new InvalidDataException("Unsafe material texture path: " + path);

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                    throw new FileNotFoundException(
                        "Source texture was not imported: " + path +
                        "\nEnsure Stage 5 and the original texture archive are present.");
                imageByKey.Add(key, texture);
                withTexture++;
            }

            if (!EditorUtility.DisplayDialog("Escape Stage 9 — original texture links",
                "Original Material records: " + manifest.materials.Length +
                "\nUsable _MainTex PNG links: " + withTexture +
                "\nMaterials with no usable image: " + noSource +
                "\n\nThis restores _MainTex, _Color, and texture tiling/offset " +
                "only for mesh materials that had actual images in the APK. " +
                "Materials without images remain unchanged.\n\n" +
                "The original shaders and alpha-cutout behavior have NOT " +
                "been ported, so output is only a visual experiment.\n\n" +
                "A NEW scene will be saved; your previous material assets " +
                "and scene remain unchanged. Continue?", "Apply Stage 9", "Cancel"))
                return;

            EditorUtility.DisplayProgressBar("Escape Stage 9", "Preparing local material copies...", .02f);
            if (!AssetDatabase.IsValidFolder(LocalMaterials))
                AssetDatabase.CreateFolder("Assets", "RecoveredMaterialTexturesStage9");

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                throw new InvalidOperationException("Sprites/Default is unavailable in this Unity install.");

            var copies = new Dictionary<string, Material>();
            int i = 0;
            foreach (KeyValuePair<string, Texture2D> pair in imageByKey)
            {
                MaterialRecord source = recordByKey[pair.Key];
                string path = LocalMaterials + "/texmat_" + pair.Key + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    mat.name = source.name + " [relinked Stage9]";
                    AssetDatabase.CreateAsset(mat, path);
                }

                mat.shader = shader;
                mat.SetTexture("_MainTex", pair.Value);
                if (source.color != null && source.color.Length == 4)
                {
                    mat.SetColor("_Color", new Color(source.color[0],
                        source.color[1], source.color[2], source.color[3]));
                }
                if (source.uvScale != null && source.uvScale.Length == 2)
                    mat.SetTextureScale("_MainTex", new Vector2(source.uvScale[0],
                        source.uvScale[1]));
                if (source.uvOffset != null && source.uvOffset.Length == 2)
                    mat.SetTextureOffset("_MainTex", new Vector2(source.uvOffset[0],
                        source.uvOffset[1]));
                EditorUtility.SetDirty(mat);
                copies.Add(pair.Key, mat);
                if (++i % 5 == 0)
                    EditorUtility.DisplayProgressBar("Escape Stage 9",
                        "Creating 23 local textured material assets...",
                        .04f + .36f * i / imageByKey.Count);
            }

            int replacedSlots = 0;
            int modifiedRenderers = 0;
            int missingRenderers = 0;
            foreach (RendererRecord original in sourceMeshes.meshRenderers)
            {
                GameObject go;
                if (!byId.TryGetValue(original.goId, out go))
                    throw new InvalidDataException("Cannot locate original GameObject #" + original.goId);
                Transform child = go.transform.Find(RecoveredMeshChild);
                if (child == null) { missingRenderers++; continue; }

                MeshRenderer mr = child.GetComponent<MeshRenderer>();
                if (mr == null)
                    throw new InvalidDataException("Recovered mesh child has no MeshRenderer on " + go.name);

                Material[] assigned = mr.sharedMaterials;
                if (assigned.Length == 0 || original.materials == null ||
                    original.materials.Length == 0)
                    continue;

                bool touched = false;
                for (int slot = 0; slot < assigned.Length; ++slot)
                {
                    Ref sourceRef = original.materials[
                        Mathf.Min(slot, original.materials.Length - 1)];
                    Material replacement;
                    if (sourceRef == null ||
                        !copies.TryGetValue(Key(sourceRef.fileId, sourceRef.id), out replacement))
                        continue;
                    assigned[slot] = replacement;
                    replacedSlots++;
                    touched = true;
                }
                if (touched)
                {
                    mr.sharedMaterials = assigned;
                    modifiedRenderers++;
                }
            }

            AssetDatabase.SaveAssets();
            string destination = AssetDatabase.GenerateUniqueAssetPath(Output);
            EditorUtility.DisplayProgressBar("Escape Stage 9",
                "Saving a new mesh/textured materials preview scene...", .96f);
            if (!EditorSceneManager.SaveScene(scene, destination, false))
                throw new IOException("Could not save the new scene: " + destination);

            Debug.Log("[Escape Stage 9] Original materials examined: " +
                manifest.materials.Length + "; PNG-linked: " + withTexture +
                "; new material copies: " + copies.Count +
                "; mesh material slots relinked: " + replacedSlots +
                "; MeshRenderers modified: " + modifiedRenderers +
                "; no recovered mesh child: " + missingRenderers +
                ". Original shader/blend/cutout effects are NOT restored.");
            EditorUtility.DisplayDialog("Escape Stage 9 complete",
                "Original materials: " + manifest.materials.Length +
                "\nMaterial copies with PNG: " + copies.Count +
                "\nRelinked slots: " + replacedSlots +
                "\nModified MeshRenderers: " + modifiedRenderers +
                "\n\nSaved as new scene: " + destination +
                "\n\nCompare Scene/Game with Stage 8; alpha-cutout, original " +
                "blend modes and runtime logic remain approximate.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Escape Stage 9 failed", ex.Message, "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static string Key(int fileId, int id)
    {
        return fileId + "_" + id;
    }
}
#endif
