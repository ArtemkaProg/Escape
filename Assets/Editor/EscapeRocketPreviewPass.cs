#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage 14: import the original Rocket's verified SpriteRenderer alpha curves
/// plus a separately authored keyboard demo. Original state machine and
/// movement constants are NOT recovered by this diagnostic pass.
/// </summary>
public static class EscapeRocketPreviewPass
{
    [Serializable] private class Manifest
    {
        public string format, unityVersion;
        public int rocketGoId, animatorRootGoId, originalControllerId;
        public RocketClip[] clips;
    }
    [Serializable] private class RocketClip
    {
        public string name;
        public int sourceId, frameRate, sampleCount;
        public float duration;
        public AlphaCurve[] convertedAlphaCurves;
        public Unconverted[] unconvertedBindings;
    }
    [Serializable] private class AlphaCurve
    {
        public int goId, sourceCurveIndex;
        public string path, attribute;
        public float[] samples;
    }
    [Serializable] private class Unconverted
    {
        public int pathHash, attributeHash, classId;
    }

    private const string Output = "Assets/Scenes/Escape_Zone003_ROCKET_TEST_PASS9.unity";
    private const string AssetsDir = "Assets/RecoveredRocketPreviewStage14";

    [MenuItem("Escape Recovery/17 - Restore rocket visuals and keyboard test (pass 9)")]
    public static void Restore()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Escape", "Exit Play Mode first.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            !(scene.name.StartsWith("Escape_Zone003_OILPUMP_ANIM_PASS8", StringComparison.Ordinal) ||
              scene.name.StartsWith("Escape_Zone003_BG_TUNING", StringComparison.Ordinal)))
        {
            EditorUtility.DisplayDialog("Open a recovered Zone003 scene",
                "Open Escape_Zone003_OILPUMP_ANIM_PASS8 (preferred), " +
                "or Escape_Zone003_BG_TUNING first.", "OK");
            return;
        }
        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog("Save graphics first",
                "Save the source scene with Ctrl+S before creating a rocket preview.", "OK");
            return;
        }

        string metadataPath = Path.Combine(Application.streamingAssetsPath,
            "escape_rocket_visual_clips.json");
        if (!File.Exists(metadataPath))
        {
            EditorUtility.DisplayDialog("Missing local original rocket metadata",
                "Extract the Stage 14 ZIP in the Escape project root. " +
                "Expected Assets/StreamingAssets/escape_rocket_visual_clips.json.", "OK");
            return;
        }

        try
        {
            Manifest source = JsonUtility.FromJson<Manifest>(
                File.ReadAllText(metadataPath));
            Validate(source);

            Transform externRoot = null;
            Camera previewCam = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "ExternGameElements") externRoot = root.transform;
                if (root.name == "Stage8 Zone003 diagnostic camera")
                    previewCam = root.GetComponent<Camera>();
            }

            if (externRoot == null || previewCam == null)
                throw new InvalidDataException(
                    "Original ExternGameElements or diagnostic camera missing.");
            Transform rocket = externRoot.Find("Rocket");
            Transform animatorRoot = rocket == null ? null : rocket.Find("SubAnimator");
            if (rocket == null || animatorRoot == null ||
                rocket.GetComponent<EscapeRocketKeyboardPreview>() != null ||
                animatorRoot.GetComponent<Animator>() != null)
                throw new InvalidDataException(
                    "Original Rocket hierarchy missing or the test was already installed.");

            int converted = 0, unconverted = 0;
            foreach (RocketClip clip in source.clips)
            {
                unconverted += clip.unconvertedBindings.Length;
                foreach (AlphaCurve curve in clip.convertedAlphaCurves)
                {
                    Transform child = animatorRoot.Find(curve.path);
                    if (child == null || child.GetComponent<SpriteRenderer>() == null)
                        throw new InvalidDataException("Unresolved original SpriteRenderer: " +
                            curve.path);
                    converted++;
                }
            }
            if (converted != 24)
                throw new InvalidDataException("Expected 24 source-derived alpha curves");

            if (!EditorUtility.DisplayDialog("Escape — rocket visual/keyboard prototype",
                "Original APK confirms 24 SpriteRenderer alpha curves from 3 clips. " +
                "TakingOff has 3 native/script or particle bindings not yet ported.\n\n" +
                "Create a NEW scene with the recovered Rocket and a keyboard thrust " +
                "demonstration? The original Rocket movement tuning, fuel, collisions " +
                "and AnimatorController state machine are not restored.",
                "Create preview", "Cancel"))
                return;

            string output = AssetDatabase.GenerateUniqueAssetPath(Output);
            if (!EditorSceneManager.SaveScene(scene, output, false))
                throw new IOException("Could not create a separate test scene: " + output);

            if (!AssetDatabase.IsValidFolder(AssetsDir))
                AssetDatabase.CreateFolder("Assets", "RecoveredRocketPreviewStage14");
            string uniqueFolderName = Path.GetFileNameWithoutExtension(output);
            string folder = AssetsDir + "/" + uniqueFolderName;
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(AssetsDir, uniqueFolderName);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(
                folder + "/RocketVisuals_DIAGNOSTIC.controller");
            if (controller == null || controller.layers.Length == 0)
                throw new InvalidOperationException("Cannot create AnimatorController");
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idle = machine.AddState("Idle");
            machine.defaultState = idle;

            foreach (RocketClip raw in source.clips)
            {
                if (raw.convertedAlphaCurves.Length == 0)
                    continue; // TakingOff has no supported SpriteRenderer-alpha channel.
                AnimationClip clip = new AnimationClip
                {
                    name = raw.name + "_RecoveredAlpha",
                    frameRate = 60f,
                    legacy = false
                };
                AssetDatabase.CreateAsset(clip, folder + "/" + raw.name + ".anim");
                foreach (AlphaCurve sourceCurve in raw.convertedAlphaCurves)
                {
                    Keyframe[] keys = new Keyframe[raw.sampleCount];
                    for (int i = 0; i < keys.Length; ++i)
                    {
                        float previous = sourceCurve.samples[Mathf.Max(0, i - 1)];
                        float next = sourceCurve.samples[Mathf.Min(keys.Length - 1, i + 1)];
                        float tangent = (next - previous) * 60f /
                            ((i == 0 || i == keys.Length - 1) ? 1f : 2f);
                        keys[i] = new Keyframe(i / 60f,
                            sourceCurve.samples[i], tangent, tangent);
                    }
                    AnimationUtility.SetEditorCurve(clip,
                        EditorCurveBinding.FloatCurve(sourceCurve.path,
                            typeof(SpriteRenderer), "m_Color.a"),
                        new AnimationCurve(keys));
                }
                AnimatorState state = machine.AddState(raw.name);
                state.motion = clip;
                state.writeDefaultValues = false;
                EditorUtility.SetDirty(clip);
            }
            EditorUtility.SetDirty(machine);

            // ExternGameElements is inactive in the isolated Zone003 preview.
            // Detach the original Rocket only in this newly saved test scene.
            rocket.SetParent(null, false);
            rocket.gameObject.SetActive(true);
            float x = previewCam.transform.position.x -
                previewCam.orthographicSize * (16f / 9f) * .31f;
            rocket.position = new Vector3(x, previewCam.transform.position.y - 1.2f, 0f);

            Animator animator = animatorRoot.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            EscapeRocketKeyboardPreview keyboard =
                rocket.gameObject.AddComponent<EscapeRocketKeyboardPreview>();
            keyboard.visualAnimator = animator;

            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new IOException("Cannot save rocket preview scene " + output);

            Debug.Log("[Escape Stage 14] Original rocket GO #27907; " +
                "source clips: 4; converted SpriteRenderer alpha curves: " +
                converted + "; unconverted native/script bindings: " + unconverted +
                ". Keyboard physics is a separate demo, NOT original gameplay. " +
                "Scene: " + output);

            EditorUtility.DisplayDialog("Rocket test created",
                "Saved: " + output +
                "\n\nPress Play. Hold Space/W/Up for thrust, R for reset. " +
                "Use 1/2/3 to preview recovered visual clips; " +
                "4 audits TakingOff (its original non-sprite channels remain unported).\n\n" +
                "This does not yet restore original fuel, collisions, controller transitions, " +
                "particles or tuned movement.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Stage 14 failed",
                ex.Message + "\n\nReopen the source OilPump scene before retrying " +
                "if the preview was only partially created.", "OK");
        }
    }

    private static void Validate(Manifest m)
    {
        if (m == null || m.format != "ESCROCKET1" ||
            m.unityVersion != "4.6.5f1" || m.rocketGoId != 27907 ||
            m.animatorRootGoId != 19899 || m.originalControllerId != 553 ||
            m.clips == null || m.clips.Length != 4)
            throw new InvalidDataException("Invalid original rocket metadata");
        string[] names = { "StartEngine", "TakingOff",
            "TryingToStartTheEngine", "TryingToTakeOff" };
        int[] ids = { 397, 398, 399, 400 };
        int[] amounts = { 8, 0, 8, 8 };
        int[] frames = { 61, 121, 24, 61 };
        for (int i = 0; i < m.clips.Length; ++i)
        {
            RocketClip c = m.clips[i];
            if (c == null || c.name != names[i] || c.sourceId != ids[i] ||
                c.frameRate != 60 || c.sampleCount != frames[i] ||
                c.convertedAlphaCurves == null ||
                c.convertedAlphaCurves.Length != amounts[i] ||
                c.unconvertedBindings == null)
                throw new InvalidDataException("Unexpected original clip " + names[i]);
            HashSet<int> used = new HashSet<int>();
            foreach (AlphaCurve curve in c.convertedAlphaCurves)
            {
                if (curve == null || curve.goId == 0 ||
                    curve.attribute != "m_Color.a" ||
                    string.IsNullOrEmpty(curve.path) || curve.path.Contains("..") ||
                    curve.samples == null || curve.samples.Length != c.sampleCount ||
                    !used.Add(curve.sourceCurveIndex))
                    throw new InvalidDataException("Invalid sampled alpha binding");
                foreach (float v in curve.samples)
                    if (float.IsNaN(v) || float.IsInfinity(v) || Mathf.Abs(v) > 100)
                        throw new InvalidDataException("Nonfinite original clip sample");
            }
        }
    }
}
#endif
