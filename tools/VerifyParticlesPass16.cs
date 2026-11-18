using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
public static class VerifyParticles16
{
 static void Need(bool condition,string message){if(!condition)throw new Exception(message);}
 static ParticleSystem.Particle[] Particles(ParticleSystem p){var a=new ParticleSystem.Particle[p.particleCount];p.GetParticles(a);return a;}
 static void Capture(Camera c,string name)
 {
  var old=c.targetTexture;var active=RenderTexture.active;float aspect=c.aspect;
  var rt=new RenderTexture(1280,720,24);var png=new Texture2D(1280,720,TextureFormat.RGB24,false);
  try {c.targetTexture=rt;c.aspect=1280f/720;c.Render();RenderTexture.active=rt;png.ReadPixels(new Rect(0,0,1280,720),0,0);png.Apply();File.WriteAllBytes("Reports/"+name,png.EncodeToPNG());}
  finally {c.targetTexture=old;c.aspect=aspect;RenderTexture.active=active;UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(png);}
 }
 public static async Task<object> Run()
 {
  UnityEditor.EditorApplication.isPaused=false;Application.runInBackground=true;
  var game=UnityEngine.Object.FindFirstObjectByType<EscapePlayableZone>();var fx=UnityEngine.Object.FindFirstObjectByType<EscapeRocketEffects>();var legacy=UnityEngine.Object.FindFirstObjectByType<EscapeLegacyParticles>();
  Need(legacy!=null && legacy.emitters.Length==5,"Five source emitters missing");
  int first=Time.frameCount;await Task.Delay(500);Need(Time.frameCount>first,"Play Mode frozen");
  var cam=game.gameCamera;var cp=cam.transform.position;float cs=cam.orthographicSize;var savedActive=legacy.emitters.Select(e=>e.system.gameObject.activeSelf).ToArray();
  try
  {
   game.enabled=false;fx.enabled=false;legacy.enabled=false;game.Restart();
   var oil=legacy.emitters.Single(e=>e.goId==30626);var oilTrack=game.timeline.tracks.Single(t=>t.goId==30626 && t.kind==4);
   float oilOn=oilTrack.channels[0].keys.First(k=>k.d>=.5f).time;
   game.timeline.Evaluate(Mathf.Max(0,oilOn-.01f),false);Need(!oil.system.gameObject.activeSelf,"Oil enabled before source gate");
   game.timeline.Evaluate(oilOn+.01f,false);Need(oil.system.gameObject.activeSelf,"Oil source gate not restored");
   var source=File.ReadAllLines("Assets/RecoveredParticlesPass16Local/verification.tsv").Select(l=>l.Split('\t')).ToDictionary(l=>int.Parse(l[0]));
   foreach(var e in legacy.emitters)
   {
    var s=source[e.goId];var main=e.system.main;var rate=e.system.emission.rateOverTime;
    Need(Mathf.Abs(main.startLifetime.constantMax-float.Parse(s[1],System.Globalization.CultureInfo.InvariantCulture))<.0001f && Mathf.Abs(main.startSize.constantMax-float.Parse(s[2],System.Globalization.CultureInfo.InvariantCulture))<.0001f,"Source size/lifetime mismatch");
    Need(Mathf.Abs(rate.constantMax-float.Parse(s[3],System.Globalization.CultureInfo.InvariantCulture))<.001f,"Source emission mismatch");
    var renderer=e.system.GetComponent<ParticleSystemRenderer>();Need(renderer.sharedMaterial.shader.name=="EscapeRecovery/LegacyParticle","Wrong source shader migration");
    Need(renderer.sortingOrder==int.Parse(s[4]),"Sorting order mismatch");
    var sheet=e.system.textureSheetAnimation;Need(!sheet.enabled || sheet.numTilesX==int.Parse(s[5]) && sheet.numTilesY==int.Parse(s[6]),"Texture sheet mismatch");
   }
   int[][] counts=new int[3][];int[] fps={30,60,144};
   for(int run=0;run<fps.Length;run++)
   {
    game.timeline.Evaluate(oilOn+.1f,false);legacy.ResetParticles();
    for(int j=0;j<fps[run]*3;j++)legacy.Tick(1f/fps[run]);
    counts[run]=legacy.emitters.Select(e=>e.system.particleCount).ToArray();Need(counts[run].All(n=>n>0),"Emitter produced no native particles at "+fps[run]);
    foreach(var e in legacy.emitters)
    {
     var particles=Particles(e.system);Need(particles.All(p=>float.IsFinite(p.position.x) && float.IsFinite(p.position.y) && p.startSize>0),"Invalid particle state");
     if(e.goId==30626)Need(particles.Any(p=>p.totalVelocity.y<0),"Oil drops never turn downward; min velocity "+particles.Min(p=>p.totalVelocity.y));
    }
   }
   var all=legacy.emitters.Select(e=>Particles(e.system)).ToArray();
   await Task.Delay(180);
   for(int i=0;i<all.Length;i++)
   {
    var after=Particles(legacy.emitters[i].system);Need(after.Length==all[i].Length,"Paused system emits particles");
    for(int j=0;j<after.Length;j++)Need(after[j].position==all[i][j].position && after[j].remainingLifetime==all[i][j].remainingLifetime,"Paused particles advanced");
   }
   // Render the recovered effects at source positions and the source oil activation pose.
   game.Seek(oilOn+.1f);Capture(cam,"pass16-scene-oil.png");
   cam.transform.position=new Vector3(oil.system.transform.position.x,oil.system.transform.position.y-1,cp.z);cam.orthographicSize=4;Capture(cam,"pass16-oil-close.png");
   var fire=legacy.emitters.First(e=>e.goId==7787).system;
   cam.transform.position=new Vector3(fire.transform.position.x,fire.transform.position.y+2,cp.z);cam.orthographicSize=7;Capture(cam,"pass16-fire-smoke.png");
   var result=new {framesAdvanced=Time.frameCount-first,oilGate=oilOn,counts30=counts[0],counts60=counts[1],counts144=counts[2],pauseFrozen=true};
   File.WriteAllText("Reports/pass16-verification.txt",result.ToString());
   legacy.ResetParticles();Need(legacy.emitters.All(e=>e.system.particleCount==0),"Reset retained particles");
   return result;
  }
  finally
  {
   for(int i=0;i<savedActive.Length;i++)legacy.emitters[i].system.gameObject.SetActive(savedActive[i]);
   game.enabled=true;fx.enabled=true;legacy.enabled=true;game.Restart();fx.ResetEffects();legacy.ResetParticles();cam.transform.position=cp;cam.orthographicSize=cs;
  }
 }
}
return await VerifyParticles16.Run();
