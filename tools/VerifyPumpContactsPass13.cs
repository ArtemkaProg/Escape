using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public static class VerifyPumpsPass13
{
    [Serializable] public class Case
    {
        public float time;
        public int constraintId, index;
        public Vector3 clearPosition, hitPosition, legacyPosition;
        public Quaternion legacyRotation;
    }
    private static bool Hit(Collider hull, Collider obstacle, Vector3 position, Quaternion rotation)
    {
        if (hull.isTrigger || obstacle.isTrigger || !hull.enabled || !obstacle.enabled ||
            !hull.gameObject.activeInHierarchy || !obstacle.gameObject.activeInHierarchy ||
            (hull.excludeLayers.value & (1 << obstacle.gameObject.layer)) != 0 ||
            (obstacle.excludeLayers.value & (1 << hull.gameObject.layer)) != 0) return false;
        return Physics.ComputePenetration(hull, hull.transform.position, hull.transform.rotation,
            obstacle, position, rotation, out var direction, out var distance) && distance > .0001f;
    }

    public static object Geometry()
    {
        var game = UnityEngine.Object.FindFirstObjectByType<EscapePlayableZone>();
        var rig = game.timeline.pumpRig;
        Vector3 start = game.rocket.transform.position;
        float maxPosition = 0, maxAngle = 0;
        var sourcePhases = new[] { .445f, .445f, .39f, .44f };
        var factors = new[] { .5f, 1.25f, 1f, .17f };
        var gates = new[] { .8666666746f, 19.9833335876f, 0f, .4833333194f };
        try
        {
            for (int i = 0; i <= 120; i++)
            {
                float t = i * .25f;
                game.timeline.Evaluate(t, false);
                for (int j = 0; j < rig.pumps.Length; j++)
                {
                    var p = rig.pumps[j];
                    float expected = sourcePhases[j] + Mathf.Max(0, t - gates[j]) * factors[j] / 2;
                    if (Mathf.Abs(p.NormalizedTime - expected) > .0001f)
                        throw new Exception("Source clock mismatch for " + p.name);
                    float actual = p.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    if (Mathf.Abs(Mathf.DeltaAngle(actual * 360, expected * 360)) > .05f)
                        throw new Exception("Animator pose phase mismatch for " + p.name + ": " + actual + " vs " + expected);
                }
                foreach (var link in rig.constraints)
                {
                    maxPosition = Mathf.Max(maxPosition, Vector3.Distance(link.master.position, link.slave.position));
                    maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(link.master.rotation, link.slave.rotation));
                }
            }
            if (maxPosition > .0002f || maxAngle > .06f) throw new Exception("Collider proxy drift");
            var fixture = JsonUtility.FromJson<EscapePumpCorrectionPass13.Fixture>(File.ReadAllText("Reports/pass13-legacy-shapes.json"));
            var hulls = game.rocket.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger && c.enabled).ToArray();
            var anchor = hulls.OfType<MeshCollider>().First();
            Vector3 hullOffset = anchor.transform.TransformPoint(anchor.sharedMesh.bounds.center) - start;
            var pumpShapes = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c => !c.transform.IsChildOf(game.rocket.transform) &&
                    !(c.GetComponent<EscapeOriginalCollisionIdentity>()?.safeForRocket ?? false)).ToArray();
            Case found = null;
            foreach (float t in Enumerable.Range(0,121).Select(i => i * .25f))
            {
                game.timeline.Evaluate(t, false);
                foreach (var old in fixture.shapes)
                {
                    var link = rig.constraints.Single(l => l.sourceId == old.constraintId);
                    var obstacle = link.slave.GetComponentsInChildren<Collider>(true)[old.index];
                    foreach (float dx in new[] { 0f, -.6f, .6f })
                    foreach (float dy in new[] { 0f, -.6f, .6f })
                    {
                        Vector3 candidate = old.center + Vector3.Scale(old.extents, new Vector3(dx,dy,0)) - hullOffset;
                        candidate.x = game.rocket.targetReference.position.x;
                        candidate.z = 0;
                        if (Mathf.Abs(candidate.y) > 4.5f) continue;
                        game.rocket.transform.position = candidate;
                        bool staleHit = hulls.Any(c => Hit(c, obstacle, old.position, old.rotation));
                        bool actualHit = pumpShapes.Any(o => hulls.Any(c => Hit(c, o, o.transform.position, o.transform.rotation)));
                        if (!staleHit || actualHit) continue;
                        // Choose a real new-pose solid overlap as the positive control.
                        Vector3 center = obstacle is BoxCollider box ? obstacle.transform.TransformPoint(box.center) :
                            obstacle.transform.TransformPoint(((MeshCollider)obstacle).sharedMesh.bounds.center);
                        found = new Case { time=t, constraintId=old.constraintId,index=old.index,
                            clearPosition=candidate, hitPosition=new Vector3(center.x-hullOffset.x,center.y-hullOffset.y,0),
                            legacyPosition=old.position,legacyRotation=old.rotation };
                        break;
                    }
                    if (found != null) break;
                }
                if (found != null) break;
            }
            if (found == null) throw new Exception("No counterfactual stale-hit / clear-new-pose case found");
            File.WriteAllText("Reports/pass13-regression-case.json", JsonUtility.ToJson(found,true));
            var result = new { poses=121, links=rig.constraints.Length, maxPositionError=maxPosition,
                maxAngleError=maxAngle, staleHit=true, correctedClear=true, sourceTime=found.time,
                safeIdentities=UnityEngine.Object.FindObjectsByType<EscapeOriginalCollisionIdentity>(FindObjectsSortMode.None).Count(c=>c.safeForRocket) };
            File.WriteAllText("Reports/pass13-geometry.txt", "PASS: " + result);
            return result;
        }
        finally { game.rocket.transform.position=start; game.timeline.Evaluate(0,false); }
    }

    public static async Task<object> LiveContacts()
    {
        var game = UnityEngine.Object.FindFirstObjectByType<EscapePlayableZone>();
        var test = JsonUtility.FromJson<Case>(File.ReadAllText("Reports/pass13-regression-case.json"));
        try
        {
            game.Restart();game.StartFlight();game.enabled=false;
            game.timeline.Evaluate(test.time,false);
            game.rocket.transform.position=test.clearPosition;
            Physics.SyncTransforms();
            int firstFrame=Time.frameCount;
            await Task.Delay(350);
            if (Time.frameCount <= firstFrame || game.State != EscapePlayableZone.FlightState.Playing)
                throw new Exception("Clearance: state=" + game.State + ", hit=" + game.LastHit +
                    ", frames=" + firstFrame + "->" + Time.frameCount);
            game.rocket.transform.position=test.hitPosition;
            Physics.SyncTransforms();
            await Task.Delay(350);
            if (game.State != EscapePlayableZone.FlightState.Dead || game.LastHit != "Collision")
                throw new Exception("Real overlap failed to produce original collision/death");
            File.WriteAllText("Reports/pass13-live-contact.txt","PASS: stale-hit position stays alive; true pump overlap causes Collision death; frames advanced\n");
            return new {clearanceSurvived=true,realPumpHitCausedDeath=true,firstFrame,lastFrame=Time.frameCount};
        }
        finally { game.enabled=true;game.Restart(); }
    }
}
