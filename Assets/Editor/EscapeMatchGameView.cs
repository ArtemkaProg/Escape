#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Diagnostic only: copies the current Scene View editor camera to the Game camera
/// so the recovery scene can be inspected with roughly matching framing.
/// This is new tooling, NOT a recovered original Escape gameplay camera.
/// </summary>
public static class EscapeMatchGameView
{
    private const string InputScene = "Escape_Zone003_MESH_PREVIEW";
    private const string DiagnosticCameraName = "Stage8 Zone003 diagnostic camera";
    private const string OutputScene = "Assets/Scenes/Escape_Zone003_CAMERA_MATCH.unity";

    [MenuItem("Escape Recovery/10 - Copy Scene view to Game camera (diagnostic)")]
    public static void CopyCamera()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Escape recovery", "Exit Play Mode before changing the preview camera.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != InputScene)
        {
            EditorUtility.DisplayDialog("Open Zone003 preview",
                "Open Assets/Scenes/Escape_Zone003_MESH_PREVIEW.unity first. " +
                "This tool copies the current Scene View camera into a NEW scene.", "OK");
            return;
        }

        SceneView view = SceneView.lastActiveSceneView;
        if (view == null || view.camera == null)
        {
            EditorUtility.DisplayDialog("Scene View required",
                "Click the Scene tab, position the view the way you want, " +
                "then run this menu command.", "OK");
            return;
        }

        Camera editorCamera = view.camera;
        GameObject target = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == DiagnosticCameraName)
            {
                if (target != null)
                {
                    EditorUtility.DisplayDialog("Ambiguous camera", "Multiple diagnostic cameras found.", "OK");
                    return;
                }
                target = root;
            }
        }
        if (target == null)
        {
            EditorUtility.DisplayDialog("Diagnostic camera not found",
                "The expected Stage 8 diagnostic camera is missing. " +
                "Reopen the original Zone003 preview scene.", "OK");
            return;
        }

        Camera gameCamera = target.GetComponent<Camera>();
        if (gameCamera == null)
        {
            EditorUtility.DisplayDialog("Camera component missing",
                "The Stage 8 diagnostic GameObject does not have a Camera component.", "OK");
            return;
        }

        string output = AssetDatabase.GenerateUniqueAssetPath(OutputScene);
        bool approve = EditorUtility.DisplayDialog("Match Game view to Scene view",
            "This will use the current Scene tab's view as the diagnostic Game camera.\n\n" +
            "The source preview scene will NOT be overwritten. New scene:\n" + output +
            "\n\nDue to different Game/Scene panel aspect ratios and editor-only overlays, " +
            "the result may not be pixel-identical. This does not reconstruct " +
            "the original gameplay camera.\n\nContinue?", "Copy camera", "Cancel");
        if (!approve) return;

        Undo.RecordObject(target.transform, "Escape Stage 8: copy editor view");
        Undo.RecordObject(gameCamera, "Escape Stage 8: copy editor projection");

        target.transform.position = editorCamera.transform.position;
        target.transform.rotation = editorCamera.transform.rotation;
        gameCamera.orthographic = editorCamera.orthographic;
        gameCamera.orthographicSize = editorCamera.orthographicSize;
        gameCamera.fieldOfView = editorCamera.fieldOfView;
        gameCamera.nearClipPlane = Mathf.Max(0.001f, editorCamera.nearClipPlane);
        gameCamera.farClipPlane = Mathf.Max(gameCamera.nearClipPlane + 1f,
            editorCamera.farClipPlane);
        gameCamera.cullingMask = editorCamera.cullingMask;
        gameCamera.rect = new Rect(0, 0, 1, 1);
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
        gameCamera.enabled = true;

        // Only one active gameplay camera in this isolated diagnostic scene.
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
                if (cam != gameCamera) cam.enabled = false;
        }

        if (!EditorSceneManager.SaveScene(scene, output, false))
        {
            EditorUtility.DisplayDialog("Escape recovery", "Could not save " + output, "OK");
            return;
        }

        Debug.Log("[Escape Stage 8] Copied Scene view camera to Game view. " +
                  "Saved a new diagnostic scene: " + output +
                  ". Perspective/orthographic: " + (gameCamera.orthographic ? "ortho" : "perspective") +
                  ". This is not the original gameplay camera.");
        EditorUtility.DisplayDialog("Camera preview saved",
            "Created: " + output + "\n\nSwitch to Game (without pressing Play) " +
            "to compare the framing.\n\n" +
            "The original preview and original meshes are unchanged.", "OK");
    }
}
#endif
