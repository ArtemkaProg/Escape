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
/// Stage 13: creates a Unity 6 AnimationClip sampled from the ORIGINAL
/// OilPumpAnimation m_MuscleClip (60 Hz, 2 sec), plus a basic looping preview
/// AnimatorController. The original state machine/gameplay is NOT reconstructed.
/// All original sampled curves stay in a private, user-local JSON.
/// </summary>
public static class EscapeOilPumpAnimationPass
{
    [Serializable] private class Data {
        public string format, unityVersion, clipName;
        public int sourceFileId, sourceId, frameRate, sourceStreamedCurves,
                   sourceConstantCurves, sampleCount;
        public float duration;
        public bool loop;
        public Curve[] curves;
        public Target[] animators;
    }
    [Serializable] private class Curve {
        public string path, attribute;
        public int sourceCurveIndex;
        public bool constant;
        public float[] samples;
    }
    [Serializable] private class Target {
        public int goId, originalControllerId;
        public string goPath;
    }

    private const string PreviewName = "Assets/Scenes/Escape_Zone003_OILPUMP_ANIM_PASS8.unity";
    private const string LocalDir = "Assets/RecoveredOilPumpAnimationStage13";

    [MenuItem("Escape Recovery/16 - Restore OilPump animation (pass 8)")]
    public static void Restore()
    {
        if (EditorApplication.isPlaying) {
            EditorUtility.DisplayDialog("Escape", "Leave Play Mode first.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            !(scene.name.StartsWith("Escape_Zone003_BG_TUNING", StringComparison.Ordinal) ||
              scene.name.StartsWith("Escape_Zone003_SHADERS_PASS7", StringComparison.Ordinal))) {
            EditorUtility.DisplayDialog("Open a Stage 11 scene",
                "Open Escape_Zone003_BG_TUNING (preferred), or SHADERS_PASS7.", "OK");
            return;
        }
        if (scene.isDirty) {
            EditorUtility.DisplayDialog("Save graphics first",
                "Save this scene with Ctrl+S before running PASS8. Its settings will be preserved.", "OK");
            return;
        }

        string input = Path.Combine(Application.streamingAssetsPath, "escape_oilpump_animation.json");
        string hierarchyPath = Path.Combine(Application.streamingAssetsPath, "escape_hierarchy.json");
        if (!File.Exists(input) || !File.Exists(hierarchyPath)) {
            EditorUtility.DisplayDialog("Missing local animation data",
                "Extract Escape_2015_Stage13_OilPump_Animation_Local.zip into Escape root.", "OK");
            return;
        }

        try {
            Data d = JsonUtility.FromJson<Data>(File.ReadAllText(input));
            Check(d);
            var original = JsonUtility.FromJson<EscapeSceneRebuilder.SceneData>(
                File.ReadAllText(hierarchyPath));
            if (original == null || original.gameObjects == null ||
                original.gameObjects.Length != 34993)
                throw new InvalidDataException("Original hierarchy does not match the APK");
            var mapped = EscapeMeshPass.MatchSceneAllowGeneratedChildren(original, scene);
            var zone3Pumps = new List<GameObject>();
            int zone31 = 0;
            foreach (Target t in d.animators) {
                GameObject go;
                if (!mapped.TryGetValue(t.goId, out go) || go.name != "SubAnimRoot")
                    throw new InvalidDataException("OilPump GameObject missing: " + t.goId);
                if (go.GetComponent<Animator>() != null)
                    throw new InvalidOperationException("Existing Animator on " + t.goPath +
                        "; reopen the original Stage11 scene before rerunning.");
                foreach (Curve c in d.curves)
                    if (go.transform.Find(c.path) == null)
                        throw new InvalidDataException("Missing animated child " + c.path +
                            " on " + t.goPath);
                if (t.goPath.StartsWith("Zone003/", StringComparison.Ordinal))
                    zone3Pumps.Add(go);
                else if (t.goPath.StartsWith("Zone031/", StringComparison.Ordinal))
                    zone31++;
                else
                    throw new InvalidDataException("Unexpected OilPump zone " + t.goPath);
            }
            if (zone3Pumps.Count != 4 || zone31 != 4)
                throw new InvalidDataException("Expected four original pumps in each of Zone003/Zone031");

            if (!EditorUtility.DisplayDialog("Restore OilPump movement",
                "Original clip: 2 seconds, 60 fps; 14 streamed plus 3 constant channels.\n\n" +
                "A NEW sampled Unity 6 .anim, a simple looping .controller, and a new " +
                "scene will be created. Four pumps in Zone003 receive an Animator.\n\n" +
                "The ORIGINAL state machine, interaction and game physics are NOT ported.",
                "Create test scene", "Cancel")) return;

            string output = AssetDatabase.GenerateUniqueAssetPath(PreviewName);
            if (!AssetDatabase.IsValidFolder(LocalDir))
                AssetDatabase.CreateFolder("Assets", "RecoveredOilPumpAnimationStage13");
            string subfolder = LocalDir + "/" + Path.GetFileNameWithoutExtension(output);
            if (!AssetDatabase.IsValidFolder(subfolder))
                AssetDatabase.CreateFolder(LocalDir, Path.GetFileNameWithoutExtension(output));
            if (!EditorSceneManager.SaveScene(scene, output, false))
                throw new IOException("Unable to create new scene " + output);

            EditorUtility.DisplayProgressBar("Escape Stage 13", "Importing OilPump curves...", .1f);
            var clip = new AnimationClip {
                name = "OilPumpAnimation_Reconstructed_60Hz",
                frameRate = 60, legacy = false, wrapMode = WrapMode.Loop
            };
            AssetDatabase.CreateAsset(clip, subfolder + "/OilPumpAnimation_Reconstructed.anim");
            foreach (Curve c in d.curves) {
                var keys = new Keyframe[d.sampleCount];
                for (int frame = 0; frame < d.sampleCount; frame++) {
                    float value = c.samples[frame];
                    float left = c.samples[Mathf.Max(0, frame - 1)];
                    float right = c.samples[Mathf.Min(d.sampleCount - 1, frame + 1)];
                    float tangent = (right - left) * d.frameRate /
                        (frame == 0 || frame == d.sampleCount - 1 ? 1f : 2f);
                    keys[frame] = new Keyframe(frame / (float)d.frameRate, value, tangent, tangent);
                }
                var curve = new AnimationCurve(keys) {
                    preWrapMode = WrapMode.Loop, postWrapMode = WrapMode.Loop
                };
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(c.path, typeof(Transform), c.attribute), curve);
            }
            var serialized = new SerializedObject(clip);
            SerializedProperty loop = serialized.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (loop == null)
                throw new InvalidOperationException("Cannot set Unity 6 clip looping metadata");
            loop.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clip);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(
                subfolder + "/OilPump_LoopPreview.controller");
            if (controller == null || controller.layers.Length == 0)
                throw new InvalidOperationException("Failed to create Unity AnimatorController");
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState("Original OilPumpAnimation (sampled)");
            state.motion = clip;
            stateMachine.defaultState = state;
            EditorUtility.SetDirty(stateMachine);

            foreach (GameObject go in zone3Pumps) {
                Animator animator = go.AddComponent<Animator>();
                if (animator == null)
                    throw new InvalidOperationException("Unable to attach Animator on " + go.name);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new IOException("Unable to save animated preview scene");
            Debug.Log("[Escape Stage 13] OilPumpAnimation imported: 17 sampled curves, " +
                "121 samples each, 2.0 sec looping. Zone003 Animator roots: " +
                zone3Pumps.Count + ". Original state machine NOT imported. Scene: " + output);
            EditorUtility.DisplayDialog("PASS8 animation preview ready",
                "Created: " + output + "\n\nFour Zone003 pumps now have a simple " +
                "looping AnimatorController.\n\nPress Play to watch the pump motion; " +
                "this is not full gameplay.", "OK");
        }
        catch (Exception ex) {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Stage 13 failed", ex.Message, "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void Check(Data d)
    {
        if (d == null || d.format != "ESCOILPUMP1" || d.unityVersion != "4.6.5f1" ||
            d.clipName != "OilPumpAnimation" || d.sourceFileId != 1 || d.sourceId != 394 ||
            d.frameRate != 60 || d.duration != 2f || !d.loop ||
            d.sourceStreamedCurves != 14 || d.sourceConstantCurves != 3 ||
            d.sampleCount != 121 || d.curves == null || d.curves.Length != 17 ||
            d.animators == null || d.animators.Length != 8)
            throw new InvalidDataException("Wrong or incomplete private Stage13 JSON");
        var ids = new HashSet<int>();
        var bound = new HashSet<string>();
        foreach (Curve c in d.curves) {
            if (c == null || string.IsNullOrEmpty(c.path) || c.path.Contains("..") ||
                string.IsNullOrEmpty(c.attribute) || c.samples == null || c.samples.Length != 121 ||
                c.sourceCurveIndex < 0 || c.sourceCurveIndex >= 17 ||
                !ids.Add(c.sourceCurveIndex) || !bound.Add(c.path + "/" + c.attribute) ||
                !(c.attribute.StartsWith("m_LocalPosition.", StringComparison.Ordinal) ||
                  c.attribute.StartsWith("m_LocalRotation.", StringComparison.Ordinal)))
                throw new InvalidDataException("Bad original curve binding");
            foreach (float value in c.samples)
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new InvalidDataException("Non-finite animation sample");
        }
        var targets = new HashSet<int>();
        foreach (Target t in d.animators)
            if (t == null || !targets.Add(t.goId) || t.originalControllerId != 550 ||
                string.IsNullOrEmpty(t.goPath))
                throw new InvalidDataException("Unexpected original OilPump Animator mapping");
    }
}
#endif
