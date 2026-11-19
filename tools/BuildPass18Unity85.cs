using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
if(EditorApplication.isPlaying || UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Save scene and stop Play first");
if(UnityEngine.Application.unityVersion!="6000.0.85f2")throw new Exception("Unexpected Editor version");
var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{EscapeBugfixPass18.Output},locationPathName="Builds/Windows-PASS18-6000.0.85f2/Escape-PASS18.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
string result=r.summary.result+"\nErrors: "+r.summary.totalErrors+"\nUnity: "+UnityEngine.Application.unityVersion;
System.IO.File.WriteAllText("Reports/pass18-85-build.txt",result);
if(r.summary.result!=BuildResult.Succeeded)throw new Exception(result);return result;
