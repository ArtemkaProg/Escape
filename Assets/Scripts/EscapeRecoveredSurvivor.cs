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
    private Vector3 animatedPosition,animatedVelocity,pausedVelocity,pausedAngularVelocity;
    private float animatedRotation,animatedSpin;private bool motionSample;
    public void ResetSurvivor()
    {
        if(!snapshot){startLocal=transform.localPosition;startRotation=transform.localRotation;snapshot=true;}
        gameObject.SetActive(true);body.isKinematic=true;transform.localPosition=startLocal;transform.localRotation=startRotation;
        State=0;Dead=false;Destroyed=false;falling=false;progress=0;clipTime=0;clipName="Stand";linkPercent=0;
        PlayClip("Stand",0);foreach(var t in linkTracks)t.Apply(0);
        animatedPosition=transform.position;animatedRotation=transform.eulerAngles.z;
        animatedVelocity=pausedVelocity=pausedAngularVelocity=Vector3.zero;animatedSpin=0;motionSample=false;
    }
    public void SampleAnimatedMotion(float dt)
    {
        if(falling || State!=0 || dt<=0)return;
        if(motionSample){animatedVelocity=(transform.position-animatedPosition)/dt;animatedSpin=Mathf.DeltaAngle(animatedRotation,transform.eulerAngles.z)*Mathf.Deg2Rad/dt;}
        animatedPosition=transform.position;animatedRotation=transform.eulerAngles.z;motionSample=true;
    }
    public void SetFalling(bool fall)
    {
        falling=fall;clipName=fall?"Fall":"Stand";clipTime=0;PlayClip(clipName,0);
        body.isKinematic=!fall;
        // Source SurvivorMovementController transfers animated motion when its
        // control changes to Dynamic, including the final pump's upward throw.
        if(fall){body.linearVelocity=animatedVelocity;body.angularVelocity=Vector3.forward*animatedSpin;}
    }
    public void SetSimulation(bool running)
    {
        bool dynamic=running && falling && State==0 && !Dead;
        if(dynamic==!body.isKinematic)return;
        if(!dynamic){pausedVelocity=body.linearVelocity;pausedAngularVelocity=body.angularVelocity;body.isKinematic=true;}
        else{body.isKinematic=false;body.linearVelocity=pausedVelocity;body.angularVelocity=pausedAngularVelocity;}
    }
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
            float r=owner.linkProbe.radius*Mathf.Abs(owner.linkProbe.transform.lossyScale.x);
            float distance=Vector2.Distance(owner.attractionTarget.position,transform.position);
            bool near=distance<=r;
            linkPercent=near?Mathf.Clamp01(1-distance/r):State>0?linkPercent:Mathf.MoveTowards(linkPercent,0,dt/Mathf.Max(.001f,linkCut));
            if(State>0)linkPercent=Mathf.Max(linkPercent,linkLock);
            foreach(var t in linkTracks)t.Apply(linkPercent*linkDuration);
            if(State==1)linkPercent=Mathf.Min(1,linkPercent+dt/Mathf.Max(.001f,linkDuration));
            foreach(var t in linkTracks)t.Apply(linkPercent*linkDuration);
            FitLink();
        }
    }
    public void FitLink()
    {
        if(State>=3 || laser==null)return;
        Vector3 delta=owner.attractionTarget.position-laser.position;delta.z=0;
        Vector3 local=laser.parent!=null?laser.parent.InverseTransformVector(delta):delta;
        if(local.sqrMagnitude<1e-10f)return;
        laser.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(local.y,local.x)*Mathf.Rad2Deg);
        // The source sprite points down Y and its child rotates it 90 degrees.
        // Measure that artwork rather than assuming a unit-length sprite.
        float length=0;
        foreach(var renderer in laser.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if(renderer.sprite==null)continue;
            Matrix4x4 matrix=Matrix4x4.identity;
            for(var t=renderer.transform;t!=laser;t=t.parent)
                matrix=Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale)*matrix;
            foreach(var v in renderer.sprite.vertices)length=Mathf.Max(length,matrix.MultiplyPoint3x4(v).x);
        }
        if(length>1e-6f){var s=laser.localScale;s.x=new Vector2(local.x,local.y).magnitude/length;laser.localScale=s;}
    }
    private void PlayClip(string name,float t){var clip=animations.FirstOrDefault(c=>c.name==name);if(clip!=null)clip.Apply(t);}
}
