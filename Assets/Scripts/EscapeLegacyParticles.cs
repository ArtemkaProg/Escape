using System;
using UnityEngine;

/// <summary>Unity4 legacy emitter migration. Parameters live in the private scene.
/// The native Unity6 random sequence is not a reproduction of Unity4's sequence.</summary>
[DefaultExecutionOrder(400)]
public sealed class EscapeLegacyParticles : MonoBehaviour
{
    [Serializable] public sealed class Emitter
    {
        public int goId;
        public ParticleSystem system;
        public float sizeGrow;
        [NonSerialized] public bool active;
    }
    public EscapePlayableZone flight;
    public Emitter[] emitters;
    private float previousZone=-1;
    private EscapePlayableZone.FlightState previousState;
    private bool initialized;

    public void ResetParticles()
    {
        foreach(var e in emitters)
        {
            e.system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            e.system.randomSeed=(uint)e.goId;
            e.active=false;
        }
    }
    public void Tick(float dt)
    {
        foreach(var e in emitters)
        {
            bool active=e.system.gameObject.activeInHierarchy;
            if(!active) { e.active=false; continue; }
            if(!e.active)
            {
                e.system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                e.system.Play(false);
                e.system.Pause(false);
            }
            // Explicit simulation prevents particles from advancing during harness pause/death.
            e.system.Simulate(dt,false,false,false);
            e.system.Pause(false);
            e.active=true;
        }
    }
    private void LateUpdate()
    {
        float now=flight.ZoneTime;
        if(!initialized || now<previousZone ||
           (flight.State==EscapePlayableZone.FlightState.Ready && previousState!=flight.State))
        { ResetParticles();initialized=true; }
        float dt=flight.State==EscapePlayableZone.FlightState.Playing ?
            Mathf.Max(0,(now-Mathf.Max(0,previousZone))/flight.timeline.speed) : 0;
        if(dt>0) Tick(dt);
        previousZone=now;previousState=flight.State;
    }
}
