#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Escape (2015) archival restoration, Stage 8. This independently written
/// editor tool reads LOCAL decoded mesh data from the user's APK.
/// No proprietary meshes or source code are stored in this repository.
/// </summary>
public static class EscapeMeshPass
{
    [Serializable] private sealed class Manifest
    {
        public string unityVersion;
        public string format;
        public MeshInfo[] meshes;
        public FilterInfo[] meshFilters;
        public RendererInfo[] meshRenderers;
        public MaterialInfo[] materials;
    }
    [Serializable] private sealed class MeshInfo
    {
        public int fileId;
        public int id;
        public string name;
        public string path;
        public int vertexCount;
        public int indexCount;
        public int submeshCount;
    }
    [Serializable] private sealed class FilterInfo
    {
        public int id;
        public int goId;
        public int meshFileId;
        public int meshId;
    }
    [Serializable] private sealed class Pointer
    {
        public int fileId;
        public int id;
    }
    [Serializable] private sealed class RendererInfo
    {
        public int id;
        public int goId;
        public bool enabled;
        public Pointer[] materials;
    }
    [Serializable] private sealed class MaterialInfo
    {
        public int fileId;
        public int id;
        public string name;
        public int shaderFileId;
        public int shaderId;
        public float[] color;
    }

    private const string Stage4 = "Escape_Zone001_GRAPHICS_PASS4";
    private const string OutputScene = "Assets/Scenes/Escape_Meshes_PASS5.unity";
    private const string Zone3Scene = "Assets/Scenes/Escape_Zone003_MESH_PREVIEW.unity";
    private const string MeshAssetFolder = "Assets/RecoveredOriginalMeshes";
    private const string MatAssetFolder = "Assets/RecoveredOriginalMaterials";
    private const string ChildName = "__EscapeRecoveredMesh";

    [MenuItem("Escape Recovery/8 - Restore original meshes (pass 5)")]
    public static void Restore()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Escape", "Exit Play Mode before importing meshes.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != Stage4)
        {
            EditorUtility.DisplayDialog("Stage 8: wrong scene",
                "Open Assets/Scenes/Escape_Zone001_GRAPHICS_PASS4.unity first. " +
                "The existing scene is NOT overwritten.", "OK");
            return;
        }

        string root = Directory.GetParent(Application.dataPath).FullName;
        string manifestPath = Path.Combine(Application.streamingAssetsPath, "escape_mesh_manifest.json");
        string hierarchyPath = Path.Combine(Application.streamingAssetsPath, "escape_hierarchy.json");
        if (!File.Exists(manifestPath) || !File.Exists(hierarchyPath))
        {
            EditorUtility.DisplayDialog("Missing private Stage 8 data",
                "Extract Escape_2015_Stage8_Meshes_Local.zip into the project root.\n\n" +
                "Expected Assets/StreamingAssets/escape_mesh_manifest.json " +
                "and RecoverySource/MeshData/*.meshbin.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Stage 8 — recover original MeshFilter/MeshRenderer",
            "Import 514 decoded Mesh assets and restore original references for up to " +
            "3,781 MeshFilter components. Materials are a BASIC COLOR approximation; " +
            "original shaders and all texture bindings are NOT restored.\n\n" +
            "The command will write NEW local assets and a new scene:\n" +
            OutputScene + "\n\nPrevious scenes remain unchanged. Continue?",
            "Import meshes", "Cancel"))
            return;

        try
        {
            EditorUtility.DisplayProgressBar("Escape Stage 8", "Checking private source data...", 0f);
            Manifest meta = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            EscapeSceneRebuilder.SceneData original =
                JsonUtility.FromJson<EscapeSceneRebuilder.SceneData>(File.ReadAllText(hierarchyPath));
            if (meta == null || meta.unityVersion != "4.6.5f1" || meta.format != "ESCMESH1" ||
                meta.meshes == null || meta.meshes.Length != 514 ||
                meta.meshFilters == null || meta.meshFilters.Length != 3781 ||
                meta.meshRenderers == null || meta.meshRenderers.Length != 3810 ||
                meta.materials == null || meta.materials.Length != 33 ||
                original == null || original.gameObjects == null ||
                original.transforms == null)
                throw new InvalidDataException("Manifest does not match the inspected Escape Unity 4.6.5f1 APK.");

            Dictionary<int, GameObject> goById = MatchSceneAllowGeneratedChildren(original, scene);

            foreach (MeshInfo m in meta.meshes)
            {
                if (m.path == null ||
                    !m.path.StartsWith("RecoverySource/MeshData/", StringComparison.Ordinal) ||
                    m.path.IndexOf("..", StringComparison.Ordinal) >= 0 ||
                    !m.path.EndsWith(".meshbin", StringComparison.Ordinal))
                    throw new InvalidDataException("Unsafe or unknown mesh path: " + m.path);

                string diskPath = Path.Combine(root, m.path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(diskPath))
                    throw new FileNotFoundException("Missing local mesh data: " + diskPath);
            }

            EnsureAssetFolder(MeshAssetFolder);
            EnsureAssetFolder(MatAssetFolder);
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                throw new InvalidOperationException("Unity built-in Sprites/Default shader not found.");

            Dictionary<string, Mesh> meshAssets = new Dictionary<string, Mesh>();
            for (int i = 0; i < meta.meshes.Length; ++i)
            {
                MeshInfo info = meta.meshes[i];
                string key = Key(info.fileId, info.id);
                if (meshAssets.ContainsKey(key))
                    throw new InvalidDataException("Duplicate source mesh " + key);

                string sourcePath = Path.Combine(root, info.path.Replace('/', Path.DirectorySeparatorChar));
                string assetPath = MeshAssetFolder + "/mesh_" + key + ".asset";
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if (mesh == null)
                {
                    mesh = DecodeMesh(sourcePath, info);
                    AssetDatabase.CreateAsset(mesh, assetPath);
                }
                meshAssets.Add(key, mesh);
                if (i % 15 == 0)
                    EditorUtility.DisplayProgressBar("Escape Stage 8",
                        "Importing validated original mesh geometry...",
                        .05f + .55f * (i + 1) / meta.meshes.Length);
            }

            Dictionary<string, Material> mats = new Dictionary<string, Material>();
            Material fallback = LoadOrCreateMaterial("fallback", "Stage 8 default (not original)",
                new Color(1f, 1f, 1f, 1f), shader);
            foreach (MaterialInfo mi in meta.materials)
            {
                string key = Key(mi.fileId, mi.id);
                if (mats.ContainsKey(key))
                    throw new InvalidDataException("Repeated material " + key);
                Color tint = Color.white;
                if (mi.color != null && mi.color.Length == 4)
                    tint = new Color(mi.color[0], mi.color[1], mi.color[2], mi.color[3]);
                mats.Add(key, LoadOrCreateMaterial(key, mi.name, tint, shader));
            }

            Dictionary<int, RendererInfo> renderers = new Dictionary<int, RendererInfo>();
            foreach (RendererInfo r in meta.meshRenderers)
            {
                if (renderers.ContainsKey(r.goId))
                    throw new InvalidDataException("Multiple original MeshRenderers on GO " + r.goId);
                renderers.Add(r.goId, r);
            }

            int populated = 0, nullMeshes = 0, absentRenderer = 0, materialFallbacks = 0;
            for (int i = 0; i < meta.meshFilters.Length; ++i)
            {
                FilterInfo entry = meta.meshFilters[i];
                if (entry.meshFileId == 0 && entry.meshId == 0)
                {
                    nullMeshes++;
                    continue;
                }

                GameObject go;
                Mesh mesh;
                RendererInfo sourceRenderer;
                if (!goById.TryGetValue(entry.goId, out go))
                    throw new InvalidDataException("Original GameObject ID not found: " + entry.goId);
                if (!meshAssets.TryGetValue(Key(entry.meshFileId, entry.meshId), out mesh))
                    throw new InvalidDataException("Mesh reference not found: " + entry.meshFileId +
                        ":" + entry.meshId);
                if (!renderers.TryGetValue(entry.goId, out sourceRenderer))
                {
                    // MeshFilter without MeshRenderer is not rendered in original game.
                    absentRenderer++;
                    continue;
                }

                // A SpriteRenderer and MeshRenderer cannot be attached to the
                // same GameObject. A child avoids the Stage 7 component conflict.
                if (go.transform.Find(ChildName) != null)
                    throw new InvalidOperationException(
                        "Recovered mesh child already exists on " + go.name +
                        ". Reopen the original PASS4 scene before rerunning.");

                GameObject child = new GameObject(ChildName);
                child.layer = go.layer;
                child.transform.SetParent(go.transform, false);
                child.transform.localPosition = Vector3.zero;
                child.transform.localRotation = Quaternion.identity;
                child.transform.localScale = Vector3.one;
                MeshFilter mf = child.AddComponent<MeshFilter>();
                MeshRenderer mr = child.AddComponent<MeshRenderer>();
                mf.sharedMesh = mesh;

                int count = Mathf.Max(1, mesh.subMeshCount);
                Material[] targetMaterials = new Material[count];
                for (int j = 0; j < count; j++)
                {
                    Material material = null;
                    if (sourceRenderer.materials != null &&
                        sourceRenderer.materials.Length > 0)
                    {
                        Pointer ptr = sourceRenderer.materials[
                            Mathf.Min(j, sourceRenderer.materials.Length - 1)];
                        if (ptr != null) mats.TryGetValue(Key(ptr.fileId, ptr.id), out material);
                    }
                    if (material == null) { material = fallback; materialFallbacks++; }
                    targetMaterials[j] = material;
                }
                mr.sharedMaterials = targetMaterials;
                mr.enabled = sourceRenderer.enabled;
                populated++;
                if (i % 60 == 0)
                    EditorUtility.DisplayProgressBar("Escape Stage 8",
                        "Relinking MeshFilters and simplified materials...",
                        .61f + .34f * (i + 1) / meta.meshFilters.Length);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayProgressBar("Escape Stage 8", "Saving a separate scene...", .97f);
            if (!EditorSceneManager.SaveScene(scene, OutputScene, false))
                throw new IOException("Failed to save " + OutputScene);

            Debug.Log("[Escape Stage 8] Decoded Mesh: " + meshAssets.Count +
                "; MeshFilters with rendered meshes: " + populated +
                "; null Mesh refs: " + nullMeshes +
                "; MeshFilter without MeshRenderer: " + absentRenderer +
                "; fallback material slots: " + materialFallbacks +
                ". Original shader and texture effects are NOT restored.");
            EditorUtility.DisplayDialog("Stage 8 — mesh import complete",
                "Imported Mesh assets: " + meshAssets.Count +
                "\nAttached MeshFilter/MeshRenderer pairs: " + populated +
                "\nNull mesh references: " + nullMeshes +
                "\nFilters without a Renderer: " + absentRenderer +
                "\nFallback material slots: " + materialFallbacks +
                "\n\nNew scene: " + OutputScene +
                "\n\nNext: Escape Recovery → 9 - Preview Zone003 meshes (diagnostic).\n" +
                "This is NOT the original playable game.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Escape Stage 8 failed", ex.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Escape Recovery/9 - Preview Zone003 meshes (diagnostic)")]
    public static void PreviewZone003()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "Escape_Meshes_PASS5")
        {
            EditorUtility.DisplayDialog("Open PASS5", "Open " + OutputScene + " first.", "OK");
            return;
        }

        GameObject zone = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Zone003") zone = root;
        if (zone == null)
        {
            EditorUtility.DisplayDialog("Zone003 missing", "No root named Zone003.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Zone003 mesh inspection",
            "Make a NEW isolated preview scene containing Zone003 mesh data, " +
            "with a diagnostic camera centered on its Mesh bounds.\n\n" +
            "This is not original camera logic. Original PASS5 is not overwritten.",
            "Create preview", "Cancel"))
            return;

        bool have = false;
        Bounds bounds = default(Bounds);
        foreach (MeshFilter mf in zone.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds local = mf.sharedMesh.bounds;
            Vector3 c = local.center, e = local.extents;
            for (int mask = 0; mask < 8; mask++)
            {
                Vector3 p = mf.transform.TransformPoint(
                    c + Vector3.Scale(e, new Vector3(
                        (mask & 1) == 0 ? -1 : 1,
                        (mask & 2) == 0 ? -1 : 1,
                        (mask & 4) == 0 ? -1 : 1)));
                if (!have) { bounds = new Bounds(p, Vector3.zero); have = true; }
                else bounds.Encapsulate(p);
            }
        }
        if (!have)
        {
            EditorUtility.DisplayDialog("No meshes in Zone003",
                "Stage 8 did not attach any meshes under Zone003.", "OK");
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
            if (root != zone) root.SetActive(false);
        zone.SetActive(true);

        // Original Cameras are disabled in this diagnostic scene ONLY.
        var cameraGO = new GameObject("Stage8 Zone003 diagnostic camera");
        var camera = cameraGO.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Clamp(
            Mathf.Max(bounds.extents.y, bounds.extents.x / (16f / 9f)) * 1.2f,
            2f, 250f);
        camera.backgroundColor = new Color(.13f, .15f, .17f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.nearClipPlane = .01f;
        camera.farClipPlane = Mathf.Max(100f, bounds.size.z + 100f);
        cameraGO.transform.position = new Vector3(bounds.center.x, bounds.center.y,
            bounds.min.z - 40f);
        cameraGO.transform.rotation = Quaternion.identity;
        if (!EditorSceneManager.SaveScene(scene, Zone3Scene, false))
            throw new IOException("Could not save " + Zone3Scene);

        Debug.Log("[Escape Stage 8] Zone003 preview saved. World mesh bounds: " +
            bounds + "; diagnostic camera size " + camera.orthographicSize);
        EditorUtility.DisplayDialog("Zone003 preview saved",
            "Created " + Zone3Scene +
            "\n\nSelect Game View. This diagnostic camera is NOT from the original game; " +
            "its position is based on decoded mesh bounds.\n\n" +
            "Preview material shaders/colors are approximations.", "OK");
    }

    // Relies on the exact original hierarchy order. Stage 7 adds only
    // __EscapeRecoveredPolygon children; these are skipped during ID matching.
    internal static Dictionary<int, GameObject> MatchSceneAllowGeneratedChildren(
        EscapeSceneRebuilder.SceneData original, Scene scene)
    {
        var objects = new Dictionary<int, EscapeSceneRebuilder.GameObjectRecord>();
        var transformsByGo = new Dictionary<int, EscapeSceneRebuilder.TransformRecord>();
        var children = new Dictionary<int, List<int>>();
        foreach (var item in original.gameObjects) objects.Add(item.id, item);
        foreach (var t in original.transforms)
        {
            transformsByGo.Add(t.goId, t);
            List<int> list;
            if (!children.TryGetValue(t.parentId, out list))
            {
                list = new List<int>();
                children.Add(t.parentId, list);
            }
            list.Add(t.goId);
        }

        var roots = new Dictionary<string, Transform>();
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == "Recovery preview camera" ||
                go.name == "Stage8 Zone003 diagnostic camera" ||
                go.name.StartsWith("[ARCHIVAL RECONSTRUCTION", StringComparison.Ordinal))
                continue;
            if (roots.ContainsKey(go.name))
                throw new InvalidDataException("Duplicate root: " + go.name);
            roots.Add(go.name, go.transform);
        }

        var stack = new Stack<KeyValuePair<int, Transform>>();
        int rootCount = 0;
        foreach (var t in original.transforms)
        {
            if (t.parentId != 0) continue;
            Transform actual;
            if (!roots.TryGetValue(objects[t.goId].name, out actual))
                throw new InvalidDataException("Missing root: " + objects[t.goId].name);
            stack.Push(new KeyValuePair<int, Transform>(t.goId, actual));
            rootCount++;
        }
        if (rootCount != roots.Count)
            throw new InvalidDataException("Root count mismatch. Open the original PASS4 scene.");

        var mapped = new Dictionary<int, GameObject>(objects.Count);
        while (stack.Count > 0)
        {
            var pair = stack.Pop();
            if (pair.Value.name != objects[pair.Key].name)
                throw new InvalidDataException("Hierarchy name mismatch on " + pair.Value.name);
            mapped.Add(pair.Key, pair.Value.gameObject);

            List<int> expected;
            int transformId = transformsByGo[pair.Key].id;
            if (!children.TryGetValue(transformId, out expected))
                expected = new List<int>();

            var found = new List<Transform>();
            foreach (Transform ch in pair.Value)
                if (ch.name != "__EscapeRecoveredPolygon" &&
                    ch.name != "__EscapeRecoveredMesh")
                    found.Add(ch);

            if (found.Count != expected.Count)
                throw new InvalidDataException(
                    "Hierarchy child count differs on " + pair.Value.name +
                    ". Reopen the original Stage 7 PASS4 scene.");
            for (int i = expected.Count - 1; i >= 0; --i)
            {
                if (found[i].name != objects[expected[i]].name)
                    throw new InvalidDataException("Sibling order differs under " + pair.Value.name);
                stack.Push(new KeyValuePair<int, Transform>(expected[i], found[i]));
            }
        }

        if (mapped.Count != original.gameObjects.Length)
            throw new InvalidDataException("Mapped " + mapped.Count +
                " of " + original.gameObjects.Length + " original GameObjects.");
        return mapped;
    }

    private static Mesh DecodeMesh(string file, MeshInfo info)
    {
        using (var br = new BinaryReader(File.OpenRead(file)))
        {
            if (System.Text.Encoding.ASCII.GetString(br.ReadBytes(8)) != "ESCMESH1")
                throw new InvalidDataException("Bad mesh magic in " + info.path);
            int vc = br.ReadInt32();
            int ic = br.ReadInt32();
            int sub = br.ReadInt32();
            if (vc != info.vertexCount || ic != info.indexCount || sub != info.submeshCount ||
                vc < 3 || vc > 100000 || ic <= 0 || ic > 600000 ||
                sub < 1 || sub > 24)
                throw new InvalidDataException("Unexpected mesh counts: " + info.path);

            var subStart = new int[sub];
            var subCount = new int[sub];
            for (int k = 0; k < sub; k++)
            {
                subStart[k] = br.ReadInt32();
                subCount[k] = br.ReadInt32();
                if (subStart[k] < 0 || subCount[k] < 0 ||
                    subStart[k] + subCount[k] > ic || subCount[k] % 3 != 0)
                    throw new InvalidDataException("Invalid triangle submesh " + info.path);
            }
            var vertices = new Vector3[vc];
            var normals = new Vector3[vc];
            var uv = new Vector2[vc];
            var color = new Color32[vc];
            for (int i = 0; i < vc; ++i)
            {
                vertices[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                normals[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                uv[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                color[i] = new Color32(br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte());
            }
            var indices = new int[ic];
            for (int i = 0; i < ic; ++i)
            {
                indices[i] = br.ReadUInt16();
                if (indices[i] >= vc)
                    throw new InvalidDataException("Mesh index out of bounds: " + info.path);
            }
            if (br.BaseStream.Position != br.BaseStream.Length)
                throw new InvalidDataException("Trailing mesh bytes: " + info.path);

            var mesh = new Mesh();
            mesh.name = info.name + " [recovered " + info.fileId + ":" + info.id + "]";
            mesh.indexFormat = vc > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.colors32 = color;
            mesh.subMeshCount = sub;
            for (int k = 0; k < sub; ++k)
            {
                int[] slice = new int[subCount[k]];
                Array.Copy(indices, subStart[k], slice, 0, subCount[k]);
                mesh.SetTriangles(slice, k, false);
            }
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    private static Material LoadOrCreateMaterial(string key, string name,
        Color color, Shader shader)
    {
        string path = MatAssetFolder + "/material_" + key + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var mat = new Material(shader);
        mat.name = name + " [approximate]";
        mat.color = color;
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", Texture2D.whiteTexture);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static string Key(int fileId, int id) { return fileId + "_" + id; }

    private static void EnsureAssetFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = "Assets";
        string name = path.Substring(path.LastIndexOf('/') + 1);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
