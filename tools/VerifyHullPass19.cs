using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor.SceneManagement;
var scene=EditorSceneManager.GetActiveScene();
if(UnityEditor.EditorApplication.isPlaying || scene.path!=EscapeVisualPolishPass19.Output || scene.isDirty)throw new Exception("Verify saved PASS19 outside Play");
var current=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EscapePlayableZone>(true)).Single();
var previous=EditorSceneManager.OpenScene(EscapeBugfixPass18.Output,OpenSceneMode.Additive);GameObject probeObject=null;
try
{
 var old=previous.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EscapePlayableZone>(true)).Single();
 Collider[] Hull(EscapePlayableZone g)=>g.rocketBody.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger && c.attachedRigidbody==g.rocketBody).ToArray();
 Physics.SyncTransforms();var a=Hull(old);var b=Hull(current);if(a.Length!=19 || b.Length!=19)throw new Exception("Hull collider count old="+a.Length+" new="+b.Length+" oldAll="+string.Join(";",old.rocketBody.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+(c.attachedRigidbody==old.rocketBody))));
 Bounds BoundsOf(Collider[] c){var bounds=c[0].bounds;foreach(var x in c.Skip(1))bounds.Encapsulate(x.bounds);return bounds;}
 var ba=BoundsOf(a);var bb=BoundsOf(b);
 if(Mathf.Abs(bb.size.x/ba.size.x-.96f)>.003f || Mathf.Abs(bb.size.y/ba.size.y-.96f)>.003f)throw new Exception("Unexpected aggregate hull shrink: "+ba+" -> "+bb);
 if(b.Any(c=>Mathf.Abs(c.contactOffset-.002f)>.00001f))throw new Exception("Rocket contact skin mismatch");
 string Triggers(EscapePlayableZone g)=>string.Join(";",g.rocketBody.GetComponentsInChildren<Collider>(true).Where(c=>c.isTrigger).Select(c=>c.name+":"+(c is SphereCollider s?s.radius.ToString():((BoxCollider)c).size.ToString())));
 if(Triggers(old)!=Triggers(current))throw new Exception("Trigger/proximity sizes changed");
 probeObject=new GameObject("Temporary native hull probe");var probe=probeObject.AddComponent<BoxCollider>();probe.size=new Vector3(.01f,.01f,.1f);probe.isTrigger=true;
 bool Hits(Collider[] colliders)=>colliders.Any(c=>Physics.ComputePenetration(probe,probe.transform.position,Quaternion.identity,c,c.transform.position,c.transform.rotation,out _,out _));
 int forgiving=0,deep=0;
 for(float x=ba.max.x+.02f;x>ba.max.x-.18f;x-=.002f)
 {
  probe.transform.position=new Vector3(x,old.rocketBody.position.y,0);
  bool oldHit=Hits(a),newHit=Hits(b);if(oldHit && !newHit)forgiving++;if(oldHit && newHit)deep++;
 }
 if(forgiving<3 || deep<3)throw new Exception("Native narrow-phase edge/deep contact check failed "+forgiving+"/"+deep);
 string result="PASS: 19 solid colliders, XY hull scale 0.96, contactOffset 0.002; triggers unchanged; old-only grazing probes="+forgiving+"; retained deep contacts="+deep+"\nOld="+ba+"\nNew="+bb;
 File.WriteAllText("Reports/pass19-hull.txt",result);return result;
}
finally
{
 if(probeObject!=null)UnityEngine.Object.DestroyImmediate(probeObject);
 EditorSceneManager.SetActiveScene(scene);EditorSceneManager.CloseScene(previous,true);
}
