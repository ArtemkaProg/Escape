#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using UnityEngine;
// Opt-in regression harness; absent from normal gameplay unless explicitly requested.
[DefaultExecutionOrder(1100)]
public sealed class EscapeBugfixBuildAudit : MonoBehaviour
{
 string output;EscapePlayableZone game;EscapeSurvivorRescue rescue;EscapeRecoveredSurvivor last;
 EscapeSourceCameraStack stack;MeshRenderer[] backgrounds;bool started,launched,paused,finished;float pauseTime;Vector3 launch;int[] missing;
 RenderTexture auditTarget;
 int renderedFrames,backgroundFrames;
 void OnRendered(Camera camera){if(camera==stack.perspective)backgroundFrames++;}
 void OnDestroy(){Camera.onPostRender-=OnRendered;}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Install()
 {
  var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-escapeBugfixAudit");
  if(i>=0 && i+1<args.Length)new GameObject("PASS18 regression audit").AddComponent<EscapeBugfixBuildAudit>().output=args[i+1];
 }
 void Start()
 {
  Application.runInBackground=true;
  var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-auditFps");Application.targetFrameRate=i>=0?int.Parse(args[i+1]):60;QualitySettings.vSyncCount=0;
  game=FindFirstObjectByType<EscapePlayableZone>();rescue=FindFirstObjectByType<EscapeSurvivorRescue>();
  stack=game.gameCamera.GetComponent<EscapeSourceCameraStack>();last=rescue.people.Single(p=>p.sourceGo==27181);
  backgrounds=FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.gameObject.layer==17).ToArray();missing=new int[backgrounds.Length];
  auditTarget=new RenderTexture(640,360,24);game.gameCamera.targetTexture=auditTarget;stack.Synchronize();
  Camera.onPostRender+=OnRendered;
  Debug.Log("PASS18 audit initialized; Unity="+Application.unityVersion+" duration="+game.timeline.duration);
 }
 void LateUpdate()
 {
  if(finished)return;
  if(++renderedFrames%300==0)Debug.Log("PASS18 audit frame="+renderedFrames+" time="+game.ZoneTime+" state="+game.State);
  if(!started){game.Restart();rescue.ResetRescue();game.HoldMotionForAudit=true;game.InvincibleForAudit=true;game.ThrustOverride=false;game.StartFlight();started=true;return;}
  if(!stack.sky.enabled || !stack.perspective.enabled || stack.sky.depth>=stack.perspective.depth || stack.perspective.depth>=game.gameCamera.depth){Finish("FAIL: camera order",1);return;}
  // Exercise the production camera loop. The shared offscreen target lets a
  // hidden player render without presenting a window; no extra Camera.Render.
  var planes=GeometryUtility.CalculateFrustumPlanes(stack.perspective);
  for(int i=0;i<backgrounds.Length;i++)
  {
   var r=backgrounds[i];var b=r.bounds;
   bool inside=planes.All(p=>p.GetDistanceToPoint(b.center)>.1f);
   missing[i]=inside && !r.isVisible?missing[i]+1:0;
   if(missing[i]>2){Finish("FAIL: visible background culled early time="+game.ZoneTime+" bg="+r.bounds+" active="+r.gameObject.activeInHierarchy+" enabled="+r.enabled+" force="+r.forceRenderingOff+" layer="+r.gameObject.layer+" camera="+stack.perspective.transform.position+" mask="+stack.perspective.cullingMask+" ancestors="+string.Join(";",r.GetComponentsInParent<Transform>(true).Select(t=>t.name+":"+t.gameObject.activeSelf)),1);return;}
  }
  if(!launched && !last.body.isKinematic)
  {
   launch=last.body.linearVelocity;launched=true;
   if(launch.x<=0 || launch.y<=0){Finish("FAIL: pump launch "+launch,1);return;}
   game.TogglePause();pauseTime=Time.unscaledTime;return;
  }
  if(launched && !paused)
  {
   if(Time.unscaledTime-pauseTime<.15f)return;
   game.TogglePause();paused=true;return;
  }
  if(launched && paused && game.State==EscapePlayableZone.FlightState.Playing && last.body.isKinematic)return;
  if(game.State==EscapePlayableZone.FlightState.Complete)
  {
   if(!launched){Finish("FAIL: pump never released",1);return;}
   if(backgroundFrames<100){Finish("FAIL: background camera did not render enough frames: "+backgroundFrames,1);return;}
   Finish("PASS: full zone, normal camera loop, background frustum visibility; rendered background frames="+backgroundFrames+", upward/forward pump launch="+launch+", pause/resume; FPS="+Application.targetFrameRate,0);
  }
 }
 void Finish(string text,int code){finished=true;Debug.Log("PASS18 audit draining render jobs: "+text);StartCoroutine(FinishAfterFrames(text,code));}
 System.Collections.IEnumerator FinishAfterFrames(string text,int code)
 {
  if(game.State==EscapePlayableZone.FlightState.Playing)game.TogglePause();
  game.gameCamera.enabled=false;stack.sky.enabled=false;stack.perspective.enabled=false;
  game.gameCamera.targetTexture=null;stack.sky.targetTexture=null;stack.perspective.targetTexture=null;
  if(auditTarget!=null){auditTarget.Release();Destroy(auditTarget);}
  // Drain the last render's temporary jobs before terminating the harness.
  for(int i=0;i<8;i++)yield return null;
  File.WriteAllText(output,text+Environment.NewLine);Debug.Log(text);Application.Quit(code);
 }
}
#endif
