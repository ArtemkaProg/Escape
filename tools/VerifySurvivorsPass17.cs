using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
public static class Verify17
{
 static void Need(bool b,string s){if(!b)throw new Exception(s);}
 static void Capture(Camera c,string name)
 {
  var rt=new RenderTexture(1280,720,24);var t=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=c.targetTexture;var active=RenderTexture.active;float aspect=c.aspect;
  try{c.targetTexture=rt;c.aspect=1280f/720;c.Render();RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,1280,720),0,0);t.Apply();File.WriteAllBytes("Reports/"+name,t.EncodeToPNG());}
  finally{c.targetTexture=old;c.aspect=aspect;RenderTexture.active=active;UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(t);}
 }
 public static async Task<object> Run()
 {
  var game=UnityEngine.Object.FindFirstObjectByType<EscapePlayableZone>();var m=UnityEngine.Object.FindFirstObjectByType<EscapeSurvivorRescue>();var fx=UnityEngine.Object.FindFirstObjectByType<EscapeRocketEffects>();var particles=UnityEngine.Object.FindFirstObjectByType<EscapeLegacyParticles>();
  Need(m!=null && m.people.Length==9,"Nine survivor actors missing");int frame=Time.frameCount;await Task.Delay(300);Need(Time.frameCount>frame,"Frozen Play Mode");
  var camera=game.gameCamera;var cp=camera.transform.position;float size=camera.orthographicSize;
  try
  {
   game.enabled=false;m.enabled=false;fx.enabled=false;particles.enabled=false;game.Restart();m.ResetRescue();
   var stack=camera.GetComponent<EscapeSourceCameraStack>();Need(stack!=null && !stack.perspective.orthographic && Mathf.Abs(stack.sourceFov-45)<.001f,"Source perspective missing");
   float originalOilMax=1;Need(Mathf.Abs(particles.emitters.Single(x=>x.goId==30626).system.main.startSize.constantMax-originalOilMax*.55f)<.001f,"Oil correction missing");
   foreach(int fps in new[]{30,60,144})
   {
    game.Restart();m.ResetRescue();foreach(var person in m.people)person.transform.position+=Vector3.up*100;
    for(int id=0;id<m.people.Length;id++)
    {
     var person=m.people[id];person.gameObject.SetActive(true);person.transform.position=m.probe.transform.TransformPoint(m.probe.center);
     m.Tick(1f/fps,0);Need(person.State==1,"Source attraction sphere did not pick "+person.sourceGo);
     int limit=fps*4;for(int j=0;j<limit && !person.Destroyed;j++)m.Tick(1f/fps,0);
     Need(person.State==3 && person.Destroyed,"Attraction/enter/hide failed "+person.sourceGo);
     Need(m.SavedCount==id+1,"Duplicate or missing rescued event");
    }
    Need(m.SavedCount==9,"Cannot rescue all nine at "+fps);
   }
   game.Restart();m.ResetRescue();Need(m.SavedCount==0 && m.people.All(p=>p.HereAndAlive && p.State==0),"Restart did not restore people");
   game.Seek(10.5f);game.rocket.transform.position=game.rocket.targetReference.position;particles.ResetParticles();for(int i=0;i<180;i++)particles.Tick(1f/60);Capture(camera,"pass17-oil-background.png");
   var person0=m.people[0];person0.transform.position=m.probe.transform.TransformPoint(m.probe.center)+Vector3.right*.2f;
   m.Tick(.01f,8.3f);Need(person0.State==1,"Live pickup not started");
   for(int i=0;i<40;i++)m.Tick(.01f,8.3f);
   Capture(camera,"pass17-rescue-attraction.png");
   var frozen=person0.transform.position;await Task.Delay(150);Need(person0.transform.position==frozen,"Rescue advances while paused");
   var result=new{people=9,fps=new[]{30,60,144},rescuedEachRun=9,sourceDuration=person0.duration,sourceDoorPercent=person0.reach,sourceFov=stack.sourceFov,paused=true};
   File.WriteAllText("Reports/pass17-verification.txt",result.ToString());return result;
  }
  finally{game.Restart();m.ResetRescue();fx.ResetEffects();particles.ResetParticles();game.enabled=true;m.enabled=true;fx.enabled=true;particles.enabled=true;camera.transform.position=cp;camera.orthographicSize=size;}
 }
}
return await Verify17.Run();
