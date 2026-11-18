using System;
using System.Linq;
using UnityEngine;

/// <summary>Source attraction timing, linear/SmoothDamp position, spin and entering clips.</summary>
public sealed class EscapeRecoveredSurvivor : MonoBehaviour
{
    public int sourceGo;public EscapeSurvivorRescue owner;public Collider proximityCollider;public Rigidbody body;
    public float duration,smooth,minSpin,maxSpin,reach,hideDelay,linkDuration,linkLock,linkCut;public int mode;
    public Transform laser,linkRoot;public EscapeSurvivorRescue.Clip[] animations;public EscapeSurvivorRescue.Track[] linkTracks;
    public int State{get;private set;} // source attracted enum: Idle / Attracted / Entering / In
    public bool Dead{get;private set;}public bool Destroyed{get;private set;}
    public bool HereAndAlive=>!Dead && !Destroyed && gameObject.activeInHierarchy;
    private Vector3 startLocal;private Quaternion startRotation;private Vector2 initial,smoothVelocity;
    private float progress,angularSpeed,enteringTime,inTime,clipTime,linkPercent;private bool snapshot,falling;
    private string clipName;
    public void ResetSurvivor()
    {
        if(!snapshot){startLocal=transform.localPosition;startRotation=transform.localRotation;snapshot=true;}
        gameObject.SetActive(true);body.isKinematic=true;transform.localPosition=startLocal;transform.localRotation=startRotation;
        State=0;Dead=false;Destroyed=false;falling=false;progress=0;clipTime=0;clipName="Stand";linkPercent=0;
        PlayClip("Stand",0);foreach(var t in linkTracks)t.Apply(0);
    }
    public void SetFalling(bool fall)
    {
        falling=fall;clipName=fall?"Fall":"Stand";clipTime=0;PlayClip(clipName,0);
        body.isKinematic=!fall;
        if(fall){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
    }
    public void SetSimulation(bool running){body.isKinematic=!(running && falling && State==0 && !Dead);}
    public void BeginAttraction()
    {
        if(State!=0 || !HereAndAlive)return;
        initial=transform.position;State=1;progress=0;smoothVelocity=Vector2.zero;body.isKinematic=true;
        var rng=new System.Random(sourceGo);angularSpeed=(float)(minSpin+(maxSpin-minSpin)*rng.NextDouble())*(rng.Next(2)==0?1:-1);
        clipName="Attracted";clipTime=0;PlayClip(clipName,0);
    }
    public void Tick(float dt)
    {
        clipTime+=dt;
        if(State==1 || State==2)
        {
            progress+=dt/duration;Vector2 target=owner.attractionTarget.position;
            Vector2 next=mode==0?Vector2.Lerp(initial,target,progress):Vector2.SmoothDamp(transform.position,target,ref smoothVelocity,(1-progress)*smooth,Mathf.Infinity,dt);
            transform.position=new Vector3(next.x,next.y,0);transform.Rotate(0,0,angularSpeed*dt,Space.World);
            if(State==1 && progress>=reach){State=2;enteringTime=0;clipName="Entering";clipTime=0;}
            if(State==2)enteringTime+=dt;
            if(progress>=1){State=3;inTime=0;owner.NotifySaved();}
        }
        else if(State==3)
        {
            transform.position=owner.attractionTarget.position;transform.Rotate(0,0,angularSpeed*dt,Space.World);inTime+=dt;
            if(inTime>=hideDelay){Destroyed=true;gameObject.SetActive(false);return;}
        }
        PlayClip(clipName,clipTime);
        // Source link targets the door and scales along local X. Progression
        // artwork comes from the original clip, not a newly drawn line.
        if(State<3)
        {
            Vector2 delta=owner.attractionTarget.position-laser.position;
            float r=owner.linkProbe.radius*Mathf.Abs(owner.linkProbe.transform.lossyScale.x);
            float distance=Vector2.Distance(owner.attractionTarget.position,transform.position);
            bool near=distance<=r;
            linkPercent=near?Mathf.Clamp01(1-distance/r):State>0?linkPercent:Mathf.MoveTowards(linkPercent,0,dt/Mathf.Max(.001f,linkCut));
            if(State>0)linkPercent=Mathf.Max(linkPercent,linkLock);
            foreach(var t in linkTracks)t.Apply(linkPercent*linkDuration);
            if(delta.sqrMagnitude>0)laser.right=delta.normalized;
            var s=laser.localScale;s.x=delta.magnitude;laser.localScale=s;
            if(State==1)linkPercent=Mathf.Min(1,linkPercent+dt/Mathf.Max(.001f,linkDuration));
            foreach(var t in linkTracks)t.Apply(linkPercent*linkDuration);
        }
    }
    private void PlayClip(string name,float t){var clip=animations.FirstOrDefault(c=>c.name==name);if(clip!=null)clip.Apply(t);}
}
