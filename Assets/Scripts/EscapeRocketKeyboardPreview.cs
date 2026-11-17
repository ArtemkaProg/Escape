using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Independently authored demonstration of keyboard thrust. The original APK
/// confirms a thrust button and vertical velocity += acceleration * deltaTime;
/// these numeric parameters are deliberately provisional, not original values.
/// </summary>
public sealed class EscapeRocketKeyboardPreview : MonoBehaviour
{
    [Header("Diagnostic physics (NOT original tuned values)")]
    public float gravity = 2.8f;
    public float thrustAcceleration = 6.7f;
    public float maxRiseSpeed = 5f;
    public float maxFallSpeed = 3f;
    public float maxRiseFromStart = 8f;
    public Animator visualAnimator;

    private Vector3 startPosition;
    private float verticalVelocity;
    private bool engineStarted;
    private string stateLabel = "Ready";
    private GUIStyle labelStyle;

    private void Awake()
    {
        startPosition = transform.position;
        if (visualAnimator != null) visualAnimator.enabled = false;
    }

    private void Update()
    {
        if (KeyDown(KeyCode.R))
        {
            transform.position = startPosition;
            verticalVelocity = 0f;
            engineStarted = false;
            stateLabel = "Reset";
            if (visualAnimator != null) visualAnimator.enabled = false;
        }

        if (KeyDown(KeyCode.Alpha1)) PlayState("TryingToStartTheEngine");
        if (KeyDown(KeyCode.Alpha2))
        {
            PlayState("StartEngine");
            engineStarted = true;
        }
        if (KeyDown(KeyCode.Alpha3)) PlayState("TryingToTakeOff");
        if (KeyDown(KeyCode.Alpha4))
            stateLabel = "TakingOff: original particle/script channels not ported";

        bool thrust = Key(KeyCode.Space) || Key(KeyCode.W) || Key(KeyCode.UpArrow);
        if (thrust && !engineStarted)
        {
            engineStarted = true;
            PlayState("StartEngine");
        }

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        verticalVelocity = Mathf.Clamp(verticalVelocity +
            ((thrust ? thrustAcceleration : 0f) - gravity) * dt,
            -maxFallSpeed, maxRiseSpeed);
        Vector3 p = transform.position;
        p.y = Mathf.Clamp(p.y + verticalVelocity * dt,
            startPosition.y, startPosition.y + maxRiseFromStart);
        if ((p.y <= startPosition.y && verticalVelocity < 0f) ||
            (p.y >= startPosition.y + maxRiseFromStart && verticalVelocity > 0f))
            verticalVelocity = 0f;
        transform.position = p;
    }

    private void PlayState(string name)
    {
        if (visualAnimator == null) { stateLabel = "No Animator"; return; }
        visualAnimator.enabled = true;
        visualAnimator.Play(name, 0, 0f);
        stateLabel = name;
    }

    private void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 16;
            labelStyle.normal.textColor = Color.white;
        }
        GUI.Box(new Rect(12, 10, 600, 104),
            "Escape Rocket — Stage 14 (diagnostic, not original gameplay)");
        GUI.Label(new Rect(25, 38, 570, 20),
            "Hold Space / W / Up = thrust  |  R = reset", labelStyle);
        GUI.Label(new Rect(25, 60, 570, 20),
            "1 = try start  |  2 = engine  |  3 = try takeoff  |  4 = takeoff audit",
            labelStyle);
        GUI.Label(new Rect(25, 82, 570, 20),
            "Visual state: " + stateLabel + " | y velocity: " +
            verticalVelocity.ToString("F2"), labelStyle);
    }

    private static bool Key(KeyCode key)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(key);
#elif ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        switch (key)
        {
            case KeyCode.Space: return Keyboard.current.spaceKey.isPressed;
            case KeyCode.W: return Keyboard.current.wKey.isPressed;
            case KeyCode.UpArrow: return Keyboard.current.upArrowKey.isPressed;
            case KeyCode.R: return Keyboard.current.rKey.isPressed;
            case KeyCode.Alpha1: return Keyboard.current.digit1Key.isPressed;
            case KeyCode.Alpha2: return Keyboard.current.digit2Key.isPressed;
            case KeyCode.Alpha3: return Keyboard.current.digit3Key.isPressed;
            case KeyCode.Alpha4: return Keyboard.current.digit4Key.isPressed;
            default: return false;
        }
#else
        return false;
#endif
    }

    private bool prev1, prev2, prev3, prev4, prevR;

    private bool KeyDown(KeyCode key)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(key);
#else
        bool down = Key(key);
        bool was;
        switch (key)
        {
            case KeyCode.Alpha1: was = prev1; prev1 = down; break;
            case KeyCode.Alpha2: was = prev2; prev2 = down; break;
            case KeyCode.Alpha3: was = prev3; prev3 = down; break;
            case KeyCode.Alpha4: was = prev4; prev4 = down; break;
            case KeyCode.R: was = prevR; prevR = down; break;
            default: was = false; break;
        }
        return down && !was;
#endif
    }
}
