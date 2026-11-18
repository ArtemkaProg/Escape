using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Independently authored port of the verified movement equations.
/// Settings come from the private level0 component, not constructor defaults.
/// The stationary reference replaces the unported RocketTarget/zone progression.
/// Fuel, damage, launch transitions and target scrolling remain unported.
/// </summary>
[DisallowMultipleComponent]
public sealed class EscapeRecoveredRocketMotion : MonoBehaviour
{
    public float thrustStrength, gravity, physicScale;
    public float angularAmplitudeReduction, rotationInertia;
    public Animator visualAnimator;
    public Transform targetReference;
    public float VerticalVelocity { get; private set; }
    public Vector2 MeasuredVelocity { get; private set; }
    public float VerticalPosition { get; private set; }
    public bool? DiagnosticThrustOverride { get; set; }

    private Vector3 startPosition, previousPosition;
    private Quaternion startRotation;
    private bool engineStarted;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        ResetFlight();
    }

    public void ResetFlight()
    {
        transform.SetPositionAndRotation(startPosition, startRotation);
        previousPosition = startPosition;
        VerticalPosition = 0;
        VerticalVelocity = 0;
        MeasuredVelocity = Vector2.zero;
        engineStarted = false;
        if (visualAnimator != null)
        {
            visualAnimator.enabled = true;
            visualAnimator.Rebind();
            visualAnimator.Update(0);
            visualAnimator.enabled = false;
        }
    }

    private void Update()
    {
        if (ResetPressed()) ResetFlight();
        if (visualAnimator == null) return;
        if (ClipPressed(1)) PlayVisual("TryingToStartTheEngine");
        if (ClipPressed(2)) PlayVisual("StartEngine");
        if (ClipPressed(3)) PlayVisual("TryingToTakeOff");
    }

    private void PlayVisual(string state)
    {
        visualAnimator.enabled = true;
        visualAnimator.Play(state, 0, 0);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        bool thrust = DiagnosticThrustOverride ?? ThrustHeld();
        if (thrust && !engineStarted)
        {
            engineStarted = true;
            if (visualAnimator != null) PlayVisual("StartEngine");
        }

        // Same separate force integrations and update order as the source IL.
        // Orientation uses last frame's measured world velocity.
        VerticalVelocity -= gravity * physicScale * dt;
        if (thrust) VerticalVelocity += thrustStrength * physicScale * dt;
        float wanted = Mathf.Atan2(MeasuredVelocity.y, rotationInertia) *
            Mathf.Rad2Deg * angularAmplitudeReduction;
        Vector3 angles = transform.eulerAngles;
        angles.z = wanted;
        transform.eulerAngles = angles;

        VerticalPosition += VerticalVelocity * dt;
        Vector3 local = targetReference.InverseTransformPoint(transform.position);
        local.x = 0;
        local.y = VerticalPosition;
        Vector3 world = targetReference.TransformPoint(local);
        // Source Position is Vector2 and thus discards z.
        transform.position = new Vector3(world.x, world.y, 0);
        MeasuredVelocity = (Vector2)(transform.position - previousPosition) / dt;
        previousPosition = transform.position;
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(12, 12, 640, 88), "Escape PASS11 — recovered art and movement / diagnostic");
        GUI.Label(new Rect(24, 36, 620, 22), "Space / W / Up: thrust | R: reset | 1 / 2 / 3: original visual clips");
        GUI.Label(new Rect(24, 59, 620, 30), "Vertical speed: " + VerticalVelocity.ToString("F2") +
            " | Fuel, collisions and zone scrolling pending");
    }

    private static bool ThrustHeld()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
#elif ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && (k.spaceKey.isPressed || k.wKey.isPressed || k.upArrowKey.isPressed);
#else
        return false;
#endif
    }
    private static bool ResetPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.R);
#elif ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return false;
#endif
    }
    private static bool ClipPressed(int number)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + number - 1));
#elif ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && (number == 1 ? k.digit1Key.wasPressedThisFrame :
            number == 2 ? k.digit2Key.wasPressedThisFrame : k.digit3Key.wasPressedThisFrame);
#else
        return false;
#endif
    }
}
