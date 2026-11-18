#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
public static class EscapeSurvivorsPass17
{
 [Serializable] class Node{public int id,parent,sibling;public string name;}
 [Serializable] class Person
 {
  public int goId,markerGo,colliderGo,animatorGo,initialState,mode,laserGo,linkRoot;
  public float duration,smooth,minSpin,maxSpin,reach,hideDelay,linkDuration,mass,drag,angularDrag,linkLock,linkCut;
  public EscapeSurvivorRescue.Clip[] animations;public EscapeSurvivorRescue.Track[] linkTracks;
 }
 [Serializable] class Group{public int state;public int[] members;public EscapeOriginalTimeline.Channel gate;}
 [Serializable] class Follow{public int masterGo,slaveGo;public EscapeOriginalTimeline.Channel gate;}
 [Serializable] class Data
 {
  public string format;public Node[] nodes;public Person[] people;public Group[] groups;public Follow follow;
  public int probeGo,linkProbeGo,targetGo,backgroundMask;public float fov,perspectiveZ;
 }
 public const string Output="Assets/Scenes/Escape_Zone003_PC_SURVIVORS_PASS17.unity";
 public static void Create()
 {
  if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene and stop Play Mode first");
  var d=JsonUtility.FromJson<Data>(File.ReadAllText("Assets/RecoveredSurvivorsPass17Local/survivors.json"));
  if(d.format!="ESCSURV17" || d.people.Length!=9)throw new InvalidDataException("Missing private survivor source");
  var scene=EditorSceneManager.OpenScene(EscapeParticlesPass16.Output);string output=AssetDatabase.GenerateUniqueAssetPath(Output);EditorSceneManager.SaveScene(scene,output);
  var roots=scene.GetRootGameObjects();var map=new Dictionary<int,GameObject>();
  foreach(var n in d.nodes)
  {
   var g=n.parent==0?roots.Single(x=>x.name==n.name):map[n.parent].transform.Cast<Transform>().Where(x=>!x.name.StartsWith("__EscapeRecovered",StringComparison.Ordinal)).ElementAt(n.sibling).gameObject;
   if(g.name!=n.name)throw new InvalidDataException("Source hierarchy mismatch "+n.id);map.Add(n.id,g);
  }
  var flight=roots.SelectMany(g=>g.GetComponentsInChildren<EscapePlayableZone>(true)).Single();
  var manager=flight.gameObject.AddComponent<EscapeSurvivorRescue>();manager.flight=flight;manager.probe=map[d.probeGo].GetComponent<SphereCollider>();manager.linkProbe=map[d.linkProbeGo].GetComponent<SphereCollider>();manager.attractionTarget=map[d.targetGo].transform;
  void Bind(EscapeSurvivorRescue.Track[] tracks){foreach(var t in tracks){t.target=map[t.goId].transform;t.sprite=map[t.goId].GetComponent<SpriteRenderer>();}}
  manager.people=d.people.Select(p=>
  {
   var g=map[p.goId];var actor=g.AddComponent<EscapeRecoveredSurvivor>();actor.owner=manager;actor.sourceGo=p.goId;
   actor.proximityCollider=map[p.markerGo].GetComponent<Collider>();actor.duration=p.duration;actor.smooth=p.smooth;actor.minSpin=p.minSpin;actor.maxSpin=p.maxSpin;actor.reach=p.reach;actor.hideDelay=p.hideDelay;actor.mode=p.mode;
   actor.body=g.GetComponent<Rigidbody>();if(actor.body==null)actor.body=g.AddComponent<Rigidbody>();actor.body.isKinematic=true;actor.body.useGravity=true;actor.body.mass=p.mass;actor.body.linearDamping=p.drag;actor.body.angularDamping=p.angularDrag;actor.body.constraints=(RigidbodyConstraints)56;
   actor.laser=map[p.laserGo].transform;actor.linkRoot=map[p.linkRoot].transform;actor.linkDuration=p.linkDuration;actor.linkLock=p.linkLock;actor.linkCut=p.linkCut;actor.linkTracks=p.linkTracks;Bind(actor.linkTracks);
   actor.animations=p.animations;foreach(var a in actor.animations)Bind(a.tracks);
   map[p.markerGo].GetComponent<EscapeSurvivorPresence>().actor=actor;
   return actor;
  }).ToArray();
  manager.groups=d.groups.Select(g=>new EscapeSurvivorRescue.Group{state=g.state,gate=g.gate,members=g.members.Select(id=>manager.people.Single(p=>p.sourceGo==id)).ToArray()}).ToArray();
  manager.followMaster=map[d.follow.masterGo].transform;manager.followSlave=map[d.follow.slaveGo].transform;manager.followGate=d.follow.gate;
  manager.ResetRescue();
  // Explicit screenshot-based oil size calibration; the preserved source ranges
  // remain in private PASS16 data. This is not an asserted Unity4 formula.
  var oil=flight.GetComponent<EscapeLegacyParticles>().emitters.Single(e=>e.goId==30626).system;
  var om=oil.main;var size=om.startSize;size.constantMin*=.55f;size.constantMax*=.55f;om.startSize=size;
  var camera=flight.gameCamera;var stack=camera.gameObject.AddComponent<EscapeSourceCameraStack>();stack.sourceFov=d.fov;stack.sourceZ=d.perspectiveZ;stack.skySizeRatio=5.4f/4.05f;
  Camera LayerCamera(string name,int mask,CameraClearFlags clear)
  {
   var c=new GameObject(name).AddComponent<Camera>();c.CopyFrom(camera);c.enabled=false;c.cullingMask=mask;c.clearFlags=clear;c.transform.SetParent(camera.transform,false);return c;
  }
  stack.sky=LayerCamera("__EscapeRecoveredSky",1<<18,CameraClearFlags.SolidColor);
  stack.perspective=LayerCamera("__EscapeRecoveredPerspective",d.backgroundMask,CameraClearFlags.Depth);stack.perspective.orthographic=false;
  camera.cullingMask&=~(d.backgroundMask|(1<<18));camera.clearFlags=CameraClearFlags.Depth;
  string folder="Assets/RecoveredSurvivorsPass17Local/"+Path.GetFileNameWithoutExtension(output);Directory.CreateDirectory(folder);AssetDatabase.Refresh();
  var old=map[16192].transform.Find("__EscapeRecoveredMesh").GetComponent<MeshRenderer>().sharedMaterial;
  var background=new Material(Shader.Find("EscapeRecovery/AlphaBackground")){name="Source Alpha8 industrial gradient"};background.mainTexture=old.mainTexture;background.mainTextureScale=old.mainTextureScale;background.mainTextureOffset=old.mainTextureOffset;
  AssetDatabase.CreateAsset(background,folder+"/background.mat");
  foreach(int id in new[]{16192,33126})map[id].transform.Find("__EscapeRecoveredMesh").GetComponent<MeshRenderer>().sharedMaterial=background;
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  File.WriteAllText("Reports/pass17-scene.txt",output+"\nSurvivors: 9\nSource background FOV: "+d.fov+"\nOil reference size scale: 0.55\n");
 }
 public static void Build()
 {
  if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save and stop Play first");
  var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Output},locationPathName="Builds/Windows-PASS17/Escape-PASS17.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
  File.WriteAllText("Reports/pass17-build.txt",r.summary.result+"\nErrors: "+r.summary.totalErrors);if(r.summary.result!=BuildResult.Succeeded)throw new Exception("PASS17 build failed");
 }
}
#endif
