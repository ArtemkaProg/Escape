using UnityEngine;

/// <summary>Preserves the original safe-tag decision without changing project tags.</summary>
public sealed class EscapeOriginalCollisionIdentity : MonoBehaviour
{
    public int sourceId;
    public bool safeForRocket;
}
