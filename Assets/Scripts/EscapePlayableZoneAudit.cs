#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Opt-in integration checks in the built Windows player.</summary>
[DefaultExecutionOrder(200)]
public sealed class EscapePlayableZoneAudit : MonoBehaviour
{
    private EscapePlayableZone game;
    private string output;
    private int phase, frames;
    private float elapsed, pausedTime, lastTime;
    private float expectedVelocity, expectedHeight, previousVelocity;
    private Vector3 pausedPosition;
    private bool done;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-escapeZoneAudit");
        if (i < 0 || i + 1 >= args.Length) return;
        new GameObject("PASS12 player verification").AddComponent<EscapePlayableZoneAudit>().output = args[i + 1];
    }

    private void Start()
    {
        string[] args = Environment.GetCommandLineArgs();
        int fpsArg = Array.IndexOf(args, "-auditFps");
        Application.targetFrameRate = fpsArg >= 0 && fpsArg + 1 < args.Length ? int.Parse(args[fpsArg + 1]) : 60;
        QualitySettings.vSyncCount = 0;
        game = FindFirstObjectByType<EscapePlayableZone>();
        if (game == null) { Finish("FAIL: missing PC flight", 1); return; }
        if (game.State != EscapePlayableZone.FlightState.Ready || game.ZoneTime != 0)
        { Finish("FAIL: initial Ready state", 1); return; }
        game.InvincibleForAudit = true;
        game.ThrustOverride = true;
        game.StartFlight();
    }

    private void LateUpdate()
    {
        if (done || game == null) return;
        frames++; elapsed += Time.deltaTime;
        if (frames > 600) { Finish("FAIL: audit frame timeout", 1); return; }
        switch (phase)
        {
            case 0:
                expectedVelocity += 11.71875f * Time.deltaTime;
                expectedHeight += expectedVelocity * Time.deltaTime;
                float expectedTilt = Mathf.Atan2(previousVelocity, 10) * Mathf.Rad2Deg;
                if (Mathf.Abs(game.ZoneTime - lastTime - Time.deltaTime * 1.25f) > .002f ||
                    Mathf.Abs(game.rocket.transform.position.x - (900 + game.ZoneTime * 5)) > .005f ||
                    Mathf.Abs(game.gameCamera.transform.position.x - game.rocket.targetReference.position.x - 5) > .005f ||
                    Mathf.Abs(game.rocket.VerticalVelocity - expectedVelocity) > .002f ||
                    Mathf.Abs(game.rocket.VerticalPosition - expectedHeight) > .002f ||
                    Mathf.Abs(Mathf.DeltaAngle(game.rocket.transform.eulerAngles.z, expectedTilt)) > .05f)
                { Finish("FAIL: source motion, previous-frame tilt, target route, speed or camera", 1); return; }
                previousVelocity = game.rocket.MeasuredVelocity.y;
                lastTime = game.ZoneTime;
                if (elapsed < .3f) break;
                game.TogglePause();
                pausedTime = game.ZoneTime; pausedPosition = game.rocket.transform.position;
                phase = 1; elapsed = 0;
                break;
            case 1:
                if (game.ZoneTime != pausedTime || Vector3.Distance(game.rocket.transform.position, pausedPosition) > .001f)
                { Finish("FAIL: paused clock/position", 1); return; }
                if (elapsed < .2f) break;
                game.TogglePause();
                if (game.State != EscapePlayableZone.FlightState.Playing)
                { Finish("FAIL: resume", 1); return; }
                // Use a real recovered collider rather than an invented test wall.
                game.Restart(); game.StartFlight(); game.Seek(10);
                game.InvincibleForAudit = false; game.HoldMotionForAudit = true;
                // Reset/seek write Transforms; refresh broadphase bounds once
                // before this same-frame diagnostic query, not every frame.
                Physics.SyncTransforms();
                var hazard = FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).First(c =>
                    !c.isTrigger && c.enabled && c.gameObject.activeInHierarchy &&
                    !c.transform.IsChildOf(game.rocket.transform) &&
                    c.GetComponent<EscapeOriginalCollisionIdentity>() != null &&
                    !c.GetComponent<EscapeOriginalCollisionIdentity>().safeForRocket &&
                    ((int)c.excludeLayers & (1 << 10)) == 0 &&
                    c.bounds.size.x > .2f && c.bounds.size.y > .2f);
                var hull = game.rocket.GetComponentsInChildren<MeshCollider>().First(c => !c.isTrigger);
                Vector3 delta = hull.bounds.center - game.rocket.transform.position;
                game.rocket.transform.position = hazard.bounds.center - delta;
                Physics.SyncTransforms();
                Debug.Log("PASS12 contact test: hazard=" + hazard.name + " layer=" + hazard.gameObject.layer +
                    " hull=" + hull.name + " layer=" + hull.gameObject.layer + " rbKin=" + game.rocketBody.isKinematic +
                    " excluded=" + hull.excludeLayers.value + "/" + hazard.excludeLayers.value +
                    " projectIgnore=" + Physics.GetIgnoreLayerCollision(hull.gameObject.layer, hazard.gameObject.layer) +
                    " hullBounds=" + hull.bounds + " hazardBounds=" + hazard.bounds);
                phase = 2; elapsed = 0;
                break;
            case 2:
                if (game.State == EscapePlayableZone.FlightState.Dead)
                {
                    if (game.LastHit != "Collision") { Finish("FAIL: expected native collision callback", 1); return; }
                    game.HoldMotionForAudit = false; game.InvincibleForAudit = true;
                    game.Restart();
                    if (game.State != EscapePlayableZone.FlightState.Ready || game.rocket.VerticalVelocity != 0 || game.ZoneTime != 0)
                    { Finish("FAIL: restart after collision", 1); return; }
                    game.StartFlight(); game.Seek(game.timeline.duration - .001f);
                    phase = 3; elapsed = 0;
                }
                else if (elapsed > 1)
                    Finish("FAIL: no OnCollisionEnter against recovered geometry", 1);
                break;
            case 3:
                if (game.State != EscapePlayableZone.FlightState.Complete)
                { Finish("FAIL: source timeline completion", 1); return; }
                game.Restart();
                Capture(Path.ChangeExtension(output, ".png"));
                // Inspect the middle of the source route as well as the start.
                game.Seek(15);
                game.rocket.transform.position = game.rocket.targetReference.position;
                Capture(Path.Combine(Path.GetDirectoryName(output), "pass12-player-midroute.png"));
                game.Restart();
                Finish("PASS: source vertical motion/tilt/route/speed/camera, pause/resume, native 3D collision/death, restart, completion, rendered frames; frames=" + frames + "; targetFPS=" + Application.targetFrameRate, 0);
                break;
        }
    }

    private void Capture(string path)
    {
        var camera = game.gameCamera;
        var target = camera.targetTexture; var active = RenderTexture.active;
        var rt = new RenderTexture(1280, 720, 24);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            var pixels = texture.GetPixels32();
            if (!Array.Exists(pixels, c => c.r != pixels[0].r || c.g != pixels[0].g || c.b != pixels[0].b))
                throw new InvalidOperationException("Empty player render");
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = target; RenderTexture.active = active;
            Destroy(rt); Destroy(texture);
        }
    }

    private void Finish(string message, int code)
    {
        done = true; File.WriteAllText(output, message + Environment.NewLine);
        Debug.Log(message); Application.Quit(code);
    }
}
#endif
