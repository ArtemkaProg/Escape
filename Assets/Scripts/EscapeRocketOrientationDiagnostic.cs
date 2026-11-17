using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Stage 15 readjustment and diagnostic only.
/// The automatic tilt formula is based on the original RocketMovementController
/// IL: atan2(vertical velocity, rotationInertia) * Rad2Deg *
/// angularAmplitudeReduction, rotated about the rocket's forward axis.
/// Numeric values here are placeholders because original serialized values
/// have not yet been restored. This component NEVER generates new sprites.
/// </summary>
[DisallowMultipleComponent]
public sealed class EscapeRocketOrientationDiagnostic : MonoBehaviour
{
    [Header("Diagnostic tilt: formula from original IL, numbers provisional")]
    public bool enableAutomaticTilt = true;
    [Min(0.001f)] public float rotationInertia = 2.2f;
    [Range(0f, 1f)] public float angularAmplitudeReduction = 0.35f;
    [Range(1f, 90f)] public float maxTestAngle = 40f;

    private Quaternion originalRotation;
    private float previousY;
    private float computedVelocity;
    private float currentTilt;
    private bool glassHidden, colliderMasksHidden;
    private readonly List<SpriteRenderer> glassAndGauges = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> colliderMasks = new List<SpriteRenderer>();
    private readonly Dictionary<SpriteRenderer, bool> initiallyEnabled =
        new Dictionary<SpriteRenderer, bool>();

    private bool prevG, prevC, prevT;

    private void Awake()
    {
        originalRotation = transform.localRotation;
        previousY = transform.position.y;
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            initiallyEnabled[sr] = sr.enabled;
            string n = sr.gameObject.name;

            // The original atlas32 carries almost all rocket body, glass and
            // indicator sprites. Isolate auxiliary layers without repainting
            // or overwriting any source PNG/material.
            if (n.StartsWith("RocketLightGlass", StringComparison.Ordinal) ||
                n.StartsWith("RocketFuelGauge", StringComparison.Ordinal))
                glassAndGauges.Add(sr);
            else if (n.EndsWith("_collider", StringComparison.Ordinal))
                colliderMasks.Add(sr);
        }

        Debug.Log("[Escape Rocket diagnostic] SpriteRenderer total: " +
            initiallyEnabled.Count + "; glass/gauge: " + glassAndGauges.Count +
            "; collider-mask sprites: " + colliderMasks.Count +
            ". Original atlas sprite pixel quality is NOT repaired in this pass.");
        foreach (SpriteRenderer sr in glassAndGauges)
        {
            string spriteInfo = sr.sprite == null ? "(missing)" :
                sr.sprite.name + " / " +
                (sr.sprite.texture != null ? sr.sprite.texture.name : "no texture");
            Debug.Log("[Escape Rocket diagnostic] " + sr.gameObject.name +
                " => " + spriteInfo + " | color=" + sr.color +
                " | enabled=" + sr.enabled);
        }
    }

    private void LateUpdate()
    {
        bool reset = KeyDown(KeyCode.R);
        if (KeyDown(KeyCode.G))
        {
            glassHidden = !glassHidden;
            SetVisibility(glassAndGauges, !glassHidden);
        }
        if (KeyDown(KeyCode.C))
        {
            colliderMasksHidden = !colliderMasksHidden;
            SetVisibility(colliderMasks, !colliderMasksHidden);
        }
        if (KeyDown(KeyCode.T))
            enableAutomaticTilt = !enableAutomaticTilt;

        float dt = Time.deltaTime;
        if (reset)
        {
            previousY = transform.position.y;
            computedVelocity = 0f;
            currentTilt = 0f;
            transform.localRotation = originalRotation;
            return;
        }

        if (dt > 0.000001f)
            computedVelocity = Mathf.Clamp(
                (transform.position.y - previousY) / dt, -50f, 50f);
        previousY = transform.position.y;

        float wantedAngle = 0f;
        if (enableAutomaticTilt)
        {
            // The structure (not these tunable numeric values) is confirmed
            // from original RocketMovementController.ComputeWantedRotation.
            wantedAngle = Mathf.Atan2(computedVelocity,
                Mathf.Max(0.001f, rotationInertia)) *
                Mathf.Rad2Deg * angularAmplitudeReduction;
            wantedAngle = Mathf.Clamp(wantedAngle, -maxTestAngle, maxTestAngle);
        }
        currentTilt = wantedAngle;
        transform.localRotation = originalRotation *
            Quaternion.AngleAxis(currentTilt, Vector3.forward);
    }

    private void SetVisibility(List<SpriteRenderer> group, bool visible)
    {
        foreach (SpriteRenderer sr in group)
        {
            if (sr == null) continue;
            bool wasEnabled;
            if (!initiallyEnabled.TryGetValue(sr, out wasEnabled))
                wasEnabled = false;
            sr.enabled = visible && wasEnabled;
        }
    }

    private void OnGUI()
    {
        float width = Mathf.Min(470f, Screen.width - 24f);
        float left = Mathf.Max(12f, Screen.width - width - 12f);
        GUI.Box(new Rect(left, 12, width, 100),
            "Rocket PASS10: orientation & sprite layer audit");
        GUI.Label(new Rect(left + 12, 34, width - 24, 22),
            "T = tilt " + (enableAutomaticTilt ? "ON" : "OFF") +
            "  |  vertical speed " + computedVelocity.ToString("F2") +
            "  |  tilt " + currentTilt.ToString("F1") + " deg");
        GUI.Label(new Rect(left + 12, 55, width - 24, 22),
            "G = glass/gauges " + (glassHidden ? "HIDDEN" : "VISIBLE") +
            "  |  C = collider sprites " +
            (colliderMasksHidden ? "HIDDEN" : "VISIBLE"));
        GUI.Label(new Rect(left + 12, 76, width - 24, 22),
            "Space/W/Up thrust; R reset (existing PASS9 keyboard)");
    }

    private static bool Key(KeyCode key)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(key);
#elif ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        switch (key)
        {
            case KeyCode.R: return Keyboard.current.rKey.isPressed;
            case KeyCode.G: return Keyboard.current.gKey.isPressed;
            case KeyCode.C: return Keyboard.current.cKey.isPressed;
            case KeyCode.T: return Keyboard.current.tKey.isPressed;
            default: return false;
        }
#else
        return false;
#endif
    }

    private bool KeyDown(KeyCode key)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(key);
#else
        bool pressed = Key(key);
        bool before = false;
        switch (key)
        {
            case KeyCode.R: before = prevR; prevR = pressed; break;
            case KeyCode.G: before = prevG; prevG = pressed; break;
            case KeyCode.C: before = prevC; prevC = pressed; break;
            case KeyCode.T: before = prevT; prevT = pressed; break;
        }
        return pressed && !before;
#endif
    }
}
