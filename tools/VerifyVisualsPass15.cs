using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
public static class VerifyPass15
{
 static void Need(bool value,string msg){if(!value)throw new Exception(msg);}
 static void Capture(Camera c,string name)
 {
  var old=c.targetTexture;var active=RenderTexture.active;float aspect=c.aspect;
  var rt=new RenderTexture(1280,720,24);var png=new Texture2D(1280,720,TextureFormat.RGB24,false);
  try{c.targetTexture=rt;c.aspect=1280f/720;c.Render();RenderTexture.active=rt;png.ReadPixels(new Rect(0,0,1280,720),0,0);png.Apply();File.WriteAllBytes("Reports/"+name,png.EncodeToPNG());}
  finally{c.targetTexture=old;c.aspect=aspect;RenderTexture.active=active;UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(png);}
 }
 static float Hermite(AnimationCurve curve,float t)
 {
  var keys=curve.keys;if(t<=keys[0].time)return keys[0].value;if(t>=keys[keys.Length-1].time)return keys[keys.Length-1].value;
  int index=0;while(index+1<keys.Length && keys[index+1].time<=t)index++;
  var a=keys[index];var b=keys[index+1];float span=b.time-a.time;float u=(t-a.time)/span;
  return (2*u*u*u-3*u*u+1)*a.value+(u*u*u-2*u*u+u)*span*a.outTangent+(-2*u*u*u+3*u*u)*b.value+(u*u*u-u*u)*span*b.inTangent;
 }
 public static async Task<object> Run()
 {
  var game=UnityEngine.Object.FindFirstObjectByType<EscapePlayableZone>();var fx=game.GetComponent<EscapeRocketEffects>();if(fx==null)fx=UnityEngine.Object.FindFirstObjectByType<EscapeRocketEffects>();
  var destruction=game.timeline.destruction;var cam=game.gameCamera;var cameraStart=cam.transform.position;float cameraSize=cam.orthographicSize;
  try
  {
   game.enabled=false;fx.enabled=false;game.Restart();
   Need(destruction.groups.Sum(g=>g.parts.Length)==48,"Missing recorded parts");float maxError=0;
   for(int i=0;i<=120;i++)
   {
    float t=i*.25f;game.timeline.Evaluate(t,false);
    foreach(var g in destruction.groups)
    {
     float elapsed=g.PlaybackTime(t);bool begun=g.gate.keys.Any(k=>k.time<=t && k.d>=.5f);
     foreach(var p in g.parts)
     {
      Vector3 expected=begun?new Vector3(Hermite(p.curves[0],elapsed),Hermite(p.curves[1],elapsed),0):p.initialPosition;
      maxError=Mathf.Max(maxError,Vector3.Distance(expected,p.target.localPosition));
      if(begun){var q=new Quaternion(Hermite(p.curves[2],elapsed),Hermite(p.curves[3],elapsed),Hermite(p.curves[4],elapsed),Hermite(p.curves[5],elapsed)).normalized;Need(Quaternion.Angle(q,p.target.localRotation)<.15f,"Debris rotation interpolation mismatch");}
     }
    }
   }
   Need(maxError<.001f,"Recorded position mismatch "+maxError);
   game.timeline.Evaluate(21.6f,false);int moved=destruction.groups.Sum(g=>g.parts.Count(p=>Vector3.Distance(p.initialPosition,p.target.localPosition)>.1f));Need(moved>=40,"Bridge pieces did not scatter");
   var laserTrack=game.timeline.tracks.Single(t=>t.goId==16824 && t.kind==3);var laser=laserTrack.target.GetComponent<SpriteRenderer>();
   Need(UnityEditor.AssetDatabase.GetAssetPath(laser.sprite)=="Assets/RecoveredVisualsPass15Local/laser_705.png","Wrong active beam sprite");
   float best=0,score=-1;for(int i=0;i<=300;i++){float t=i*.1f;float s=laserTrack.channels[0].Evaluate(t)*laserTrack.channels[1].Evaluate(t);if(s>score){best=t;score=s;}}
   game.timeline.Evaluate(best,false);Need(laser.bounds.size.y>.1f,"Beam is invisible during capture");
   cam.transform.position=new Vector3(laser.bounds.center.x,laser.bounds.center.y,cameraStart.z);cam.orthographicSize=Mathf.Max(4,laser.bounds.extents.y*1.1f);Capture(cam,"pass15-active-laser.png");
   game.timeline.Evaluate(21.6f,false);cam.transform.position=new Vector3(laserTrack.target.position.x,cam.transform.position.y,cameraStart.z);cam.orthographicSize=7;Capture(cam,"pass15-debris.png");
   game.Restart();game.StartFlight();fx.ResetEffects();foreach(var trail in fx.trails)trail.renderer.emitting=true;
   int first=Time.frameCount;for(int i=0;i<30;i++){game.rocket.transform.position+=new Vector3(.16f,.025f,0);fx.Tick(.025f,true);await Task.Delay(25);}
   Need(Time.frameCount>first && fx.trails.All(t=>t.renderer.positionCount>=3),"Native trail vertices not advancing");
   Need(fx.trails[0].renderer.sharedMaterial.GetFloat("_DstBlend")==1 && fx.trails[1].renderer.sharedMaterial.GetFloat("_DstBlend")==10,"Wrong reactor blends");
   Need(UnityEditor.AssetDatabase.GetAssetPath(fx.trails[0].renderer.sharedMaterial.mainTexture).EndsWith("texture_73.png"),"Wrong bright trail texture");
   cam.transform.position=new Vector3(game.rocket.transform.position.x-.2f,game.rocket.transform.position.y,cameraStart.z);cam.orthographicSize=1.3f;
   Capture(cam,"pass15-trails-both.png");fx.trails[1].renderer.enabled=false;Capture(cam,"pass15-trail-bright.png");fx.trails[1].renderer.enabled=true;fx.trails[0].renderer.enabled=false;Capture(cam,"pass15-trail-smoke.png");fx.trails[0].renderer.enabled=true;
   var result=new{parts=48,samplePoses=121,maxPositionError=maxError,scatterMoved=moved,activeLaserTime=best,beamHeight=laser.bounds.size.y,trailVertices=fx.trails.Select(t=>t.renderer.positionCount).ToArray()};
   File.WriteAllText("Reports/pass15-verification.txt","PASS: "+result);return result;
  }
  finally{foreach(var t in fx.trails)t.renderer.enabled=true;game.enabled=true;fx.enabled=true;game.Restart();fx.ResetEffects();cam.transform.position=cameraStart;cam.orthographicSize=cameraSize;}
 }
}
return await VerifyPass15.Run();
