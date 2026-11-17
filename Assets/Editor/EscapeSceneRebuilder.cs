#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Provenance: written for the recovery project, NOT from the original Escape game.
// Reconstructs GameObject/Transform hierarchy ONLY. Gameplay, sprites, meshes, materials,
// scripts, animator state machines and colliders are NOT restored by this importer.
public static class EscapeSceneRebuilder
{
    [Serializable]
    public class GameObjectRecord
    {
        public int id;
        public string name;
        public int layer;
        public bool active;
    }

    [Serializable]
    public class TransformRecord
    {
        public int id;
        public int goId;
        public float[] position;
        public float[] rotation;
        public float[] scale;
        public int parentId;
        public int classId;
    }

    [Serializable]
    public class SceneData
    {
        public string unityVersion;
        public string sceneName;
        public GameObjectRecord[] gameObjects;
        public TransformRecord[] transforms;
    }

    [MenuItem("Escape Recovery/1 - Rebuild original hierarchy (35k objects)")]
    public static void RebuildHierarchy()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "escape_hierarchy.json");
        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog("Missing data", path + " not found.", "OK");
            return;
        }
        bool confirmed = EditorUtility.DisplayDialog(
            "Escape: archival scene rebuild",
            "This creates ~35,000 objects. This is a structural reconstruction only, " +
            "without original rendering, gameplay, collisions or script components. Continue?",
            "Rebuild", "Cancel");
        if (!confirmed) return;

        EditorUtility.DisplayProgressBar("Escape recovery", "Reading original scene hierarchy...", 0f);
        try
        {
            var sceneData = JsonUtility.FromJson<SceneData>(File.ReadAllText(path));
            if (sceneData == null || sceneData.gameObjects == null || sceneData.transforms == null)
                throw new InvalidDataException("Could not deserialize escape_hierarchy.json");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var goById = new Dictionary<int, GameObject>(sceneData.gameObjects.Length);
            var transIdByGo = new Dictionary<int, TransformRecord>(sceneData.transforms.Length);
            var transById = new Dictionary<int, Transform>(sceneData.transforms.Length);
            foreach (var item in sceneData.transforms)
                transIdByGo[item.goId] = item;

            int i = 0;
            foreach (var item in sceneData.gameObjects)
            {
                TransformRecord itemTrans;
                bool rectTransform = transIdByGo.TryGetValue(item.id, out itemTrans) && itemTrans.classId == 224;
                var go = rectTransform
                    ? new GameObject(item.name, typeof(RectTransform))
                    : new GameObject(item.name);
                go.layer = item.layer >= 0 && item.layer <= 31 ? item.layer : 0;
                go.SetActive(item.active);
                goById[item.id] = go;
                if (++i % 1000 == 0)
                    EditorUtility.DisplayProgressBar("Escape recovery", "Creating GameObjects...", (float)i / sceneData.gameObjects.Length * 0.60f);
            }

            i = 0;
            foreach (var item in sceneData.transforms)
            {
                GameObject go;
                if (!goById.TryGetValue(item.goId, out go)) continue;
                var t = go.transform;
                transById[item.id] = t;
                if (item.position != null && item.position.Length == 3)
                    t.localPosition = new Vector3(item.position[0], item.position[1], item.position[2]);
                if (item.rotation != null && item.rotation.Length == 4)
                    t.localRotation = new Quaternion(item.rotation[0], item.rotation[1], item.rotation[2], item.rotation[3]);
                if (item.scale != null && item.scale.Length == 3)
                    t.localScale = new Vector3(item.scale[0], item.scale[1], item.scale[2]);
                if (++i % 1000 == 0)
                    EditorUtility.DisplayProgressBar("Escape recovery", "Placing transforms...", 0.60f + (float)i / sceneData.transforms.Length * 0.20f);
            }

            i = 0;
            foreach (var item in sceneData.transforms)
            {
                Transform child;
                Transform parent;
                if (item.parentId != 0 && transById.TryGetValue(item.id, out child) &&
                    transById.TryGetValue(item.parentId, out parent) && parent != child)
                {
                    child.SetParent(parent, false);
                }
                if (++i % 1000 == 0)
                    EditorUtility.DisplayProgressBar("Escape recovery", "Restoring parent-child relations...", 0.80f + (float)i / sceneData.transforms.Length * 0.19f);
            }
            var inspector = new GameObject("[ARCHIVAL RECONSTRUCTION - gameplay not restored]");
            inspector.AddComponent<EscapeRecoveryNotice>();
            var cameraObject = new GameObject("Recovery preview camera");
            Camera cam = cameraObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5;
            cam.backgroundColor = Color.black;
            cameraObject.transform.position = new Vector3(0, 0, -10);
            cam.clearFlags = CameraClearFlags.SolidColor;
            var folder = "Assets/Scenes";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Scenes");
            var scenePath = folder + "/Escape_Structure_RECONSTRUCTED.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene(scenePath, true)};
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Hierarchy rebuilt",
                "Recovered " + sceneData.gameObjects.Length + " GameObjects and " + sceneData.transforms.Length +
                " transforms. Scene saved to " + scenePath + ".\n\n" +
                "Original MonoBehaviour, meshes, sprite references and playable game logic are NOT included.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Recovery failed", e.ToString(), "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Escape Recovery/2 - Build Windows x64 scaffold (not playable original)")]
    public static void BuildWindows()
    {
        if (!EditorUtility.DisplayDialog("Build the recovery scaffold?",
            "This will make an EXE for the reconstructed structure only. " +
            "The original gameplay is NOT yet recovered. Build anyway?", "Build scaffold", "Cancel")) return;
        if (EditorBuildSettings.scenes == null || EditorBuildSettings.scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("First rebuild the scene", "Run menu item 1 first.", "OK");
            return;
        }
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var targetDir = Path.Combine(projectRoot, "Builds", "Windows");
        Directory.CreateDirectory(targetDir);
        var opts = new BuildPlayerOptions
        {
            scenes = new[] {"Assets/Scenes/Escape_Structure_RECONSTRUCTED.unity"},
            locationPathName = Path.Combine(targetDir, "EscapeRecoveryScaffold.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("Escape recovery Windows build: " + report.summary.result +
                  " | " + report.summary.outputPath);
    }
}
#endif
