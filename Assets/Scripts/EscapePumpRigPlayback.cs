using System;
using UnityEngine;

/// <summary>
/// Source-derived pump clocks and AnimationConstraint_PositionRotation links.
/// Manual sampling keeps seek, pause, reset and collision proxies on one clock.
/// </summary>
public sealed class EscapePumpRigPlayback : MonoBehaviour
{
    [Serializable] public sealed class Pump
    {
        public int goId;
        public string name;
        public Animator animator;
        public float startNormalized, speedPerZoneSecond, clipDuration;
        public EscapeOriginalTimeline.Channel gate;
        public float NormalizedTime { get; private set; }

        public void Sample(float zoneTime)
        {
            float played = 0;
            var keys = gate.keys;
            for (int i = 0; i < keys.Length && keys[i].time < zoneTime; i++)
            {
                float end = i + 1 < keys.Length ? Mathf.Min(zoneTime, keys[i + 1].time) : zoneTime;
                if (keys[i].d >= .5f) played += Mathf.Max(0, end - keys[i].time);
            }
            // DLL: parent Animator speed * value * speedScale. ZoneTime already
            // includes that parent speed. StartTime is normalized, not seconds.
            NormalizedTime = startNormalized + played * speedPerZoneSecond / clipDuration;
            animator.enabled = true;
            animator.speed = 0;
            animator.Play(0, 0, Mathf.Repeat(NormalizedTime, 1));
            animator.Update(0);
        }
    }

    [Serializable] public sealed class Constraint
    {
        public int sourceId;
        public Transform master, slave;
    }

    public Pump[] pumps;
    public Constraint[] constraints;

    public void Evaluate(float zoneTime)
    {
        foreach (Pump pump in pumps) pump.Sample(zoneTime);
        foreach (Constraint link in constraints)
        {
            // Original DLL copies WORLD position and rotation, preserving scale.
            if (link.master != null && link.slave != null)
                link.slave.SetPositionAndRotation(link.master.position, link.master.rotation);
        }
    }
}
