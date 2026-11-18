#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EscapeEffectsPass14
{
    [Serializable] private class Node { public int id,parent,sibling; public string name; }
    [Serializable] private class Laser { public int id;public string png;public float ppu;public float[] pivot; }
    [Serializable] private class LightInfo { public int goId,spriteGo;public float baseEmission,percent,animated; }
    [Serializable] private class Clip {public string name;public float duration;public EscapeOriginalTimeline.Channel channel;}
    [Serializable] private class Door {public int scaleGo,lightGo,probeGo,probeCollider;public Clip clip;}
    [Serializable] private class TrailInfo
    {
        public int goId,material;public string texture;
        public float time,width,endWidth,minVertex,emissionScale,maxEmission,widthMax;
        public bool clampEmission,clampWidth;
        public Color[] gradient;
    }
    [Serializable] private class Presence {public int goId,survivorGo;}
    [Serializable] private class Bar {public int goId,lightGo;}
    [Serializable] private class Data
    {
        public string format;public Node[] nodes;public Laser laser;public LightInfo[] lights;
        public Door door;public TrailInfo[] trails;public Presence[] survivors;public Bar[] bars;
        public Clip fuelClip;
        public float emissionBase,emissionInitial,emissionAnimated,emissionMin,emissionMax,rise,fall;
    }
    public const string Output="Assets/Scenes/Escape_Zone003_PC_EFFECTS_PASS14.unity";
    public static void Create()
    {
        if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save current scene and leave Play Mode first");
        var d=JsonUtility.FromJson<Data>(File.ReadAllText("Assets/RecoveredEffectsPass14Local/effects.json"));
        if(d.format!="ESCFX14" || d.lights.Length!=8 || d.trails.Length!=2 || d.survivors.Length!=9)
            throw new InvalidDataException("Unexpected private effects data");
        var scene=EditorSceneManager.OpenScene(EscapePumpCorrectionPass13.Output);
        string path=AssetDatabase.GenerateUniqueAssetPath(Output);
        if(!EditorSceneManager.SaveScene(scene,path)) throw new IOException("Cannot create separate PASS14");
        string folder="Assets/RecoveredEffectsPass14Local/"+Path.GetFileNameWithoutExtension(path);
        Directory.CreateDirectory(folder);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var roots=scene.GetRootGameObjects();var map=new Dictionary<int,GameObject>();
        foreach(var n in d.nodes)
        {
            var g=n.parent==0?roots.Single(x=>x.name==n.name):map[n.parent].transform.Cast<Transform>()
                .Where(x=>!x.name.StartsWith("__EscapeRecovered",StringComparison.Ordinal)).ElementAt(n.sibling).gameObject;
            if(g.name!=n.name)throw new InvalidDataException("Original hierarchy mismatch: "+n.id);
            map.Add(n.id,g);
        }
        var imp=(TextureImporter)AssetImporter.GetAtPath(d.laser.png);
        imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
        imp.spritePixelsPerUnit=d.laser.ppu;imp.mipmapEnabled=false;imp.alphaIsTransparency=true;
        imp.textureCompression=TextureImporterCompression.Uncompressed;imp.npotScale=TextureImporterNPOTScale.None;
        var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);
        settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(d.laser.pivot[0],d.laser.pivot[1]);
        imp.SetTextureSettings(settings);imp.SaveAndReimport();
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(d.laser.png);int lasers=0;
        foreach(var sr in map.Values.SelectMany(g=>g.GetComponents<SpriteRenderer>()))
        {
            if(sr.sprite==null || !AssetDatabase.GetAssetPath(sr.sprite).EndsWith("escape_sprite_"+d.laser.id+".png",StringComparison.Ordinal))continue;
            sr.sprite=sprite;lasers++;
        }
        if(lasers==0)throw new InvalidDataException("No source UFO laser found");
        var flight=roots.SelectMany(g=>g.GetComponentsInChildren<EscapePlayableZone>(true)).Single();
        var fx=flight.gameObject.AddComponent<EscapeRocketEffects>();fx.flight=flight;
        fx.lights=d.lights.Select(l=>new EscapeRocketEffects.Light {goId=l.goId,sprite=map[l.spriteGo].GetComponent<SpriteRenderer>(),baseEmission=l.baseEmission,percent=l.percent,animated=l.animated}).ToArray();
        fx.doorScale=map[d.door.scaleGo].transform;fx.doorLightIndex=Array.FindIndex(d.lights,l=>l.goId==d.door.lightGo);
        fx.doorCurve=d.door.clip.channel;fx.doorDuration=d.door.clip.duration;
        fx.proximityProbe=map[d.door.probeGo].GetComponent<SphereCollider>();
        fx.survivorColliders=d.survivors.Select(s=>{var c=map[s.goId].AddComponent<EscapeSurvivorPresence>();c.survivor=map[s.survivorGo];return c;}).ToArray();
        fx.fuelBars=d.bars.Select(b=>new EscapeRocketEffects.FuelBar {lightIndex=Array.FindIndex(d.lights,l=>l.goId==b.lightGo)}).ToArray();
        fx.fuelCurve=d.fuelClip.channel;fx.fuelDuration=d.fuelClip.duration;
        fx.emissionBase=d.emissionBase;fx.emissionInitial=d.emissionInitial;fx.emissionAnimated=d.emissionAnimated;
        fx.emissionMin=d.emissionMin;fx.emissionMax=d.emissionMax;fx.rise=d.rise;fx.fall=d.fall;
        var trails=new List<EscapeRocketEffects.Trail>();
        foreach(var t in d.trails)
        {
            var textureImporter=(TextureImporter)AssetImporter.GetAtPath(t.texture);
            textureImporter.mipmapEnabled=false;textureImporter.textureCompression=TextureImporterCompression.Uncompressed;
            textureImporter.wrapMode=TextureWrapMode.Clamp;textureImporter.SaveAndReimport();
            var mat=new Material(Shader.Find("EscapeRecovery/Particle"));
            mat.name=t.material==30?"Recovered original additive reactor trail":"Recovered original alpha smoke trail";
            mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(t.texture);
            float tint=t.material==30?128f/255:.5f;mat.SetColor("_TintColor",new Color(tint,tint,tint,tint));
            mat.SetFloat("_DstBlend",t.material==30?1:10);AssetDatabase.CreateAsset(mat,folder+"/trail_"+t.goId+".mat");
            var go=map[t.goId];go.SetActive(true);var tr=go.AddComponent<TrailRenderer>();
            tr.sharedMaterial=mat;tr.time=t.time;tr.startWidth=t.width;tr.endWidth=t.endWidth;
            tr.minVertexDistance=t.minVertex;tr.autodestruct=false;tr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows=false;tr.textureMode=LineTextureMode.Stretch;tr.alignment=LineAlignment.View;
            // The Unity4 native TrailRenderer stores five uniformly spaced Color32 keys.
            var colors=t.gradient;var gradient=new Gradient();
            gradient.SetKeys(Enumerable.Range(0,5).Select(i=>new GradientColorKey(colors[i],i/4f)).ToArray(),
                Enumerable.Range(0,5).Select(i=>new GradientAlphaKey(colors[i].a,i/4f)).ToArray());tr.colorGradient=gradient;
            trails.Add(new EscapeRocketEffects.Trail {renderer=tr,time=t.time,width=t.width,emissionScale=t.emissionScale,
                clampEmission=t.clampEmission,maxEmission=t.maxEmission,clampWidth=t.clampWidth,widthMax=t.widthMax});
        }
        fx.trails=trails.ToArray();fx.ResetEffects();
        if(fx.proximityProbe==null || fx.doorLightIndex<0 || fx.fuelBars.Any(b=>b.lightIndex<0))throw new InvalidDataException("Missing source light or probe");
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Reports/pass14-scene.txt",path+"\nRepaired laser sprites: "+lasers+"\nOriginal trails: 2\nLight controllers: 8\n");
    }
    public static void Build()
    {
        if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene and leave Play Mode first");
        Directory.CreateDirectory("Builds/Windows-PASS14");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{Output},locationPathName="Builds/Windows-PASS14/Escape-PASS14.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        File.WriteAllText("Reports/pass14-build.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors+"\n");
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("PASS14 build failed");
    }
}
#endif
