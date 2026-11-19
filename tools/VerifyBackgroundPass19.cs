using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
public static class Background19
{
 static void Need(bool ok,string text){if(!ok)throw new Exception(text);}
 static Color32[] Capture(Camera camera,string path)
 {
  var stack=camera.GetComponent<EscapeSourceCameraStack>();var old=camera.targetTexture;float aspect=camera.aspect;var active=RenderTexture.active;
  var rt=new RenderTexture(960,540,24);var texture=new Texture2D(960,540,TextureFormat.RGB24,false);
  try
  {
   camera.targetTexture=rt;camera.aspect=16f/9;stack.Synchronize();stack.sky.Render();stack.perspective.Render();
   RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();
   if(path!=null)File.WriteAllBytes("Reports/"+path,texture.EncodeToPNG());return texture.GetPixels32();
  }
  finally{camera.targetTexture=old;camera.aspect=aspect;stack.Synchronize();RenderTexture.active=active;UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(texture);}
 }
 static double Difference(Color32[] a,Color32[] b)
 {
  double sum=0;for(int i=0;i<a.Length;i++)sum+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);return sum/(3*a.Length);
 }
 public static async Task<object> Run()
 {
  var game=UnityEngine.Object.FindFirstObjectByType<EscapePlayableZone>();var particles=game.GetComponent<EscapeLegacyParticles>();
  var rescue=game.GetComponent<EscapeSurvivorRescue>();var effects=game.GetComponent<EscapeRocketEffects>();var camera=game.gameCamera;var stack=camera.GetComponent<EscapeSourceCameraStack>();
  int frame=Time.frameCount;await Task.Delay(200);Need(Time.frameCount>frame,"Frozen Play Mode");
  game.enabled=particles.enabled=rescue.enabled=effects.enabled=false;
  try
  {
   game.Restart();rescue.ResetRescue();particles.ResetParticles();
   Need(game.rocketBody.GetComponentsInChildren<Collider>(true).Count(c=>!c.isTrigger && c.attachedRigidbody==game.rocketBody)==19,"Hull did not survive Play reload");
   const float dt=1f/120;
   for(int i=1;i<=1632;i++){game.Seek(i*dt*game.timeline.speed);particles.Tick(dt);}
   var smoke=particles.emitters.Single(e=>e.goId==6420).system.GetComponent<ParticleSystemRenderer>();
   Capture(camera,null);var center=smoke.bounds.center;
   var cranes=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.gameObject.layer==17).OrderBy(r=>r.bounds.center.x).ToArray();
   Need(cranes.Length==2 && smoke.bounds.size.sqrMagnitude>0,"Background renderer setup");
   var report=new System.Text.StringBuilder();int index=0;
   foreach(var crane in cranes)
   {
    var b=crane.bounds.center;double dz=stack.sourceZ,cy=camera.transform.position.y;
    double crossing=((double)b.x*b.x+(double)b.y*b.y+(double)b.z*b.z-(double)center.x*center.x-(double)center.y*center.y-(double)center.z*center.z-2*cy*(b.y-center.y)-2*dz*(b.z-center.z))/(2*(b.x-center.x));
    Color32[] Shot(bool fixedSort,float x,string suffix)
    {
     stack.sortBackgroundByDepth=fixedSort;stack.perspective.transparencySortMode=fixedSort?TransparencySortMode.Orthographic:TransparencySortMode.Default;
     var pos=camera.transform.position;pos.x=x;camera.transform.position=pos;
     return Capture(camera,"pass19-sort-"+index+"-"+suffix+".png");
    }
    var oldLeft=Shot(false,(float)crossing-.004f,"old-left");var oldRight=Shot(false,(float)crossing+.004f,"old-right");
    var newLeft=Shot(true,(float)crossing-.004f,"fixed-left");var newRight=Shot(true,(float)crossing+.004f,"fixed-right");
    double oldDelta=Difference(oldLeft,oldRight),newDelta=Difference(newLeft,newRight);
    report.AppendLine("Crane "+index+" crossing="+crossing.ToString("F3")+" progress="+((crossing-905)/150*100).ToString("F2")+"%; old frame delta="+oldDelta.ToString("F4")+"; fixed delta="+newDelta.ToString("F4"));
    Need(oldDelta>newDelta*2,"The legacy sorting jump was not reproduced or removed: "+report);index++;
   }
   stack.sortBackgroundByDepth=true;stack.Synchronize();
   Need(!stack.perspective.orthographic && stack.perspective.transparencySortMode==TransparencySortMode.Orthographic,"Projection/sorting changed incorrectly");
   var result="PASS: frozen smoke, both radial sorting crossings; no global fog/crane swap with depth sorting.\n"+report;
   File.WriteAllText("Reports/pass19-background.txt",result);return result;
  }
  finally{stack.sortBackgroundByDepth=true;game.Restart();rescue.ResetRescue();particles.ResetParticles();game.enabled=particles.enabled=rescue.enabled=effects.enabled=true;stack.Synchronize();}
 }
}
return await Background19.Run();
