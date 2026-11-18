using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
public static class Verify18
{
 static void Need(bool b,string s){if(!b)throw new Exception(s);}
 public static void Capture(Camera c,string name)
 {
  var s=c.GetComponent<EscapeSourceCameraStack>();var rt=new RenderTexture(960,540,24);var t=new Texture2D(960,540,TextureFormat.RGB24,false);var old=c.targetTexture;var active=RenderTexture.active;float aspect=c.aspect;
  try{c.targetTexture=rt;c.aspect=16f/9;s.Synchronize();s.sky.Render();s.perspective.Render();c.Render();RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,960,540),0,0);t.Apply();File.WriteAllBytes("Reports/"+name,t.EncodeToPNG());}
  finally{c.targetTexture=old;c.aspect=aspect;s.Synchronize();RenderTexture.active=active;UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(t);}
 }
 public static async Task<object> Run()
 {
  var g=UnityEngine.Object.FindFirstObjectByType<EscapePlayableZone>();var m=UnityEngine.Object.FindFirstObjectByType<EscapeSurvivorRescue>();var fx=g.GetComponent<EscapeRocketEffects>();var particles=g.GetComponent<EscapeLegacyParticles>();
  int frame=Time.frameCount;await Task.Delay(300);Need(Time.frameCount>frame,"Frozen play loop");
  g.enabled=m.enabled=fx.enabled=particles.enabled=false;
  var report=new System.Text.StringBuilder();
  try
  {
   foreach(int fps in new[]{30,60,144})
   {
    g.Restart();m.ResetRescue();var last=m.people.Single(p=>p.sourceGo==27181);bool launched=false;Vector3 velocity=Vector3.zero;
    float dt=1f/fps;
    // Advance source pump poses and group events without replacing their gates.
    for(float time=0;time<=g.timeline.duration;time+=dt*g.timeline.speed)
    {
     g.timeline.Evaluate(time,true);m.Tick(dt,time);
     if(!last.body.isKinematic){velocity=last.body.linearVelocity;launched=true;break;}
    }
    Need(launched && velocity.x>0 && velocity.y>0,"Final pump launch at "+fps+": "+velocity);
    report.AppendLine("Pump "+fps+" FPS velocity="+velocity);
    // Check actual sprite endpoint after the original progression tracks.
    g.Restart();m.ResetRescue();foreach(var p in m.people)p.transform.position+=Vector3.up*100;
    foreach(var p in m.people)
    {
     p.transform.position=m.attractionTarget.position+new Vector3(-2,-1,0);p.BeginAttraction();
     for(int i=0;i<fps/2;i++)
     {
      m.attractionTarget.position+=new Vector3(.01f,.003f,0);p.Tick(dt);
      var r=p.laser.GetComponentInChildren<SpriteRenderer>(true);
      Vector3 end=r.sprite.vertices.Select(v=>r.transform.TransformPoint(v)).OrderByDescending(v=>Vector3.Dot(v-p.laser.position,p.laser.right)).First();
      // Artwork thickness is centered around the beam; use its longitudinal endpoint.
      Vector3 target=m.attractionTarget.position;Vector3 delta=target-p.laser.position;delta.z=0;
      float extent=Vector3.Dot(end-p.laser.position,delta.normalized);
      Need(Mathf.Abs(extent-delta.magnitude)<.002f,"Short link "+p.sourceGo+" at "+fps+" error="+(extent-delta.magnitude));
     }
    }
    report.AppendLine("Nine moving link endpoints "+fps+" FPS: PASS");
   }
   g.Restart();m.ResetRescue();
   foreach(float percent in new[]{.355f,.36f,.365f,.575f,.58f,.585f,.60f,.605f,.61f,.615f})
   {
    g.Seek(g.timeline.duration*percent);g.rocket.transform.position=g.rocket.targetReference.position;
    Capture(g.gameCamera,"pass18-background-"+(percent*1000).ToString("F0")+".png");
   }
   var stack=g.gameCamera.GetComponent<EscapeSourceCameraStack>();
   Need(stack.sky.enabled && stack.perspective.enabled && stack.sky.depth<stack.perspective.depth && stack.perspective.depth<g.gameCamera.depth,"Camera render order");
   report.AppendLine("Background frames captured around 36%, 58%, 60-61%; independent cameras enabled");
   File.WriteAllText("Reports/pass18-editor.txt",report.ToString());return report.ToString();
  }
  finally{g.Restart();m.ResetRescue();g.enabled=m.enabled=fx.enabled=particles.enabled=true;}
 }
}
return await Verify18.Run();
