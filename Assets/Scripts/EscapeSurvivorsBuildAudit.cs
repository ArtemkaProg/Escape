#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Opt-in built-player verification of source proximity and rescued events.</summary>
[DefaultExecutionOrder(500)]
public sealed class EscapeSurvivorsBuildAudit : MonoBehaviour
{
    private string output;private EscapeSurvivorRescue rescue;private EscapePlayableZone game;
    private int phase,index;private float elapsed;private Vector3 pausedPose;private bool pausedOnce,done;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-escapeRescueAudit");
        if(i>=0 && i+1<args.Length)new GameObject("PASS17 rescue verification").AddComponent<EscapeSurvivorsBuildAudit>().output=args[i+1];
    }
    private void Start()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-auditFps");
        Application.targetFrameRate=i>=0?int.Parse(args[i+1]):60;QualitySettings.vSyncCount=0;
        game=FindFirstObjectByType<EscapePlayableZone>();rescue=FindFirstObjectByType<EscapeSurvivorRescue>();
        if(game==null || rescue==null)Finish("FAIL: missing rescue scene",1);
    }
    private void LateUpdate()
    {
        if(done)return;elapsed+=Time.deltaTime;
        if(phase==0)
        {
            game.Restart();rescue.ResetRescue();game.InvincibleForAudit=true;game.HoldMotionForAudit=true;game.ThrustOverride=false;
            foreach(var p in rescue.people)p.transform.position+=Vector3.up*100;
            game.StartFlight();phase=1;elapsed=0;return;
        }
        var person=rescue.people[index];
        if(phase==1)
        {
            person.SetFalling(false);person.transform.position=rescue.probe.transform.TransformPoint(rescue.probe.center);
            phase=2;elapsed=0;return;
        }
        if(phase==3)
        {
            if(person.transform.position!=pausedPose){Finish("FAIL: survivor moved during pause",1);return;}
            if(elapsed>.15f){game.TogglePause();phase=2;elapsed=0;}
            return;
        }
        if(!pausedOnce && person.State==1)
        {
            pausedOnce=true;game.TogglePause();pausedPose=person.transform.position;phase=3;elapsed=0;return;
        }
        if(elapsed>4){Finish("FAIL: survivor attraction timeout "+person.sourceGo,1);return;}
        if(!person.Destroyed)return;
        if(person.State!=3 || rescue.SavedCount!=index+1){Finish("FAIL: rescued count/state mismatch",1);return;}
        index++;
        if(index<rescue.people.Length){phase=1;return;}
        game.Restart();rescue.ResetRescue();
        if(rescue.SavedCount!=0 || rescue.people.Any(p=>!p.HereAndAlive || p.State!=0))
        {Finish("FAIL: rescue reset",1);return;}
        Finish("PASS: nine real proximity pickups, entering, single rescued events, pause/resume and restart; FPS="+Application.targetFrameRate,0);
    }
    private void Finish(string result,int code){done=true;File.WriteAllText(output,result+Environment.NewLine);Debug.Log(result);Application.Quit(code);}
}
#endif
