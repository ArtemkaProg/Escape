using System;
using System.Linq;
using UnityEngine;

/// <summary>Recovered attraction events and local-zone rescued counter.</summary>
[DefaultExecutionOrder(350)]
public sealed class EscapeSurvivorRescue : MonoBehaviour
{
    [Serializable] public class Track
    {
        public int goId,kind;public string path;public Transform target;public SpriteRenderer sprite;
        public EscapeOriginalTimeline.Channel[] channels;
        public void Apply(float t)
        {
            var c=channels;
            if(kind==1)target.localPosition=new Vector3(c[0].Evaluate(t),c[1].Evaluate(t),c[2].Evaluate(t));
            else if(kind==2)target.localRotation=new Quaternion(c[0].Evaluate(t),c[1].Evaluate(t),c[2].Evaluate(t),c[3].Evaluate(t)).normalized;
            else if(kind==3)target.localScale=new Vector3(c[0].Evaluate(t),c[1].Evaluate(t),c[2].Evaluate(t));
            else if(kind==4)target.gameObject.SetActive(c[0].Evaluate(t)>=.5f);
            else if(kind>=10 && sprite!=null){var color=sprite.color;color[kind-10]=c[0].Evaluate(t);sprite.color=color;}
        }
    }
    [Serializable] public class Clip
    {
        public string name;public float duration;public bool loop;public Track[] tracks;
        public void Apply(float time){float t=duration<=0?0:loop?Mathf.Repeat(time,duration):Mathf.Clamp(time,0,duration);foreach(var x in tracks)x.Apply(t);}
    }
    [Serializable] public class Group
    {
        public int state;public EscapeRecoveredSurvivor[] members;public EscapeOriginalTimeline.Channel gate;
        [NonSerialized] public bool wasOn;
    }
    public EscapePlayableZone flight;public SphereCollider probe,linkProbe;public Transform attractionTarget;
    public EscapeRecoveredSurvivor[] people;public Group[] groups;
    public Transform followMaster,followSlave;public EscapeOriginalTimeline.Channel followGate;
    public int SavedCount{get;private set;}
    private float previous=-1;private bool initialized;
    public void ResetRescue()
    {
        SavedCount=0;foreach(var p in people)p.ResetSurvivor();foreach(var g in groups)g.wasOn=false;
    }
    public void NotifySaved(){SavedCount++;}
    public void Tick(float dt,float zoneTime)
    {
        if(followMaster!=null && followSlave!=null && followGate.Evaluate(zoneTime)>=.5f)
            followSlave.SetPositionAndRotation(followMaster.position,followMaster.rotation);
        foreach(var g in groups)
        {
            bool on=g.gate.Evaluate(zoneTime)>=.5f;
            if(on && !g.wasOn)foreach(var p in g.members)if(p.State==0 && !p.Dead)p.SetFalling(g.state==5);
            g.wasOn=on;
        }
        foreach(var p in people)p.SampleAnimatedMotion(dt);
        Physics.SyncTransforms();
        Vector3 center=probe.transform.TransformPoint(probe.center),scale=probe.transform.lossyScale;
        float radius=probe.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
        foreach(var p in people)
        {
            if(!p.HereAndAlive)continue;
            var c=p.proximityCollider;
            if(p.State==0 && c!=null && c.enabled && (probe.excludeLayers.value&(1<<c.gameObject.layer))==0 &&
                (c.excludeLayers.value&(1<<probe.gameObject.layer))==0 && (c.ClosestPoint(center)-center).sqrMagnitude<=radius*radius)
                p.BeginAttraction();
            p.Tick(dt);
        }
    }
    private void LateUpdate()
    {
        float time=flight.ZoneTime;
        if(!initialized || time<previous || flight.State==EscapePlayableZone.FlightState.Ready && previous>0)
        {ResetRescue();initialized=true;}
        bool play=flight.State==EscapePlayableZone.FlightState.Playing;
        foreach(var p in people)p.SetSimulation(play);
        if(play)Tick(Mathf.Max(0,(time-Mathf.Max(0,previous))/flight.timeline.speed),time);
        previous=time;
    }
    private void OnGUI(){GUI.Label(new Rect(16,45,260,24),"SAVED  "+SavedCount+" / "+people.Length);}
}
