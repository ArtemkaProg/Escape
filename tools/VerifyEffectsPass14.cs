using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
public static class VerifyEffects14
{
 static void Require(bool condition,string text){if(!condition)throw new Exception(text);}
 static void Capture(Camera cam,string name)
 {
  var target=cam.targetTexture;var active=RenderTexture.active;float aspect=cam.aspect;
  var rt=new RenderTexture(1280,720,24);var png=new Texture2D(1280,720,TextureFormat.RGB24,false);
  try{cam.targetTexture=rt;cam.aspect=1280f/720;cam.Render();RenderTexture.active=rt;png.ReadPixels(new Rect(0,0,1280,720),0,0);png.Apply();File.WriteAllBytes("Reports/"+name,png.EncodeToPNG());}
  finally{cam.targetTexture=target;cam.aspect=aspect;RenderTexture.active=active;UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(png);}
 }
 public static async Task<object> Run()
 {
  var fx=UnityEngine.Object.FindFirstObjectByType<EscapeRocketEffects>();var game=fx.flight;
  var cameraPosition=game.gameCamera.transform.position;float size=game.gameCamera.orthographicSize;
  try
  {
   game.Restart();game.enabled=false;fx.enabled=false;fx.ResetEffects();
   Require(fx.lights.Count(l=>l.sprite.name.StartsWith("RocketFuelGauge"))==5,"Expected five gauge lights");
   var gauges=fx.lights.Where(l=>l.sprite.name.StartsWith("RocketFuelGauge")).ToArray();
   Require(gauges.All(l=>l.sprite.color.a>.99f),"Initial fuel lights hidden");
   game.rocket.visualAnimator.enabled=true;game.rocket.visualAnimator.Play("StartEngine",0,1);game.rocket.visualAnimator.Update(0);fx.Tick(0,false);
   Require(gauges.All(l=>l.sprite.color.a>.99f),"Launch animation still hides fuel");
   game.rocket.visualAnimator.enabled=false;
   game.rocket.transform.position=new Vector3(800,100,0);Physics.SyncTransforms();fx.Tick(1,false);
   Require(fx.NearbySurvivors==0 && fx.DoorTime==0 && fx.doorScale.localScale.y==0,"Door not closed away from people");
   var survivor=fx.survivorColliders.First(s=>s.HereAndAlive);var collider=survivor.GetComponent<Collider>();
   Vector3 delta=collider.bounds.center-fx.proximityProbe.transform.TransformPoint(fx.proximityProbe.center);
   game.rocket.transform.position+=delta;Physics.SyncTransforms();fx.Tick(.5f,false);
   Require(fx.NearbySurvivors>0 && Mathf.Abs(fx.doorScale.localScale.y-.5f)<.001f,"Door does not open at original proximity");
   fx.Tick(.5f,false);Require(Mathf.Abs(fx.doorScale.localScale.y-1)<.001f,"Door failed to fully open");
   game.rocket.transform.position=new Vector3(800,100,0);Physics.SyncTransforms();fx.Tick(.5f,false);
   Require(Mathf.Abs(fx.doorScale.localScale.y-.5f)<.001f,"Door reverse transition incorrect");
   fx.Tick(.5f,false);Require(fx.doorScale.localScale.y==0,"Door failed to close");
   fx.ResetEffects();fx.Tick(.1f,true);Require(Mathf.Abs(fx.EmissionScale-2)<.0001f,"Source reactor rise incorrect");
   fx.Tick(3,false);Require(Mathf.Abs(fx.EmissionScale-1)<.0001f,"Source reactor decay incorrect");
   fx.LaunchFuelEvent(0);fx.Tick(1f/6,false);
   Require(fx.lights[fx.fuelBars[0].lightIndex].sprite.color.a<.001f,"Original fuel blink missing");
   Require(fx.fuelBars.Skip(1).All(b=>fx.lights[b.lightIndex].sprite.color.a>.99f),"Wrong fuel bars affected");
   fx.Tick(1f/6,false);Require(fx.lights[fx.fuelBars[0].lightIndex].sprite.color.a>.99f,"Original fuel blink return missing");
   fx.Tick(.5f,false);Require(fx.lights[fx.fuelBars[0].lightIndex].sprite.color.a<.001f,"Fuel bar does not finish dark");
   game.Restart();game.StartFlight();fx.ResetEffects();
   int frame=Time.frameCount;
   foreach(var t in fx.trails)t.renderer.emitting=true;
   for(int i=0;i<20;i++){game.rocket.transform.position+=new Vector3(.12f,.02f,0);fx.Tick(.025f,true);await Task.Delay(25);}
   Require(Time.frameCount>frame,"Editor simulation is not advancing");
   Require(fx.trails.All(t=>t.renderer.positionCount>=3),"Native trails have no real vertices");
   int[] counts=fx.trails.Select(t=>t.renderer.positionCount).ToArray();
   Capture(game.gameCamera,"pass14-flight.png");
   game.gameCamera.transform.position=new Vector3(game.rocket.transform.position.x,game.rocket.transform.position.y,cameraPosition.z);game.gameCamera.orthographicSize=1.3f;
   Capture(game.gameCamera,"pass14-rocket.png");
   var laser=UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(sr=>sr.sprite!=null && UnityEditor.AssetDatabase.GetAssetPath(sr.sprite)=="Assets/RecoveredEffectsPass14Local/texture_67.png" && sr.gameObject.activeInHierarchy);
   game.gameCamera.transform.position=new Vector3(laser.bounds.center.x,laser.bounds.center.y,cameraPosition.z);game.gameCamera.orthographicSize=3;
   Capture(game.gameCamera,"pass14-laser.png");
   var result=new{gauges=5,doorProximityPassed=true,doorReversePassed=true,sourceFuelBlinkPassed=true,reactorRiseDecayPassed=true,trailVertexCounts=counts,frames=Time.frameCount-frame};
   File.WriteAllText("Reports/pass14-effects.txt","PASS: "+result);return result;
  }
  finally{game.gameCamera.transform.position=cameraPosition;game.gameCamera.orthographicSize=size;game.enabled=true;fx.enabled=true;game.Restart();fx.ResetEffects();}
 }
}
return await VerifyEffects14.Run();
