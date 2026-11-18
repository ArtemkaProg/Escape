using UnityEngine;

public sealed class EscapeRocketContact : MonoBehaviour
{
    public EscapePlayableZone game;
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider == null) return;
        var identity = collision.collider.GetComponent<EscapeOriginalCollisionIdentity>();
        if (identity != null && identity.safeForRocket) return;
        game.NotifyHit("Collision");
    }
}
