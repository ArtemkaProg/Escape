#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
public static class EscapeVisualPolishPass19
{
 public const string Output="Assets/Scenes/Escape_Zone003_PC_VISUAL_POLISH_PASS19.unity";
 public const float HullScale=.96f,ContactOffset=.002f;
 public static void Create()
 {
  if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene and stop Play first");
  if(File.Exists(Output))throw new IOException("PASS19 already exists");
  var scene=EditorSceneManager.OpenScene(EscapeBugfixPass18.Output);EditorSceneManager.SaveScene(scene,Output);
  var game=UnityEngine.Object.FindFirstObjectByType<EscapePlayableZone>();
  var stack=game.gameCamera.GetComponent<EscapeSourceCameraStack>();stack.sortBackgroundByDepth=true;stack.Synchronize();
  string folder="Assets/RecoveredPolishPass19Local";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
  Vector3 Inset(Vector3 point){var p=game.rocketBody.transform.InverseTransformPoint(point);p.x*=HullScale;p.y*=HullScale;return game.rocketBody.transform.TransformPoint(p);}
  var hull=game.rocketBody.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger && c.attachedRigidbody==game.rocketBody).ToArray();
  var report=new System.Text.StringBuilder();int count=0;
  foreach(var c in hull)
  {
   report.AppendLine(c.name+" "+c.GetType().Name+" oldBounds="+c.bounds);
   if(c is BoxCollider box)
   {
    box.center=box.transform.InverseTransformPoint(Inset(box.transform.TransformPoint(box.center)));
    var size=box.size;size.x*=HullScale;size.y*=HullScale;box.size=size;
   }
   else if(c is SphereCollider sphere)
   {
    sphere.center=sphere.transform.InverseTransformPoint(Inset(sphere.transform.TransformPoint(sphere.center)));sphere.radius*=HullScale;
   }
   else if(c is MeshCollider mesh)
   {
    var copy=UnityEngine.Object.Instantiate(mesh.sharedMesh);copy.name=mesh.sharedMesh.name+"_Hull96";
    copy.vertices=copy.vertices.Select(v=>mesh.transform.InverseTransformPoint(Inset(mesh.transform.TransformPoint(v)))).ToArray();copy.RecalculateBounds();
    AssetDatabase.CreateAsset(copy,folder+"/hull_"+(count++)+".asset");
    Physics.BakeMesh(copy.GetInstanceID(),mesh.convex);mesh.sharedMesh=null;mesh.sharedMesh=copy;
    bool enabled=mesh.enabled;mesh.enabled=false;mesh.enabled=enabled;
    EditorUtility.SetDirty(copy);
   }
   else throw new NotSupportedException(c.GetType().Name);
   c.contactOffset=ContactOffset;
  }
  if(hull.Length!=19 || count!=12)throw new InvalidDataException("Unexpected source rocket hull");
  Physics.SyncTransforms();if(hull.Any(c=>c.attachedRigidbody!=game.rocketBody))throw new InvalidDataException("A collider did not cook into the rocket body");
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  File.WriteAllText("Reports/pass19-scene.txt",Output+"\nHull XY scale: "+HullScale+"\nRocket contact offset: "+ContactOffset+"\nNon-trigger hull colliders: "+hull.Length+"\n"+report);
 }
 public static void Build()
 {
  if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save and stop Play first");
  var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Output},locationPathName="Builds/Windows-PASS19/Escape-PASS19.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
  File.WriteAllText("Reports/pass19-build.txt",r.summary.result+"\nErrors: "+r.summary.totalErrors+"\nUnity: "+Application.unityVersion);if(r.summary.result!=BuildResult.Succeeded)throw new Exception("PASS19 build failed");
 }
}
#endif
