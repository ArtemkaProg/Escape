#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EscapePlayableZonePass12
{
    [Serializable] private class Source
    {
        public string format, unityVersion;
        public int zoneRoot, rocketRoot, rocketTarget, viewpointTarget, boundsCollider;
        public float duration, speed;
        public uint[] layerMasks;
        public Body rigidbody;
        public Node[] nodes;
        public Shape[] colliders;
        public EscapeOriginalTimeline.Track[] tracks;
    }
    [Serializable] private class Node
    {
        public int id, parent, sibling, layer;
        public string name;
    }
    [Serializable] private class Shape
    {
        public int id, goId, kind;
        public bool trigger, enabled, convex, safe;
        public string mesh;
        public float[] size, center;
        public float radius;
    }
    [Serializable] private class Body
    {
        public float mass, drag, angularDrag;
        public bool gravity;
        public int constraints, collisionDetection;
    }
    public const string Output = "Assets/Scenes/Escape_Zone003_PC_PLAYABLE_PASS12_VERIFIED.unity";

    [MenuItem("Escape Recovery/20 - Recover Zone003 PC flight and 3D contacts (pass 12)")]
    public static void Create()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Exit Play Mode and save the active scene before PASS12.");
        var data = JsonUtility.FromJson<Source>(File.ReadAllText("Assets/RecoveredPlayablePass12Local/zone003.json"));
        if (data.format != "ESCPLAY12" || data.unityVersion != "4.6.5f1" ||
            data.tracks.Length != 16 || data.colliders.Length != 284 || data.speed != 1.25f)
            throw new InvalidDataException("Private Zone003 extraction did not pass expected checks");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Escape_Zone003_ROCKET_RECOVERED_PASS11.unity");
        string output = AssetDatabase.GenerateUniqueAssetPath(Output);
        if (!EditorSceneManager.SaveScene(scene, output)) throw new IOException("Cannot save separate PASS12 scene");
        var map = new Dictionary<int, GameObject>();
        var roots = scene.GetRootGameObjects();
        var layers = new Dictionary<int, int>();
        foreach (Node node in data.nodes)
        {
            GameObject go;
            if (node.parent == 0) go = roots.Single(g => g.name == node.name);
            else
            {
                var originalChildren = map[node.parent].transform.Cast<Transform>()
                    .Where(t => !t.name.StartsWith("__EscapeRecovered", StringComparison.Ordinal)).ToArray();
                go = originalChildren[node.sibling].gameObject;
                if (go.name != node.name) throw new InvalidDataException("Original child order differs at " + node.id);
            }
            map.Add(node.id, go);
            layers.Add(node.id, node.layer);
        }
        GameObject rocket = map[data.rocketRoot];
        if (rocket.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("PASS11 already has native colliders; refusing duplicate shapes");
        var shapes = new Dictionary<int, Collider>();
        foreach (Shape source in data.colliders)
        {
            GameObject go = map[source.goId];
            Collider shape;
            switch (source.kind)
            {
                case 64:
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(source.mesh);
                    if (mesh == null) throw new InvalidDataException("Missing private mesh: " + source.mesh);
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mesh;
                    mc.convex = source.convex;
                    shape = mc;
                    break;
                case 65:
                    bool mirrored = false;
                    for (Transform ancestor = go.transform; ancestor != null; ancestor = ancestor.parent)
                    {
                        Vector3 scale = ancestor.localScale;
                        mirrored |= scale.x < 0 || scale.y < 0 || scale.z < 0;
                    }
                    if (mirrored)
                    {
                        // Exact eight corners of the source box; Unity6 PhysX
                        // supports their mirrored transform as a convex mesh.
                        var collisionMesh = new Mesh { name = "Original mirrored box " + source.id };
                        Vector3 size = V(source.size), center = V(source.center);
                        var vertices = new Vector3[8];
                        for (int i = 0; i < 8; i++)
                            vertices[i] = center + Vector3.Scale(size, new Vector3(
                                (i & 1) == 0 ? -.5f : .5f, (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f));
                        collisionMesh.vertices = vertices;
                        collisionMesh.triangles = new[] { 0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5 };
                        collisionMesh.RecalculateNormals(); collisionMesh.RecalculateBounds();
                        AssetDatabase.CreateAsset(collisionMesh, AssetDatabase.GenerateUniqueAssetPath(
                            "Assets/RecoveredPlayablePass12Local/box_" + source.id + ".asset"));
                        var replacement = go.AddComponent<MeshCollider>();
                        replacement.sharedMesh = collisionMesh; replacement.convex = true; shape = replacement;
                        break;
                    }
                    var box = go.AddComponent<BoxCollider>();
                    box.size = V(source.size); box.center = V(source.center); shape = box;
                    break;
                case 135:
                    var sphere = go.AddComponent<SphereCollider>();
                    sphere.radius = source.radius; sphere.center = V(source.center); shape = sphere;
                    break;
                default: throw new InvalidDataException("Unknown source shape");
            }
            shape.isTrigger = source.trigger;
            // The original RuntimeActivatableCollider enables zone shapes near
            // the camera. This isolated active-zone harness keeps them enabled.
            shape.enabled = true;
            shape.excludeLayers = unchecked((int)~data.layerMasks[layers[source.goId]]);
            var identity = go.GetComponent<EscapeOriginalCollisionIdentity>();
            if (identity == null) identity = go.AddComponent<EscapeOriginalCollisionIdentity>();
            identity.sourceId = source.id; identity.safeForRocket = source.safe;
            shapes.Add(source.id, shape);
        }
        Rigidbody body = rocket.AddComponent<Rigidbody>();
        body.mass = data.rigidbody.mass;
        body.linearDamping = data.rigidbody.drag;
        body.angularDamping = data.rigidbody.angularDrag;
        body.useGravity = data.rigidbody.gravity;
        body.constraints = (RigidbodyConstraints)data.rigidbody.constraints;
        body.collisionDetectionMode = (CollisionDetectionMode)data.rigidbody.collisionDetection;
        body.isKinematic = true;

        var timeline = new GameObject("Recovered Zone003 timeline").AddComponent<EscapeOriginalTimeline>();
        timeline.duration = data.duration; timeline.speed = data.speed; timeline.tracks = data.tracks;
        foreach (var t in timeline.tracks)
        {
            t.target = map[t.goId].transform;
            if (t.kind == 5)
            {
                t.pump = t.target.GetComponentInChildren<Animator>(true);
                if (t.pump == null) throw new InvalidDataException("Recovered pump Animator missing: " + t.path);
            }
        }
        var motion = rocket.GetComponent<EscapeRecoveredRocketMotion>();
        motion.managedExternally = true;
        motion.targetReference = map[data.rocketTarget].transform;
        timeline.Evaluate(0, false);
        rocket.transform.position = motion.targetReference.position;
        rocket.transform.rotation = Quaternion.identity;
        var camera = roots.Single(g => g.name == "Stage8 Zone003 diagnostic camera").GetComponent<Camera>();
        camera.transform.position = new Vector3(map[data.viewpointTarget].transform.position.x,
            map[data.viewpointTarget].transform.position.y, camera.transform.position.z);
        var game = new GameObject("Escape PC flight").AddComponent<EscapePlayableZone>();
        game.timeline = timeline; game.rocket = motion; game.rocketBody = body;
        game.rocketBounds = shapes[data.boundsCollider]; game.gameCamera = camera;
        game.viewpointTarget = map[data.viewpointTarget].transform;
        rocket.AddComponent<EscapeRocketContact>().game = game;
        var staleReference = roots.SingleOrDefault(g => g.name.StartsWith("PASS11 stationary target reference", StringComparison.Ordinal));
        if (staleReference != null) UnityEngine.Object.DestroyImmediate(staleReference);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save PASS12");
        Directory.CreateDirectory("Reports");
        File.WriteAllText("Reports/pass12-scene.txt", output + "\nSource shapes: " + shapes.Count +
            "\nRocket shapes: " + rocket.GetComponentsInChildren<Collider>(true).Length +
            "\nTimeline tracks: " + timeline.tracks.Length + "\nSource speed: " + data.speed + "\n");
        Debug.Log("PASS12 created: " + output);
    }

    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode first");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save current scene first");
        EditorSceneManager.OpenScene(Output);
        Directory.CreateDirectory("Builds/Windows-PASS12");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Output }, locationPathName = "Builds/Windows-PASS12/Escape-PASS12.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        File.WriteAllText("Reports/pass12-build.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors + "\n");
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("PASS12 build failed");
    }

    private static Vector3 V(float[] p) { return new Vector3(p[0], p[1], p[2]); }
}
#endif
