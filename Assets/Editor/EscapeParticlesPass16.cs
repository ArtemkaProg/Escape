#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class EscapeParticlesPass16
{
    [Serializable] private class Node {public int id,parent,sibling;public string name;}
    [Serializable] private class Mat {public string key,name,texture;public int mode;public float cutoff;}
    [Serializable] private class Emitter
    {
        public int goId,sourceEmitter,sortingLayer,sortingOrder,tilesX,tilesY;
        public string name,material;
        public float minSize,maxSize,minEnergy,maxEnergy,minEmission,maxEmission,inheritVelocity;
        public float angularVelocity,randomAngularVelocity,sizeGrow,cameraVelocity,lengthScale,velocityScale,maxParticleSize,cycles;
        public Vector3 worldVelocity,localVelocity,randomVelocity,ellipsoid,randomForce,force;
        public bool animateColor;public Color[] colors;
    }
    [Serializable] private class Data {public string format;public Node[] nodes;public Emitter[] particles;public Mat[] materials;}
    public const string Output="Assets/Scenes/Escape_Zone003_PC_PARTICLES_PASS16.unity";
    private static ParticleSystem.MinMaxCurve Range(float min,float max) => min==max ? new ParticleSystem.MinMaxCurve(min) : new ParticleSystem.MinMaxCurve(min,max);
    public static void Create()
    {
        if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene and leave Play Mode first");
        var d=JsonUtility.FromJson<Data>(File.ReadAllText("Assets/RecoveredParticlesPass16Local/particles.json"));
        if(d.format!="ESCPARTICLE16" || d.particles.Length!=5 || d.materials.Length!=3)throw new InvalidDataException("Unexpected source particle data");
        var scene=EditorSceneManager.OpenScene(EscapeVisualCorrectionPass15.Output);
        string output=AssetDatabase.GenerateUniqueAssetPath(Output);
        if(!EditorSceneManager.SaveScene(scene,output))throw new IOException("Cannot create separate PASS16");
        var roots=scene.GetRootGameObjects();var map=new Dictionary<int,GameObject>();
        foreach(var n in d.nodes)
        {
            var g=n.parent==0?roots.Single(x=>x.name==n.name):map[n.parent].transform.Cast<Transform>().Where(x=>!x.name.StartsWith("__EscapeRecovered",StringComparison.Ordinal)).ElementAt(n.sibling).gameObject;
            if(g.name!=n.name)throw new InvalidDataException("Source hierarchy mismatch "+n.id);map.Add(n.id,g);
        }
        string folder="Assets/RecoveredParticlesPass16Local/"+Path.GetFileNameWithoutExtension(output);
        Directory.CreateDirectory(folder);AssetDatabase.Refresh();var materials=new Dictionary<string,Material>();
        foreach(var m in d.materials)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(m.texture);importer.textureType=TextureImporterType.Default;
            importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.SaveAndReimport();
            var mat=new Material(Shader.Find("EscapeRecovery/LegacyParticle")){name=m.name};mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(m.texture);
            mat.SetFloat("_Mode",m.mode);mat.SetFloat("_Cutoff",m.cutoff);
            mat.SetFloat("_SrcBlend",m.mode==2?1:m.mode==1?2:5);mat.SetFloat("_DstBlend",m.mode==2?0:m.mode==1?3:10);
            mat.SetFloat("_ZWrite",m.mode==2?1:0);mat.renderQueue=m.mode==2?2450:3000;
            AssetDatabase.CreateAsset(mat,folder+"/material_"+m.key+".mat");materials.Add(m.key,mat);
        }
        var flight=roots.SelectMany(g=>g.GetComponentsInChildren<EscapePlayableZone>(true)).Single();
        var controller=flight.gameObject.AddComponent<EscapeLegacyParticles>();controller.flight=flight;
        var emitters=new List<EscapeLegacyParticles.Emitter>();
        foreach(var e in d.particles)
        {
            var go=map[e.goId];var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=true;main.duration=e.maxEnergy;main.startLifetime=Range(e.minEnergy,e.maxEnergy);
            main.startSpeed=0;main.startSize=Range(e.minSize,e.maxSize);main.startColor=Color.white;
            main.startRotation=Range(0,2*Mathf.PI);main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.scalingMode=ParticleSystemScalingMode.Shape;main.maxParticles=Mathf.CeilToInt(e.maxEmission*e.maxEnergy)+32;
            main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            ps.useAutoRandomSeed=false;ps.randomSeed=(uint)e.goId;
            var emission=ps.emission;emission.enabled=true;emission.rateOverTime=Range(e.minEmission,e.maxEmission);
            var shape=ps.shape;float radius=Mathf.Max(e.ellipsoid.x,e.ellipsoid.y,e.ellipsoid.z);
            shape.enabled=radius>0;
            if(radius>0) {shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=radius;shape.radiusThickness=1;shape.scale=e.ellipsoid/radius;}
            // These five source emitters have no simultaneously nonzero local/world velocity.
            Vector3 vel=e.worldVelocity+go.transform.TransformDirection(e.localVelocity);
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            // Vector module axes must all use the same mode, even for zero ranges.
            velocity.x=new ParticleSystem.MinMaxCurve(vel.x-e.randomVelocity.x,vel.x+e.randomVelocity.x);
            velocity.y=new ParticleSystem.MinMaxCurve(vel.y-e.randomVelocity.y,vel.y+e.randomVelocity.y);
            velocity.z=new ParticleSystem.MinMaxCurve(vel.z-e.randomVelocity.z,vel.z+e.randomVelocity.z);
            var inherit=ps.inheritVelocity;inherit.enabled=e.inheritVelocity!=0;inherit.mode=ParticleSystemInheritVelocityMode.Initial;inherit.curve=e.inheritVelocity;
            var rotation=ps.rotationOverLifetime;rotation.enabled=true;
            rotation.z=Range((e.angularVelocity-e.randomAngularVelocity)*Mathf.Deg2Rad,(e.angularVelocity+e.randomAngularVelocity)*Mathf.Deg2Rad);
            var force=ps.forceOverLifetime;force.enabled=e.force!=Vector3.zero || e.randomForce!=Vector3.zero;force.space=ParticleSystemSimulationSpace.World;force.randomized=true;
            Vector3 rnd=new Vector3(Mathf.Abs(e.randomForce.x),Mathf.Abs(e.randomForce.y),Mathf.Abs(e.randomForce.z));
            force.x=new ParticleSystem.MinMaxCurve(e.force.x-rnd.x,e.force.x+rnd.x);force.y=new ParticleSystem.MinMaxCurve(e.force.y-rnd.y,e.force.y+rnd.y);force.z=new ParticleSystem.MinMaxCurve(e.force.z-rnd.z,e.force.z+rnd.z);
            if(e.animateColor)
            {
                var gradient=new Gradient();gradient.SetKeys(Enumerable.Range(0,5).Select(i=>new GradientColorKey(e.colors[i],i/4f)).ToArray(),Enumerable.Range(0,5).Select(i=>new GradientAlphaKey(e.colors[i].a,i/4f)).ToArray());
                var color=ps.colorOverLifetime;color.enabled=true;color.color=gradient;
            }
            // Match the legacy upgrader's growth mapping; native random/size integration
            // differs across engines and remains a documented migration approximation.
            if(e.sizeGrow!=0)
            {
                var curve=new AnimationCurve();
                for(int i=0;i<=32;i++) {float t=i/32f;float v=e.sizeGrow>0?Mathf.Pow(1+e.sizeGrow,t*e.maxEnergy):Mathf.Pow(-e.sizeGrow,t);curve.AddKey(t,v);}
                var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,curve);
            }
            var sheet=ps.textureSheetAnimation;sheet.enabled=e.tilesX*e.tilesY>1;
            if(sheet.enabled) {sheet.numTilesX=e.tilesX;sheet.numTilesY=e.tilesY;sheet.animation=ParticleSystemAnimationType.WholeSheet;sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));sheet.cycleCount=(int)e.cycles;}
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=materials[e.material];
            renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.sortMode=ParticleSystemSortMode.None;
            renderer.sortingLayerID=e.sortingLayer;renderer.sortingOrder=e.sortingOrder;renderer.maxParticleSize=e.maxParticleSize;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            emitters.Add(new EscapeLegacyParticles.Emitter{goId=e.goId,system=ps,sizeGrow=e.sizeGrow});
        }
        controller.emitters=emitters.ToArray();controller.ResetParticles();
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Cannot save PASS16");AssetDatabase.SaveAssets();
        File.WriteAllText("Reports/pass16-scene.txt",output+"\nLegacy emitters: 5\nOriginal material modes: alpha, double multiply, cutout\n");
    }
    public static void Build()
    {
        if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene and leave Play Mode first");
        Directory.CreateDirectory("Builds/Windows-PASS16");var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Output},locationPathName="Builds/Windows-PASS16/Escape-PASS16.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        File.WriteAllText("Reports/pass16-build.txt",r.summary.result+"\nErrors: "+r.summary.totalErrors+"\n");if(r.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Build failed");
    }
    public static void RepairVectorCurves()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!=Output)throw new InvalidOperationException("Repair only the unverified PASS16 scene outside Play Mode");
        var d=JsonUtility.FromJson<Data>(File.ReadAllText("Assets/RecoveredParticlesPass16Local/particles.json"));
        var controller=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EscapeLegacyParticles>(true)).Single();
        foreach(var entry in controller.emitters)
        {
            var e=d.particles.Single(x=>x.goId==entry.goId);var ps=entry.system;
            Vector3 vel=e.worldVelocity+ps.transform.TransformDirection(e.localVelocity);
            var v=ps.velocityOverLifetime;
            v.x=new ParticleSystem.MinMaxCurve(vel.x-e.randomVelocity.x,vel.x+e.randomVelocity.x);
            v.y=new ParticleSystem.MinMaxCurve(vel.y-e.randomVelocity.y,vel.y+e.randomVelocity.y);
            v.z=new ParticleSystem.MinMaxCurve(vel.z-e.randomVelocity.z,vel.z+e.randomVelocity.z);
            var f=ps.forceOverLifetime;Vector3 r=new Vector3(Mathf.Abs(e.randomForce.x),Mathf.Abs(e.randomForce.y),Mathf.Abs(e.randomForce.z));
            f.x=new ParticleSystem.MinMaxCurve(e.force.x-r.x,e.force.x+r.x);
            f.y=new ParticleSystem.MinMaxCurve(e.force.y-r.y,e.force.y+r.y);
            f.z=new ParticleSystem.MinMaxCurve(e.force.z-r.z,e.force.z+r.z);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
}
#endif
