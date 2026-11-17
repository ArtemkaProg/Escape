#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Restoration pass 3 for the local APK archive. This code is original to the
/// recovery project, NOT the original Escape game's C# source.
/// Only camera settings and Sorting Layer numeric IDs are reconstructed.
/// The original names/order of custom Sorting Layers remain unknown.
/// </summary>
public static class EscapeCameraPass
{
    [Serializable]
    private sealed class CameraManifest
    {
        public string unityVersion;
        public CameraInfo[] cameras;
        public SortingInfo[] sortingLayers;
    }

    [Serializable]
    private sealed class CameraInfo
    {
        public int id;
        public int goId;
        public string name;
        public string path;
        public bool enabled;
        public int clearFlags;
        public float[] backgroundColor;
        public float[] normalizedViewport;
        public float nearClip;
        public float farClip;
        public float fieldOfView;
        public bool orthographic;
        public float orthographicSize;
        public float depth;
        public int cullingMask;
        public int renderingPath;
    }

    [Serializable]
    private sealed class SortingInfo
    {
        public int id;
        public string name;
        public int rendererCount;
    }

    [Serializable]
    private sealed class SpriteManifest
    {
        public RendererInfo[] renderers;
    }

    [Serializable]
    private sealed class RendererInfo
    {
        public int id;
        public int goId;
        public int sortingLayerId;
        public int sortingOrder;
    }

    private const string Pass2 = "Escape_Sprites_PASS2";
    private const string Pass3Path = "Assets/Scenes/Escape_Cameras_PASS3.unity";
    private const string ZonePreviewPath = "Assets/Scenes/Escape_Zone001_PREVIEW.unity";

    [MenuItem("Escape Recovery/5 - Restore original cameras and sorting IDs (pass 3)")]
    public static void RestoreCameras()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != Pass2)
        {
            EditorUtility.DisplayDialog("Wrong scene",
                "Open Assets/Scenes/Escape_Sprites_PASS2.unity first. " +
                "The operation saves a new scene and leaves PASS2 unchanged.", "OK");
            return;
        }

        string baseDir = Application.streamingAssetsPath;
        string cameraPath = Path.Combine(baseDir, "escape_camera_manifest.json");
        string scenePath = Path.Combine(baseDir, "escape_hierarchy.json");
        string spritePath = Path.Combine(baseDir, "escape_sprite_manifest.json");

        if (!File.Exists(cameraPath) || !File.Exists(scenePath) || !File.Exists(spritePath))
        {
            EditorUtility.DisplayDialog("Missing recovery files",
                "Stage 6 needs all three local JSON files in Assets/StreamingAssets:\n" +
                "escape_hierarchy.json\nescape_sprite_manifest.json\nescape_camera_manifest.json\n" +
                "Extract the Stage 6 ZIP in the project root.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Camera restoration — PASS3",
            "This will restore 8 original cameras and the 5 original numeric " +
            "Sorting Layer IDs, using provisional layer names and order.\n\n" +
            "ProjectSettings/TagManager.asset may change. A backup will be made " +
            "in UserSettings/TagManager_before_Escape_PASS3.asset.\n\n" +
            "The result is saved separately as " + Pass3Path + ". Continue?",
            "Restore", "Cancel"))
            return;

        try
        {
            EditorUtility.DisplayProgressBar("Escape camera recovery",
                "Reading recovered APK metadata...", 0);
            var cameraManifest = JsonUtility.FromJson<CameraManifest>(File.ReadAllText(cameraPath));
            var spriteManifest = JsonUtility.FromJson<SpriteManifest>(File.ReadAllText(spritePath));
            var sceneData = JsonUtility.FromJson<EscapeSceneRebuilder.SceneData>(File.ReadAllText(scenePath));
            if (cameraManifest == null || cameraManifest.cameras == null ||
                spriteManifest == null || spriteManifest.renderers == null ||
                sceneData == null || sceneData.gameObjects == null ||
                cameraManifest.cameras.Length != 8 || cameraManifest.sortingLayers == null ||
                cameraManifest.sortingLayers.Length != 5)
                throw new InvalidDataException("Stage 6 data is missing or not from this Escape APK.");

            // This strict mapping validates names, roots, sibling order, and count
            // before attaching any Camera or Sorting Layer references.
            Dictionary<int, GameObject> byId = EscapeSpritePass.MatchExistingScene(sceneData, scene);
            EditorUtility.DisplayProgressBar("Escape camera recovery",
                "Backing up and registering Sorting Layers...", 0.24f);
            int addedLayers = InstallSortingLayers(cameraManifest.sortingLayers);

            int restoredLayers = 0, layerNotFound = 0;
            foreach (RendererInfo info in spriteManifest.renderers)
            {
                GameObject go;
                if (!byId.TryGetValue(info.goId, out go))
                    throw new InvalidDataException("Unmapped SpriteRenderer GO " + info.goId);
                SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                if (sr == null)
                    continue; // Four originally empty sprite refs were skipped in pass 2.
                sr.sortingOrder = info.sortingOrder;
                if (SortingLayer.IsValid(info.sortingLayerId))
                {
                    sr.sortingLayerID = info.sortingLayerId;
                    restoredLayers++;
                }
                else
                {
                    layerNotFound++; // Do not silently use unknown IDs.
                }
            }

            EditorUtility.DisplayProgressBar("Escape camera recovery",
                "Recreating eight original Camera components...", 0.68f);
            var seenObjects = new HashSet<int>();
            foreach (CameraInfo info in cameraManifest.cameras)
            {
                GameObject go;
                if (!byId.TryGetValue(info.goId, out go) || go.name != info.name ||
                    !seenObjects.Add(info.goId))
                    throw new InvalidDataException("Invalid Camera GO " + info.goId);

                Camera cam = go.GetComponent<Camera>();
                if (cam == null) cam = go.AddComponent<Camera>();

                cam.enabled = info.enabled;
                cam.clearFlags = (CameraClearFlags)info.clearFlags;
                cam.backgroundColor = ReadColor(info.backgroundColor);
                cam.rect = ReadRect(info.normalizedViewport);
                cam.nearClipPlane = info.nearClip;
                cam.farClipPlane = info.farClip;
                cam.fieldOfView = info.fieldOfView;
                cam.orthographic = info.orthographic;
                cam.orthographicSize = info.orthographicSize;
                cam.depth = info.depth;
                cam.cullingMask = info.cullingMask;
                // Rendering path, HDR and other camera options were not fully
                // reconstructed; don't fabricate them.
            }

            // Remove only the synthetic preview camera added by Stage 1.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Recovery preview camera") continue;
                Camera preview = root.GetComponent<Camera>();
                if (preview != null) preview.enabled = false;
            }

            EditorUtility.DisplayProgressBar("Escape camera recovery",
                "Saving original camera scene separately...", 0.95f);
            if (!EditorSceneManager.SaveScene(scene, Pass3Path, false))
                throw new IOException("Could not save " + Pass3Path);

            Debug.Log("[Escape Stage 6] Cameras recovered: " + seenObjects.Count +
                      "/8; Sorting Layers added: " + addedLayers +
                      "; SpriteRenderers assigned Sorting Layer ID: " + restoredLayers +
                      "; skipped/unregistered: " + layerNotFound +
                      ". Original layer NAMES AND ORDER are NOT known.");
            EditorUtility.DisplayDialog("Escape Stage 6 — cameras restored",
                "Cameras: " + seenObjects.Count + "/8\n" +
                "Custom Sorting Layers added: " + addedLayers + "\n" +
                "Renderer sorting layer IDs assigned: " + restoredLayers + "\n" +
                "IDs not yet recognized: " + layerNotFound + "\n\n" +
                "Saved: " + Pass3Path + "\n" +
                (layerNotFound > 0
                    ? "Restart Unity and repeat Stage 6 from PASS2 if custom layer IDs are not recognized yet.\n\n"
                    : "") +
                "Next, use Escape Recovery → 6 - Preview Zone001 only (diagnostic).\n\n" +
                "This is a non-playable preview: runtime zone activation, 3D meshes, " +
                "animations and code are not recovered.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Stage 6 failed", ex.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Escape Recovery/6 - Preview Zone001 only (diagnostic)")]
    public static void PreviewZone001()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "Escape_Cameras_PASS3")
        {
            EditorUtility.DisplayDialog("Open the restored camera scene",
                "Open Assets/Scenes/Escape_Cameras_PASS3.unity first.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Isolate Zone001",
            "The original level0 contains many simultaneously serialized zones. " +
            "Normally game scripts select one; those scripts are not running.\n\n" +
            "This diagnostic view keeps only Zone001, ExternGameElements, GameHUD, " +
            "and Managers. It saves a NEW scene " + ZonePreviewPath +
            " without changing PASS2 or PASS3. Continue?", "Isolate Zone001", "Cancel"))
            return;

        int hidden = 0, kept = 0;
        bool foundZone = false;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            bool keep = go.name == "Zone001" ||
                        go.name == "ExternGameElements" ||
                        go.name == "GameHUD" ||
                        go.name == "Managers" ||
                        go.name == "Recovery preview camera" ||
                        go.name.StartsWith("[ARCHIVAL RECONSTRUCTION", StringComparison.Ordinal);
            if (go.name == "Zone001") foundZone = true;
            if (keep)
            {
                kept++;
                go.SetActive(true);
            }
            else
            {
                hidden++;
                go.SetActive(false);
            }
        }

        if (!foundZone)
        {
            EditorUtility.DisplayDialog("Zone001 missing",
                "The selected scene does not contain Zone001.", "OK");
            return;
        }

        if (!EditorSceneManager.SaveScene(scene, ZonePreviewPath, false))
        {
            EditorUtility.DisplayDialog("Failed", "Could not save zone preview.", "OK");
            return;
        }

        Debug.Log("[Escape Stage 6] Zone001 preview saved. Kept " +
                  kept + " root objects, temporarily hid " + hidden +
                  ". Original PASS3 scene remains unchanged.");
        EditorUtility.DisplayDialog("Zone001 preview ready",
            "Saved " + ZonePreviewPath +
            "\n\nSwitch from Scene to Game view (do not press Play). " +
            "The original game logic and runtime camera movement are still missing.", "OK");
    }

    private static Color ReadColor(float[] rgba)
    {
        if (rgba == null || rgba.Length != 4)
            throw new InvalidDataException("Bad camera backgroundColor");
        return new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
    }

    private static Rect ReadRect(float[] xywh)
    {
        if (xywh == null || xywh.Length != 4)
            throw new InvalidDataException("Bad camera normalized viewport");
        return new Rect(xywh[0], xywh[1], xywh[2], xywh[3]);
    }

    private static int InstallSortingLayers(SortingInfo[] original)
    {
        const string tagPath = "ProjectSettings/TagManager.asset";
        UnityEngine.Object[] managers = AssetDatabase.LoadAllAssetsAtPath(tagPath);
        if (managers == null || managers.Length == 0 || managers[0] == null)
            throw new InvalidOperationException("Unity TagManager.asset could not be loaded; no sorting layers modified.");

        var manager = managers[0];
        var so = new SerializedObject(manager);
        SerializedProperty sorting = so.FindProperty("m_SortingLayers");
        if (sorting == null || !sorting.isArray)
            throw new InvalidOperationException("TagManager.m_SortingLayers is unavailable in this Unity Editor.");

        var existing = new HashSet<int>();
        for (int i = 0; i < sorting.arraySize; ++i)
        {
            var layer = sorting.GetArrayElementAtIndex(i);
            var id = layer.FindPropertyRelative("uniqueID");
            if (id == null) throw new InvalidDataException("Sorting Layer uniqueID is missing.");
            existing.Add(unchecked((int)id.uintValue));
        }

        List<SortingInfo> missing = new List<SortingInfo>();
        foreach (SortingInfo item in original)
            if (item.id != 0 && !existing.Contains(item.id)) missing.Add(item);
        if (missing.Count == 0) return 0;

        // A full backup of the original Unity project setting is left in UserSettings.
        string root = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(root, "ProjectSettings", "TagManager.asset");
        string dest = Path.Combine(root, "UserSettings", "TagManager_before_Escape_PASS3.asset");
        Directory.CreateDirectory(Path.GetDirectoryName(dest));
        if (File.Exists(source) && !File.Exists(dest)) File.Copy(source, dest);

        int added = 0;
        foreach (SortingInfo item in missing)
        {
            int lastIndex = sorting.arraySize;
            sorting.InsertArrayElementAtIndex(lastIndex);
            var layer = sorting.GetArrayElementAtIndex(lastIndex);
            var name = layer.FindPropertyRelative("name");
            var id = layer.FindPropertyRelative("uniqueID");
            var locked = layer.FindPropertyRelative("locked");
            if (name == null || id == null || locked == null)
                throw new InvalidDataException("Sorting Layer fields are incompatible with this Unity version.");
            name.stringValue = item.name; // PROVISIONAL — not the original name
            id.uintValue = unchecked((uint)item.id); // ORIGINAL numeric ID
            locked.boolValue = false;
            added++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
        return added;
    }
}
#endif
