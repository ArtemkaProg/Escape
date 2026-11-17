#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A safe, editor-only framing tool for the recovered Zone003 scene.
/// The camera here is diagnostic, NOT the original Escape runtime camera.
/// The source material scene is never edited: adjustments are enabled only
/// after a separate preview scene has been saved.
/// </summary>
public sealed class EscapeZone003FramingWindow : EditorWindow
{
    private const string SourcePrefix = "Escape_Zone003_MATERIALS_PASS6";
    private const string PreviewPrefix = "Escape_Zone003_FRAMING_TUNED";
    private const string PreviewPath = "Assets/Scenes/Escape_Zone003_FRAMING_TUNED.unity";
    private const string CameraName = "Stage8 Zone003 diagnostic camera";

    private float moveStep = 1f;
    private float zoomPercentage = 10f;

    [MenuItem("Escape Recovery/12 - Adjust Zone003 PC camera (preview only)")]
    public static void OpenWindow()
    {
        EscapeZone003FramingWindow window = GetWindow<EscapeZone003FramingWindow>("Escape Camera Framing");
        window.minSize = new Vector2(380f, 335f);
        window.Show();
    }

    private static bool IsSource(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded &&
            scene.name.StartsWith(SourcePrefix, StringComparison.Ordinal);
    }

    private static bool IsPreview(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded &&
            scene.name.StartsWith(PreviewPrefix, StringComparison.Ordinal);
    }

    private static Camera FindPreviewCamera(Scene scene)
    {
        Camera result = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != CameraName)
                continue;
            Camera candidate = root.GetComponent<Camera>();
            if (candidate == null || result != null)
                return null;
            result = candidate;
        }
        return result;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Escape — Zone003 camera framing", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Compare with your reference screenshot in Game view, preferably 16:9. " +
            "The Game tab's Scale slider only zooms the editor preview; it does " +
            "not control the actual camera framing.", MessageType.Info);

        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Leave Play Mode before adjusting the camera.", MessageType.Warning);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (IsSource(scene))
        {
            EditorGUILayout.HelpBox(
                "You are viewing the Stage 9 materials scene. To protect it, " +
                "create an independent framing scene before editing any camera values.",
                MessageType.Info);

            if (GUILayout.Button("Create separate camera preview scene", GUILayout.Height(32)))
                CreateWorkingCopy(scene);
            return;
        }

        if (!IsPreview(scene))
        {
            EditorGUILayout.HelpBox(
                "Open Assets/Scenes/Escape_Zone003_MATERIALS_PASS6.unity " +
                "and use the button to create an editable copy. " +
                "Previous stages are not modified.", MessageType.Warning);
            if (GUILayout.Button("Open Stage 9 materials scene"))
                OpenSourceScene();
            return;
        }

        Camera cam = FindPreviewCamera(scene);
        if (cam == null)
        {
            EditorGUILayout.HelpBox(
                "Missing or ambiguous '" + CameraName + "' camera. " +
                "Create a fresh preview from Stage 9.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Camera in: " + scene.name, EditorStyles.miniBoldLabel);
        EditorGUILayout.Space(5);

        Vector3 pos = cam.transform.position;
        EditorGUI.BeginChangeCheck();
        float targetX = EditorGUILayout.FloatField("Camera X", pos.x);
        float targetY = EditorGUILayout.FloatField("Camera Y", pos.y);
        float targetSize;
        if (cam.orthographic)
            targetSize = EditorGUILayout.FloatField("Orthographic Size (zoom)", cam.orthographicSize);
        else
            targetSize = EditorGUILayout.Slider("Field of View (zoom)", cam.fieldOfView, 1f, 175f);

        if (EditorGUI.EndChangeCheck())
        {
            if (!float.IsNaN(targetX) && !float.IsInfinity(targetX) &&
                !float.IsNaN(targetY) && !float.IsInfinity(targetY) &&
                !float.IsNaN(targetSize) && !float.IsInfinity(targetSize))
            {
                Undo.RecordObjects(new UnityEngine.Object[] { cam.transform, cam },
                    "Escape: tune Zone003 camera");
                cam.transform.position = new Vector3(targetX, targetY, pos.z);
                if (cam.orthographic)
                    cam.orthographicSize = Mathf.Clamp(targetSize, 0.01f, 100000f);
                else
                    cam.fieldOfView = Mathf.Clamp(targetSize, 1f, 175f);
                Changed(scene, cam);
            }
        }

        EditorGUILayout.Space(8);
        moveStep = EditorGUILayout.Slider("Pan step (world units)", moveStep, 0.05f, 15f);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Up", GUILayout.Width(90))) Pan(scene, cam, 0f, moveStep);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Left", GUILayout.Width(90))) Pan(scene, cam, -moveStep, 0f);
        if (GUILayout.Button("Down", GUILayout.Width(90))) Pan(scene, cam, 0f, -moveStep);
        if (GUILayout.Button("Right", GUILayout.Width(90))) Pan(scene, cam, moveStep, 0f);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        zoomPercentage = EditorGUILayout.Slider("Zoom step (%)", zoomPercentage, 1f, 40f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Zoom in"))
            Zoom(scene, cam, 1f / (1f + zoomPercentage * 0.01f));
        if (GUILayout.Button("Zoom out"))
            Zoom(scene, cam, 1f + zoomPercentage * 0.01f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Use current Scene view angle"))
            CopyFromSceneView(scene, cam);

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Save tuned preview", GUILayout.Height(28)))
        {
            if (EditorSceneManager.SaveScene(scene))
                Debug.Log("[Escape camera] Saved tuned framing: " + scene.path);
            else
                EditorUtility.DisplayDialog("Save failed", "Could not save tuned preview scene.", "OK");
        }
        EditorGUILayout.HelpBox(
            "All edits affect only this separate FRAMING_TUNED scene. " +
            "Press Ctrl+Z to undo changes. Source MATERIALS_PASS6 is untouched. " +
            "These values are manual framing, NOT original camera values from the APK.",
            MessageType.None);
    }

    private static void CreateWorkingCopy(Scene scene)
    {
        Camera sourceCamera = FindPreviewCamera(scene);
        if (sourceCamera == null)
        {
            EditorUtility.DisplayDialog("Camera not found",
                "Expected a unique Stage8 Zone003 diagnostic camera " +
                "in the materials scene.", "OK");
            return;
        }

        string target = AssetDatabase.GenerateUniqueAssetPath(PreviewPath);
        bool proceed = EditorUtility.DisplayDialog("Create a safe camera preview",
            "Save a new scene:\n" + target + "\n\n" +
            "This copies the recovered materials and camera into a " +
            "separate editing scene. The source scene will not be overwritten.",
            "Create copy", "Cancel");
        if (!proceed)
            return;

        if (!EditorSceneManager.SaveScene(scene, target, false))
        {
            EditorUtility.DisplayDialog("Copy failed", "Unable to create " + target, "OK");
            return;
        }

        Debug.Log("[Escape camera] Created editable framing scene: " + target);
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    private static void OpenSourceScene()
    {
        const string path = "Assets/Scenes/Escape_Zone003_MATERIALS_PASS6.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
        {
            EditorUtility.DisplayDialog("Scene unavailable",
                "Cannot find " + path + ". Open your Stage 9 scene manually.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    private static void Pan(Scene scene, Camera cam, float x, float y)
    {
        Undo.RecordObject(cam.transform, "Escape: pan camera");
        cam.transform.position += new Vector3(x, y, 0f);
        Changed(scene, cam);
    }

    private static void Zoom(Scene scene, Camera cam, float factor)
    {
        Undo.RecordObject(cam, "Escape: zoom camera");
        if (cam.orthographic)
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, 0.01f, 100000f);
        else
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView * factor, 1f, 175f);
        Changed(scene, cam);
    }

    private static void CopyFromSceneView(Scene scene, Camera cam)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null || view.camera == null)
        {
            EditorUtility.DisplayDialog("Scene view missing",
                "Click the Scene tab and position the editor camera first.", "OK");
            return;
        }

        Camera editorCamera = view.camera;
        Undo.RecordObjects(new UnityEngine.Object[] { cam.transform, cam },
            "Escape: match editor camera");
        cam.transform.position = editorCamera.transform.position;
        cam.transform.rotation = editorCamera.transform.rotation;
        cam.orthographic = editorCamera.orthographic;
        cam.orthographicSize = editorCamera.orthographicSize;
        cam.fieldOfView = editorCamera.fieldOfView;
        Changed(scene, cam);
    }

    private static void Changed(Scene scene, Camera cam)
    {
        EditorUtility.SetDirty(cam);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }
}
#endif
