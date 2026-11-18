#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
public static class EscapeVisualCorrectionPass15
{
    [Serializable] private class Node {public int id,parent,sibling;public string name;}
    [Serializable] private class Laser {public int id;public string png;public float ppu;public float[] pivot;}
    [Serializable] private class Trail {public int goId,material;public string texture;}
    [Serializable] private class Key {public float time,value,inTangent,outTangent;}
    [Serializable] private class Curve {public Key[] keys;}
    [Serializable] private class Part {public int goId;public Curve[] curves;}
    [Serializable] private class Group {public int goId;public float speed;public EscapeOriginalTimeline.Channel gate;public Part[] tracks;}
    [Serializable] private class Data {public string format;public Node[] nodes;public Laser laser;public Trail[] trails;public Group[] groups;}
    public const string Output="Assets/Scenes/Escape_Zone003_PC_RESTORED_PASS15.unity";
    public static void Create()
    {
        if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene and leave Play Mode first");
        var d=JsonUtility.FromJson<Data>(File.ReadAllText("Assets/RecoveredVisualsPass15Local/visuals.json"));
        if(d.format!="ESCVIS15" || d.groups.Sum(g=>g.tracks.Length)!=48)throw new InvalidDataException("Unexpected original data");
        var scene=EditorSceneManager.OpenScene(EscapeEffectsPass14.Output);string output=AssetDatabase.GenerateUniqueAssetPath(Output);
        if(!EditorSceneManager.SaveScene(scene,output))throw new IOException("Cannot create separate PASS15");
        var roots=scene.GetRootGameObjects();var map=new Dictionary<int,GameObject>();
        foreach(var n in d.nodes)
        {
            var go=n.parent==0?roots.Single(g=>g.name==n.name):map[n.parent].transform.Cast<Transform>().Where(t=>!t.name.StartsWith("__EscapeRecovered",StringComparison.Ordinal)).ElementAt(n.sibling).gameObject;
            if(go.name!=n.name)throw new InvalidDataException("Source hierarchy mismatch "+n.id);map.Add(n.id,go);
        }
        var importer=(TextureImporter)AssetImporter.GetAtPath(d.laser.png);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=d.laser.ppu;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(d.laser.pivot[0],d.laser.pivot[1]);importer.SetTextureSettings(settings);importer.SaveAndReimport();
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(d.laser.png);int repaired=0;
        foreach(var sr in map.Values.SelectMany(go=>go.GetComponents<SpriteRenderer>()))
            if(sr.sprite!=null && AssetDatabase.GetAssetPath(sr.sprite).EndsWith("escape_sprite_"+d.laser.id+".png",StringComparison.Ordinal)){sr.sprite=sprite;repaired++;}
        if(repaired==0 || map[16824].GetComponent<SpriteRenderer>().sprite!=sprite)throw new InvalidDataException("Actual animated laser not repaired");
        string folder="Assets/RecoveredVisualsPass15Local/"+Path.GetFileNameWithoutExtension(output);Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var fx=roots.SelectMany(g=>g.GetComponentsInChildren<EscapeRocketEffects>(true)).Single();
        foreach(var t in d.trails)
        {
            var renderer=map[t.goId].GetComponent<TrailRenderer>();var mat=new Material(renderer.sharedMaterial);
            float tint=t.material==30?128f/255:.5f;mat.SetColor("_TintColor",new Color(tint,tint,tint,tint));mat.SetFloat("_DstBlend",t.material==30?1:10);
            mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(t.texture);mat.name="Original reactor material "+t.material;
            AssetDatabase.CreateAsset(mat,folder+"/reactor_"+t.material+".mat");renderer.sharedMaterial=mat;
        }
        var timeline=fx.flight.timeline;var playback=timeline.gameObject.AddComponent<EscapeRecordedDestruction>();
        playback.groups=d.groups.Select(g=>new EscapeRecordedDestruction.Group {goId=g.goId,speed=g.speed,gate=g.gate,
            parts=g.tracks.Select(p=>new EscapeRecordedDestruction.Part {target=map[p.goId].transform,initialPosition=map[p.goId].transform.localPosition,initialRotation=map[p.goId].transform.localRotation,
                curves=p.curves.Select(c=>new AnimationCurve(c.keys.Select(k=>new Keyframe(k.time,k.value,k.inTangent,k.outTangent)).ToArray()) {preWrapMode=WrapMode.ClampForever,postWrapMode=WrapMode.ClampForever}).ToArray()}).ToArray()}).ToArray();
        timeline.destruction=playback;timeline.ResetTimeline();fx.ResetEffects();
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save PASS15");AssetDatabase.SaveAssets();
        File.WriteAllText("Reports/pass15-scene.txt",output+"\nActual laser renderers: "+repaired+"\nRecorded debris: 48\nReactor materials: 30 additive, 29 alpha\n");
    }
    public static void Build()
    {
        if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene and leave Play Mode first");
        Directory.CreateDirectory("Builds/Windows-PASS15");var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{Output},locationPathName="Builds/Windows-PASS15/Escape-PASS15.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        File.WriteAllText("Reports/pass15-build.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors+"\n");if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Build failed");
    }
}
#endif
