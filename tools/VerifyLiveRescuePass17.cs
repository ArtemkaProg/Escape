using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
public static class LiveRescue17
{
 static void Need(bool b,string s){if(!b)throw new Exception(s);}
 public static async Task<object> Run()
 {
  var g=UnityEngine.Object.FindFirstObjectByType<EscapePlayableZone>();var m=UnityEngine.Object.FindFirstObjectByType<EscapeSurvivorRescue>();
  try
  {
   g.Restart();m.ResetRescue();g.HoldMotionForAudit=true;g.InvincibleForAudit=true;g.ThrustOverride=false;
   g.StartFlight();g.Seek(10.5f);await Task.Delay(100);
   var p=m.people.Single(x=>x.sourceGo==28307);Vector3 probe=m.probe.transform.TransformPoint(m.probe.center);
   g.rocket.transform.position+=p.transform.position-probe;
   await Task.Delay(200);Need(p.State>0,"Live game did not trigger attraction");
   g.TogglePause();Vector3 pose=p.transform.position;float time=g.ZoneTime;await Task.Delay(200);
   Need(p.transform.position==pose && g.ZoneTime==time,"Live pause moved survivor");
   g.TogglePause();await Task.Delay(1400);Need(m.SavedCount>=1 && p.State==3,"Live game never notified rescue");
   var result=new{liveSaved=m.SavedCount,sourceGo=p.sourceGo,pause=true,frame=Time.frameCount};
   File.WriteAllText("Reports/pass17-live-rescue.txt",result.ToString());return result;
  }
  finally{g.HoldMotionForAudit=false;g.InvincibleForAudit=false;g.ThrustOverride=null;g.Restart();m.ResetRescue();}
 }
}
return await LiveRescue17.Run();
