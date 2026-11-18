using UnityEngine;

// Marker for the original SurvivorCollider reference. AI/life state will own
// HereAndAlive when the survivor state machine is restored.
public sealed class EscapeSurvivorPresence : MonoBehaviour
{
    public GameObject survivor;
    public bool HereAndAlive => survivor != null && survivor.activeInHierarchy;
}
