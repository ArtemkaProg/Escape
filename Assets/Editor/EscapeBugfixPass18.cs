#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
public static class EscapeBugfixPass18
{
 public const string Output="Assets/Scenes/Escape_Zone003_PC_BUGFIX_PASS18.unity";
 public static void Create()
 {
  if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene and stop Play first");
  if(File.Exists(Output))throw new IOException("PASS18 already exists");
  var scene=EditorSceneManager.OpenScene(EscapeSurvivorsPass17.Output);
  EditorSceneManager.SaveScene(scene,Output);
  var stack=UnityEngine.Object.FindFirstObjectByType<EscapeSourceCameraStack>();stack.Synchronize();
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
 }
 public static void Build()
 {
  if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save and stop Play first");
  var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Output},locationPathName="Builds/Windows-PASS18/Escape-PASS18.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
  File.WriteAllText("Reports/pass18-build.txt",r.summary.result+"\nErrors: "+r.summary.totalErrors);if(r.summary.result!=BuildResult.Succeeded)throw new Exception("PASS18 build failed");
 }
}
#endif
