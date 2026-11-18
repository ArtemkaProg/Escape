#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EscapePumpCorrectionPass13
{
    [Serializable] private class Source
    {
        public string format;
        public Node[] nodes;
        public Pump[] pumps;
        public Link[] constraints;
        public Identity[] identities;
    }
    [Serializable] private class Node { public int id, parent, sibling; public string name; }
    [Serializable] private class Pump
    {
        public int goId, animatorGo;
        public string name;
        public float speedPerZoneSecond, startNormalized, clipDuration;
    }
    [Serializable] private class Link { public int sourceId, slaveGo, masterGo; }
    [Serializable] private class Identity { public int sourceId; public bool safe; }
    [Serializable] public class LegacyShape
    {
        public int constraintId, index;
        public Vector3 position, center, extents;
        public Quaternion rotation;
    }
    [Serializable] public class Fixture { public LegacyShape[] shapes; }
    public const string Output = "Assets/Scenes/Escape_Zone003_PC_PUMPS_PASS13.unity";

    [MenuItem("Escape Recovery/21 - Synchronize pump colliders and original clocks (pass 13)")]
    public static void Create()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the active scene and leave Play Mode first");
        var data = JsonUtility.FromJson<Source>(File.ReadAllText("Assets/RecoveredPumpRigsPass13Local/rigs.json"));
        if (data.format != "ESCPUMPRIG13" || data.pumps.Length != 4 || data.constraints.Length != 12)
            throw new InvalidDataException("Unexpected private pump data");
        var scene = EditorSceneManager.OpenScene(EscapePlayableZonePass12.Output);
        string output = AssetDatabase.GenerateUniqueAssetPath(Output);
        if (!EditorSceneManager.SaveScene(scene, output)) throw new IOException("Cannot create separate PASS13");
        var roots = scene.GetRootGameObjects();
        var map = new Dictionary<int, GameObject>();
        foreach (Node n in data.nodes)
        {
            GameObject go = n.parent == 0 ? roots.Single(g => g.name == n.name) :
                map[n.parent].transform.Cast<Transform>().Where(t => !t.name.StartsWith("__EscapeRecovered", StringComparison.Ordinal))
                    .ElementAt(n.sibling).gameObject;
            if (go.name != n.name) throw new InvalidDataException("Original hierarchy mismatch: " + n.id);
            map.Add(n.id, go);
        }
        var timeline = roots.SelectMany(g => g.GetComponentsInChildren<EscapeOriginalTimeline>(true)).Single();
        if (timeline.pumpRig != null) throw new InvalidOperationException("Pump synchronization already exists");
        var rig = timeline.gameObject.AddComponent<EscapePumpRigPlayback>();
        rig.constraints = data.constraints.Select(l => new EscapePumpRigPlayback.Constraint
        { sourceId = l.sourceId, master = map[l.masterGo].transform, slave = map[l.slaveGo].transform }).ToArray();
        // Record old static proxy poses as a PRIVATE counterfactual test fixture.
        var old = new List<LegacyShape>();
        Physics.SyncTransforms();
        foreach (var link in rig.constraints)
        {
            var shapes = link.slave.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < shapes.Length; i++)
                old.Add(new LegacyShape { constraintId = link.sourceId, index = i,
                    position = shapes[i].transform.position, rotation = shapes[i].transform.rotation,
                    center = shapes[i].bounds.center, extents = shapes[i].bounds.extents });
        }
        Directory.CreateDirectory("Reports");
        File.WriteAllText("Reports/pass13-legacy-shapes.json", JsonUtility.ToJson(new Fixture { shapes = old.ToArray() }));
        rig.pumps = data.pumps.Select(p => new EscapePumpRigPlayback.Pump
        {
            goId = p.goId, name = p.name, animator = map[p.animatorGo].GetComponent<Animator>(),
            startNormalized = p.startNormalized, speedPerZoneSecond = p.speedPerZoneSecond, clipDuration = p.clipDuration,
            gate = timeline.tracks.Single(t => t.goId == p.goId && t.kind == 5).channels[0]
        }).ToArray();
        foreach (var pump in rig.pumps)
        {
            if (pump.animator == null || pump.gate.keys.Any(k => k.a != 0 || k.b != 0 || k.c != 0))
                throw new InvalidDataException("Unsupported pump gate or missing recovered Animator");
            pump.animator.enabled = true; pump.animator.Rebind(); pump.animator.Update(0);
        }
        timeline.pumpRig = rig;
        timeline.ResetTimeline();
        var flags = data.identities.ToDictionary(i => i.sourceId, i => i.safe);
        int safeCount = 0;
        foreach (var identity in roots.SelectMany(g => g.GetComponentsInChildren<EscapeOriginalCollisionIdentity>(true)))
        {
            identity.safeForRocket = flags[identity.sourceId];
            if (identity.safeForRocket) safeCount++;
        }
        if (safeCount != 9) throw new InvalidDataException("Safe-tag identities did not match original survivors");
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save PASS13");
        File.WriteAllText("Reports/pass13-scene.txt", output + "\nConstraint links: 12\nPump clocks: 4\nSafe identities: 9\n");
        AssetDatabase.SaveAssets();
    }

    public static void Build()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Leave Play Mode and save scene before building");
        EditorSceneManager.OpenScene(Output);
        Directory.CreateDirectory("Builds/Windows-PASS13");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Output }, locationPathName = "Builds/Windows-PASS13/Escape-PASS13.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        File.WriteAllText("Reports/pass13-build.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors + "\n");
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("PASS13 build failed");
    }
}
#endif
