#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage 15: saves a NEW test scene with tilt and two runtime visibility
/// switches. No PNG, Sprite, shader, shader/material asset or PASS9 scene
/// is modified. The source of the glass artifacts is not assumed.
/// </summary>
public static class EscapeRocketDiagnosticPass
{
    private const string SourceName = "Escape_Zone003_ROCKET_TEST_PASS9";
    private const string OutputScene =
        "Assets/Scenes/Escape_Zone003_ROCKET_DIAGNOSTIC_PASS10.unity";

    [MenuItem("Escape Recovery/18 - Diagnose rocket graphics and tilt (pass 10)")]
    public static void CreateDiagnosticScene()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Escape Stage 15",
                "Leave Play Mode before creating the new scene.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            !scene.name.StartsWith(SourceName, StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog("Open the Rocket PASS9 scene",
                "Open Escape_Zone003_ROCKET_TEST_PASS9.unity first.", "OK");
            return;
        }
        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog("Save first",
                "Save the current Rocket PASS9 scene (Ctrl+S), " +
                "then run the command again.", "OK");
            return;
        }

        GameObject rocket = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Rocket") continue;
            if (rocket != null)
            {
                EditorUtility.DisplayDialog("Ambiguous Rocket",
                    "Multiple Rocket roots exist; nothing changed.", "OK");
                return;
            }
            rocket = root;
        }
        if (rocket == null ||
            rocket.GetComponent<EscapeRocketKeyboardPreview>() == null ||
            rocket.GetComponent<EscapeRocketOrientationDiagnostic>() != null)
        {
            EditorUtility.DisplayDialog("Original Rocket test incomplete",
                "The PASS9 keyboard Rocket root is missing or diagnostics " +
                "are already installed. Reopen the original PASS9 scene.", "OK");
            return;
        }

        int spriteCount = rocket.GetComponentsInChildren<SpriteRenderer>(true).Length;
        if (spriteCount < 5)
        {
            EditorUtility.DisplayDialog("Unexpected Rocket hierarchy",
                "Found only " + spriteCount + " SpriteRenderer(s); " +
                "this does not look like the reconstructed Rocket.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Stage 15: isolate two Rocket issues",
            "Your Rocket moves vertically but does not tilt. " +
            "The original DLL used atan2(vertical velocity, rotationInertia), " +
            "scaled by angularAmplitudeReduction, to compute rotation. " +
            "We will reproduce the formula with TUNABLE DEMO values, " +
            "not claim to have the original parameters.\n\n" +
            "The rocket's glass/gauge sprites use an ETC2 atlas. " +
            "We will add runtime switches to find which sprite group " +
            "causes the colored squares; this does NOT fix the texture.\n\n" +
            "Create a NEW test scene? Current PASS9 is not overwritten.",
            "Create PASS10", "Cancel"))
            return;

        string target = AssetDatabase.GenerateUniqueAssetPath(OutputScene);
        try
        {
            if (!EditorSceneManager.SaveScene(scene, target, false))
                throw new IOException("Cannot save a separate scene at " + target);

            var diagnostic = rocket.AddComponent<EscapeRocketOrientationDiagnostic>();
            diagnostic.rotationInertia = 2.2f;
            diagnostic.angularAmplitudeReduction = .35f;
            diagnostic.maxTestAngle = 40f;
            diagnostic.enableAutomaticTilt = true;

            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new IOException("Cannot save the new PASS10 scene");

            Debug.Log("[Escape Stage 15] Rocket diagnostics installed on " +
                spriteCount + " rocket SpriteRenderers. " +
                "Original rotation formula structure confirmed from DLL; " +
                "numeric coefficients not yet restored. New scene: " + target);
            EditorUtility.DisplayDialog("Rocket PASS10 ready",
                "Created: " + target + "\n\n" +
                "Press Play and click Game:\n" +
                "• Hold Space/W/Up: check automatic tilt\n" +
                "• T: disable/enable tilt\n" +
                "• G: hide/show glass and fuel gauges\n" +
                "• C: hide/show collider-mask sprite layers\n" +
                "• R: reset the rocket\n\n" +
                "Compare screenshot before/after pressing G and C. " +
                "These switches help locate the corrupt-looking sprite layer; " +
                "they do not restore missing source art.",
                "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Stage 15 failed",
                ex.Message + "\nThe original PASS9 scene was not overwritten.", "OK");
        }
    }
}
#endif
