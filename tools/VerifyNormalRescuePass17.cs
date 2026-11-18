using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
var game=UnityEngine.Object.FindFirstObjectByType<EscapePlayableZone>();var rescue=UnityEngine.Object.FindFirstObjectByType<EscapeSurvivorRescue>();
try
{
 game.Restart();rescue.ResetRescue();game.HoldMotionForAudit=true;game.InvincibleForAudit=false;game.ThrustOverride=false;game.StartFlight();await Task.Delay(100);
 var person=rescue.people[0];person.SetFalling(false);person.transform.position=rescue.probe.transform.TransformPoint(rescue.probe.center);
 await Task.Delay(1400);
 if(game.State!=EscapePlayableZone.FlightState.Playing || rescue.SavedCount!=1)throw new Exception("Normal rescue failed: "+game.State+" hit="+game.LastHit+" saved="+rescue.SavedCount);
 File.WriteAllText("Reports/pass17-normal-rescue.txt","PASS: source proximity rescue with invincibility disabled; rocket remains Playing; saved=1");
 return new{normalRescue=true,saved=rescue.SavedCount,state=game.State.ToString()};
}
finally{game.HoldMotionForAudit=false;game.InvincibleForAudit=false;game.ThrustOverride=null;game.Restart();rescue.ResetRescue();}
