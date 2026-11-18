using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PC recovery harness around source-derived movement, timeline and contacts.
/// Ready/pause/retry/completion UI is authored for this standalone zone preview.
/// Original menu, launch state machine, fuel and survivor progression remain separate work.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class EscapePlayableZone : MonoBehaviour
{
    public enum FlightState { Ready, Playing, Paused, Dead, Complete }
    public EscapeOriginalTimeline timeline;
    public EscapeRecoveredRocketMotion rocket;
    public Rigidbody rocketBody;
    public Collider rocketBounds;
    public Camera gameCamera;
    public Transform viewpointTarget;
    public FlightState State { get; private set; }
    public float ZoneTime { get; private set; }
    public int DeathCount { get; private set; }
    public string LastHit { get; private set; }
    public bool? ThrustOverride { get; set; }
    public bool HoldMotionForAudit { get; set; }
    public bool InvincibleForAudit { get; set; }
    private FlightState beforePause;
    private Vector3 cameraStart;
    private GUIStyle titleStyle, bodyStyle;

    private void Start()
    {
        cameraStart = gameCamera.transform.position;
        Restart();
    }

    public void Restart()
    {
        rocketBody.isKinematic = true;
        ZoneTime = 0;
        timeline.ResetTimeline();
        rocket.ResetFlight();
        LastHit = "";
        State = FlightState.Ready;
        FollowCamera();
    }

    public void StartFlight()
    {
        if (State != FlightState.Ready) return;
        rocketBody.isKinematic = false;
        rocketBody.linearVelocity = Vector3.zero;
        rocketBody.angularVelocity = Vector3.zero;
        State = FlightState.Playing;
    }

    public void TogglePause()
    {
        if (State == FlightState.Paused) State = beforePause;
        else if (State == FlightState.Playing)
        { beforePause = State; State = FlightState.Paused; }
        rocketBody.isKinematic = State != FlightState.Playing;
        timeline.Evaluate(ZoneTime, State == FlightState.Playing);
    }

    public void NotifyHit(string reason)
    {
        if (State != FlightState.Playing || InvincibleForAudit) return;
        State = FlightState.Dead;
        LastHit = reason;
        DeathCount++;
        rocketBody.isKinematic = true;
        timeline.Evaluate(ZoneTime, false);
    }

    public void Seek(float time)
    {
        ZoneTime = Mathf.Clamp(time, 0, timeline.duration);
        timeline.Evaluate(ZoneTime, State == FlightState.Playing);
        FollowCamera();
    }

    private void Update()
    {
        if (RestartPressed()) Restart();
        if (PausePressed()) TogglePause();
        if (State == FlightState.Ready && (ThrustOverride ?? ThrustHeld())) StartFlight();
    }

    private void LateUpdate()
    {
        if (State != FlightState.Playing || Time.deltaTime <= 0) return;
        float dt = Time.deltaTime;
        ZoneTime = Mathf.Min(ZoneTime + dt * timeline.speed, timeline.duration);
        timeline.Evaluate(ZoneTime, true);
        if (!HoldMotionForAudit) rocket.Advance(dt, ThrustOverride ?? ThrustHeld());
        FollowCamera();
        Bounds b = rocketBounds.bounds;
        float distance = gameCamera.WorldToViewportPoint(rocketBounds.transform.position).z;
        Vector3 min = gameCamera.ViewportToWorldPoint(new Vector3(0, 0, distance));
        Vector3 max = gameCamera.ViewportToWorldPoint(new Vector3(1, 1, distance));
        if (!HoldMotionForAudit && (b.max.x < min.x || b.min.x > max.x || b.max.y < min.y || b.min.y > max.y))
            NotifyHit("Out of screen");
        if (State == FlightState.Playing && ZoneTime >= timeline.duration)
        {
            State = FlightState.Complete;
            rocketBody.isKinematic = true;
            timeline.Evaluate(ZoneTime, false);
        }
    }

    private void FollowCamera()
    {
        gameCamera.transform.position = new Vector3(viewpointTarget.position.x,
            viewpointTarget.position.y, cameraStart.z);
    }

    private void OnGUI()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        }
        GUI.Label(new Rect(16, 12, Screen.width - 32, 30),
            "ESCAPE   |   " + (ZoneTime / timeline.duration * 100).ToString("F0") + "%   |   R: restart   Esc: pause", bodyStyle);
        if (State == FlightState.Playing) return;
        string title = State == FlightState.Ready ? "ESCAPE" :
            State == FlightState.Dead ? "TRY AGAIN" : State == FlightState.Complete ? "ZONE COMPLETE" : "PAUSED";
        string hint = State == FlightState.Ready ? "Hold Space / W / Up to fly" :
            State == FlightState.Paused ? "Esc to resume" : "Press R to restart";
        float y = Screen.height * .36f;
        GUI.Box(new Rect(Screen.width * .25f, y, Screen.width * .5f, 120), "");
        GUI.Label(new Rect(0, y + 12, Screen.width, 48), title, titleStyle);
        GUI.Label(new Rect(0, y + 67, Screen.width, 35), hint, bodyStyle);
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
    private static bool RestartPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.R);
#elif ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return false;
#endif
    }
    private static bool PausePressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return false;
#endif
    }
}
