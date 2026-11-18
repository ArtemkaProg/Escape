using UnityEngine;

// Marker for the original SurvivorCollider reference. AI/life state will own
// HereAndAlive when the survivor state machine is restored.
public sealed class EscapeSurvivorPresence : MonoBehaviour
{
    public GameObject survivor;
    public EscapeRecoveredSurvivor actor;
    public bool HereAndAlive => actor != null ? actor.HereAndAlive : survivor != null && survivor.activeInHierarchy;
}
