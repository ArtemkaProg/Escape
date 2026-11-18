using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Source-derived RocketChips light, door, fuel-bar and reactor behavior.</summary>
[DefaultExecutionOrder(300)]
public sealed class EscapeRocketEffects : MonoBehaviour
{
    [Serializable] public sealed class Light
    {
        public int goId;
        public SpriteRenderer sprite;
        public float baseEmission, percent, animated;
        public void Apply() { var c=sprite.color; c.a=baseEmission*percent*animated; sprite.color=c; }
    }
    [Serializable] public sealed class Trail
    {
        public TrailRenderer renderer;
        public float time, width, emissionScale, maxEmission, widthMax;
        public bool clampEmission, clampWidth;
        public void Apply(float emission)
        {
            float e=emission*emissionScale;
            if (clampEmission) e=Mathf.Clamp(e,0,maxEmission);
            renderer.time=e*time;
            renderer.startWidth=width*(clampWidth ? Mathf.Clamp(e,0,widthMax) : e);
        }
    }
    [Serializable] public sealed class FuelBar
    {
        public int lightIndex;
        [NonSerialized] public float offTime=-1;
    }
    public EscapePlayableZone flight;
    public Transform doorScale;
    public int doorLightIndex;
    public SphereCollider proximityProbe;
    public EscapeSurvivorPresence[] survivorColliders;
    public Light[] lights;
    public Trail[] trails;
    public FuelBar[] fuelBars;
    public EscapeOriginalTimeline.Channel doorCurve, fuelCurve;
    public float doorDuration, fuelDuration;
    public float emissionBase, emissionInitial, emissionAnimated, emissionMin, emissionMax, rise, fall;
    public float DoorTime { get; private set; }
    public float EmissionScale { get; private set; }
    public int NearbySurvivors { get; private set; }
    private readonly HashSet<GameObject> nearby = new HashSet<GameObject>();
    private float previousZone=-1, effectTime;
    private EscapePlayableZone.FlightState previousState;
    private bool initialized;

    public void ResetEffects()
    {
        effectTime=0; DoorTime=0; NearbySurvivors=0;
        EmissionScale=emissionInitial;
        foreach(var b in fuelBars) b.offTime=-1;
        foreach(var l in lights) { l.percent=1; l.animated=1; }
        foreach(var t in trails) { t.renderer.Clear();t.Apply(emissionBase*EmissionScale*emissionAnimated); }
        ApplyLights();
    }
    // Called by the recovered FuelManager event, never by a fabricated timer.
    public void LaunchFuelEvent(int index)
    {
        if(index<0 || index>=fuelBars.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if(fuelBars[index].offTime<0) fuelBars[index].offTime=effectTime;
    }
    public void Tick(float dt, bool thrust)
    {
        effectTime+=dt;
        nearby.Clear();
        Vector3 center=proximityProbe.transform.TransformPoint(proximityProbe.center);
        Vector3 scale=proximityProbe.transform.lossyScale;
        float radius=proximityProbe.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
        // Query only original SurvivorCollider shapes and honor the source layer matrix.
        // This also handles a survivor already inside the sphere at restart.
        foreach(var s in survivorColliders)
        {
            if(!s.HereAndAlive) continue;
            var c=s.GetComponent<Collider>();
            if(c==null || !c.enabled || !c.gameObject.activeInHierarchy ||
               (proximityProbe.excludeLayers.value & (1<<c.gameObject.layer))!=0 ||
               (c.excludeLayers.value & (1<<proximityProbe.gameObject.layer))!=0) continue;
            Vector3 closest=c.ClosestPoint(center);
            if((closest-center).sqrMagnitude<=radius*radius) nearby.Add(s.survivor);
        }
        NearbySurvivors=nearby.Count;
        DoorTime=Mathf.MoveTowards(DoorTime,NearbySurvivors>0?doorDuration:0,dt);
        float duration=thrust?rise:fall;
        float delta=duration<=0 ? (thrust?emissionMax-emissionMin:emissionMin-emissionMax) :
            dt*(thrust?emissionMax-emissionMin:emissionMin-emissionMax)/duration;
        EmissionScale=Mathf.Clamp(EmissionScale+delta,emissionMin,emissionMax);
        foreach(var t in trails) t.Apply(emissionBase*EmissionScale*emissionAnimated);
        foreach(var b in fuelBars)
            lights[b.lightIndex].percent=b.offTime<0?1:fuelCurve.Evaluate(Mathf.Clamp(effectTime-b.offTime,0,fuelDuration));
        ApplyLights();
    }
    private void ApplyLights()
    {
        float open=doorCurve.Evaluate(Mathf.Clamp(DoorTime,0,doorDuration));
        var s=doorScale.localScale;s.y=open;doorScale.localScale=s;
        lights[doorLightIndex].percent=open;
        // RocketLight.LateUpdate owns final alpha after launch SpriteRenderer clips.
        foreach(var l in lights) l.Apply();
    }
    private void LateUpdate()
    {
        var state=flight.State;
        if(!initialized || flight.ZoneTime<previousZone ||
           (state==EscapePlayableZone.FlightState.Ready && previousState!=state))
        { ResetEffects();initialized=true; }
        float dt=state==EscapePlayableZone.FlightState.Playing ? Mathf.Max(0,(flight.ZoneTime-Mathf.Max(0,previousZone))/flight.timeline.speed) : 0;
        bool thrust=flight.Thrusting;
        // Run after animation and movement. Query transforms at their current pose.
        if(dt>0) { Physics.SyncTransforms(); Tick(dt,thrust); }
        else ApplyLights();
        foreach(var t in trails) t.renderer.emitting=state==EscapePlayableZone.FlightState.Playing;
        previousZone=flight.ZoneTime;previousState=state;
    }
}
