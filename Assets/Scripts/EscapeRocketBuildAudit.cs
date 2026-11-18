#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>Opt-in development player integration check: -escapeRocketAudit.</summary>
[DefaultExecutionOrder(1000)]
public sealed class EscapeRocketBuildAudit : MonoBehaviour
{
    private EscapeRecoveredRocketMotion rocket;
    private float expectedVelocity, expectedY, elapsed, velocityForTilt;
    private int frames, phase;
    private bool complete;
    private string output;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-escapeRocketAudit");
        if (i < 0) return;
        var audit = new GameObject("PASS11 automated player audit").AddComponent<EscapeRocketBuildAudit>();
        audit.output = i + 1 < args.Length ? args[i + 1] : "rocket-player-audit.txt";
    }

    private void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        rocket = FindFirstObjectByType<EscapeRecoveredRocketMotion>();
        if (rocket == null) { Finish("FAIL: missing recovered rocket", 1); return; }
        rocket.ResetFlight();
        rocket.DiagnosticThrustOverride = true;
    }

    private void LateUpdate()
    {
        if (complete || rocket == null || Time.deltaTime <= 0) return;
        float dt = Time.deltaTime;
        float acceleration = phase == 0 ? 11.71875f : -9.375f;
        expectedVelocity += acceleration * dt;
        expectedY += expectedVelocity * dt;
        float angle = Mathf.Atan2(velocityForTilt, 10) * Mathf.Rad2Deg;
        if (Mathf.Abs(rocket.VerticalVelocity - expectedVelocity) > .002f ||
            Mathf.Abs(rocket.VerticalPosition - expectedY) > .002f ||
            Mathf.Abs(Mathf.DeltaAngle(rocket.transform.eulerAngles.z, angle)) > .03f)
        {
            Finish("FAIL: live velocity / position / previous-frame tilt mismatch at frame " + frames, 1);
            return;
        }
        velocityForTilt = rocket.MeasuredVelocity.y;
        frames++;
        elapsed += dt;
        if (elapsed < .6f) return;
        if (phase++ == 0)
        {
            // Continue from rising flight into an unpowered ballistic arc.
            rocket.DiagnosticThrustOverride = false;
            elapsed = 0;
        }
        else
        {
            rocket.ResetFlight();
            rocket.DiagnosticThrustOverride = false;
            if (rocket.VerticalPosition != 0 || rocket.VerticalVelocity != 0 || rocket.MeasuredVelocity != Vector2.zero)
            { Finish("FAIL: reset", 1); return; }
            complete = true;
            StartCoroutine(CaptureAndFinish());
        }
    }

    private IEnumerator CaptureAndFinish()
    {
        // Hold at the reset pose only while capturing the diagnostic image.
        rocket.enabled = false;
        yield return null;
        // Batch players do not draw a system frame buffer. Explicitly render
        // the scene camera so the check also exercises built shaders/textures.
        var camera = Array.Find(FindObjectsByType<Camera>(FindObjectsSortMode.None),
            c => c.name == "Stage8 Zone003 diagnostic camera");
        if (camera == null) { Finish("FAIL: missing scene camera", 1); yield break; }
        var rt = new RenderTexture(1280, 720, 24);
        var screenshot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            screenshot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            screenshot.Apply();
            var pixels = screenshot.GetPixels32();
            bool varied = Array.Exists(pixels, c => c.r != pixels[0].r || c.g != pixels[0].g || c.b != pixels[0].b);
            if (!varied) { Finish("FAIL: empty rendered player image", 1); yield break; }
            File.WriteAllBytes(Path.ChangeExtension(output, ".png"), screenshot.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            Destroy(rt); Destroy(screenshot);
        }
        Finish("PASS: live thrust, coast, integrated position, delayed tilt, reset; frames=" + frames, 0);
    }

    private void Finish(string message, int code)
    {
        complete = true;
        File.WriteAllText(output, message + Environment.NewLine);
        Debug.Log(message);
        Application.Quit(code);
    }
}
#endif
