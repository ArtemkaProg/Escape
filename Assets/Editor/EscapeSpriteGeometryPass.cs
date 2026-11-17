#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Escape 2015 visual pass 4: repair imported Sprite pivots and draw the original
/// nine complex sprite polygons as saved Mesh assets. This is an independently
/// authored recovery tool, NOT the original game's source.
/// All original sprite vertices, triangles and pivots are supplied as a LOCAL
/// JSON extracted from the user's APK, never uploaded to public GitHub.
/// </summary>
public static class EscapeSpriteGeometryPass
{
    [Serializable]
    private sealed class GeometryManifest
    {
        public string unityVersion;
        public GeometryRecord[] sprites;
    }

    [Serializable]
    private sealed class GeometryRecord
    {
        public int id;
        public string name;
        public int vertexCount;
        public int triangleCount;
        public float ppu;
        public float[] pivot;
        public float[] vertices; // local X,Y pairs, only for polygons
        public float[] uv;       // normalized U,V pairs, only for polygons
        public int[] triangles;  // original Unity triangle indices, only for polygons
    }

    [Serializable]
    private sealed class OriginalHierarchy
    {
        public EscapeSceneRebuilder.GameObjectRecord[] gameObjects;
        public EscapeSceneRebuilder.TransformRecord[] transforms;
    }

    [Serializable]
    private sealed class SpriteManifest
    {
        public RendererRecord[] renderers;
    }

    [Serializable]
    private sealed class RendererRecord
    {
        public int goId;
        public int spriteId;
    }

    private const string OriginalSpriteDir = "Assets/RecoveredSprites";
    private const string FixedSpriteDir = "Assets/RecoveredSpritesV2";
    private const string MeshDir = "Assets/RecoveredPolygonMeshes";
    private const string ScenePreview = "Escape_Zone001_PREVIEW";
    private const string SceneCameras = "Escape_Cameras_PASS3";

    [MenuItem("Escape Recovery/7 - Fix sprite pivots and polygons (pass 4)")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Escape recovery", "Exit Play Mode first.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            (scene.name != ScenePreview && scene.name != SceneCameras))
        {
            EditorUtility.DisplayDialog("Open a Stage 6 scene",
                "Open Escape_Zone001_PREVIEW.unity (recommended) " +
                "or Escape_Cameras_PASS3.unity before running this command.", "OK");
            return;
        }

        string folder = Application.streamingAssetsPath;
        string geometryPath = Path.Combine(folder, "escape_sprite_geometry.json");
        string hierarchyPath = Path.Combine(folder, "escape_hierarchy.json");
        string spritesPath = Path.Combine(folder, "escape_sprite_manifest.json");
        if (!File.Exists(geometryPath) || !File.Exists(hierarchyPath) ||
            !File.Exists(spritesPath))
        {
            EditorUtility.DisplayDialog("Missing local Stage 7 metadata",
                "Missing a required JSON in Assets/StreamingAssets.\n\n" +
                "Download Escape_2015_Stage7_Sprite_Geometry.zip and extract it " +
                "into the Escape project root. Existing PASS3/preview scenes " +
                "are not included in that ZIP.", "OK");
            return;
        }

        string outputPath = scene.name == ScenePreview
            ? "Assets/Scenes/Escape_Zone001_GRAPHICS_PASS4.unity"
            : "Assets/Scenes/Escape_Cameras_GRAPHICS_PASS4.unity";

        if (!EditorUtility.DisplayDialog("Escape sprite geometry — PASS4",
            "This corrects original Sprite pivots and replaces 59 complex " +
            "SpriteRenderer instances with polygon meshes. The 89 source PNGs " +
            "are copied into a NEW directory so older scenes and assets remain " +
            "unchanged.\n\nOutput: " + outputPath +
            "\n\nThe visual result is still not the original playable game. Continue?",
            "Apply Stage 7", "Cancel"))
            return;

        try
        {
            EditorUtility.DisplayProgressBar("Escape Stage 7", "Reading local metadata...", 0f);
            GeometryManifest geometry = JsonUtility.FromJson<GeometryManifest>(
                File.ReadAllText(geometryPath));
            EscapeSceneRebuilder.SceneData hierarchy =
                JsonUtility.FromJson<EscapeSceneRebuilder.SceneData>(
                    File.ReadAllText(hierarchyPath));
            SpriteManifest spriteManifest =
                JsonUtility.FromJson<SpriteManifest>(File.ReadAllText(spritesPath));

            if (geometry == null || geometry.unityVersion != "4.6.5f1" ||
                geometry.sprites == null || geometry.sprites.Length != 89 ||
                hierarchy == null || hierarchy.gameObjects == null ||
                spriteManifest == null || spriteManifest.renderers == null)
                throw new InvalidDataException("Stage 7 data does not match the expected Unity 4.6.5f1 APK.");

            // A strict, source-derived mapping avoids assigning a polygon to the
            // wrong GameObject even when thousands of objects have the same name.
            var mappedObjects = EscapeSpritePass.MatchExistingScene(hierarchy, scene);

            EnsureFolder(FixedSpriteDir);
            EnsureFolder(MeshDir);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            var geometryById = new Dictionary<int, GeometryRecord>();
            var spriteById = new Dictionary<int, Sprite>();
            var polygonMeshById = new Dictionary<int, Mesh>();
            int customPivots = 0, polygonDefinitions = 0;

            for (int i = 0; i < geometry.sprites.Length; i++)
            {
                GeometryRecord rec = geometry.sprites[i];
                if (geometryById.ContainsKey(rec.id))
                    throw new InvalidDataException("Duplicate Sprite ID " + rec.id);
                if (rec.pivot == null || rec.pivot.Length != 2 || rec.ppu <= 0 ||
                    rec.vertexCount < 3)
                    throw new InvalidDataException("Invalid source Sprite " + rec.id);
                geometryById.Add(rec.id, rec);

                string originalPath = OriginalSpriteDir + "/escape_sprite_" + rec.id + ".png";
                string fixedPath = FixedSpriteDir + "/escape_sprite_" + rec.id + ".png";
                string sourceDisk = Path.Combine(projectRoot, originalPath.Replace('/',
                    Path.DirectorySeparatorChar));
                string targetDisk = Path.Combine(projectRoot, fixedPath.Replace('/',
                    Path.DirectorySeparatorChar));

                if (!File.Exists(sourceDisk))
                    throw new FileNotFoundException("Stage 2/5 sprite crop is missing", sourceDisk);
                if (!File.Exists(targetDisk))
                    File.Copy(sourceDisk, targetDisk);

                AssetDatabase.ImportAsset(fixedPath, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = AssetImporter.GetAtPath(fixedPath) as TextureImporter;
                if (importer == null)
                    throw new InvalidDataException("Could not import " + fixedPath);

                var exactPivot = new Vector2(Mathf.Clamp01(rec.pivot[0]),
                    Mathf.Clamp01(rec.pivot[1]));
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                // Unity 6: alignment is on TextureImporterSettings, not TextureImporter.
                // Read the current settings to preserve unrelated import options.
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
                textureSettings.spritePivot = exactPivot;
                importer.SetTextureSettings(textureSettings);
                importer.spritePixelsPerUnit = rec.ppu;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                Sprite fixedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(fixedPath);
                if (fixedSprite == null)
                    throw new InvalidDataException("No Sprite found in " + fixedPath);

                if (Vector2.Distance(exactPivot, new Vector2(.5f, .5f)) > .03f)
                    customPivots++;
                spriteById.Add(rec.id, fixedSprite);

                if (rec.vertexCount > 4)
                {
                    polygonDefinitions++;
                    polygonMeshById[rec.id] = GetOrCreatePolygonMesh(rec);
                }

                EditorUtility.DisplayProgressBar("Escape Stage 7",
                    "Copying sprites and restoring exact pivot positions...",
                    0.05f + 0.65f * (i + 1) / geometry.sprites.Length);
            }

            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader == null)
                throw new InvalidOperationException(
                    "The built-in Sprites/Default shader is missing from this Unity installation.");

            int assigned = 0, polygonsApplied = 0, skipped = 0;
            for (int i = 0; i < spriteManifest.renderers.Length; i++)
            {
                RendererRecord rec = spriteManifest.renderers[i];
                GameObject go;
                Sprite sprite;
                if (!mappedObjects.TryGetValue(rec.goId, out go) ||
                    !spriteById.TryGetValue(rec.spriteId, out sprite))
                {
                    skipped++;
                    continue;
                }

                SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                if (sr == null)
                {
                    skipped++;
                    continue;
                }

                sr.sprite = sprite;
                assigned++;

                Mesh polygon;
                if (polygonMeshById.TryGetValue(rec.spriteId, out polygon))
                {
                    // SpriteRenderer's rectangular rendering was incorrect for
                    // the original "tight" polygons. Preserve its data, but
                    // render the exact original mesh through MeshRenderer.
                    MeshFilter mf = go.GetComponent<MeshFilter>();
                    if (mf == null) mf = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = polygon;

                    MeshRenderer mr = go.GetComponent<MeshRenderer>();
                    if (mr == null) mr = go.AddComponent<MeshRenderer>();
                    string matPath = MeshDir + "/escape_mat_" + rec.spriteId +
                        "_" + rec.goId + ".mat";
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (material == null)
                    {
                        material = new Material(spriteShader);
                        material.name = "Escape " + sprite.name + " " + rec.goId;
                        AssetDatabase.CreateAsset(material, matPath);
                    }

                    material.shader = spriteShader;
                    material.SetTexture("_MainTex", sprite.texture);
                    material.SetColor("_Color", sr.color);
                    EditorUtility.SetDirty(material);
                    mr.sharedMaterial = material;
                    mr.sortingLayerID = sr.sortingLayerID;
                    mr.sortingOrder = sr.sortingOrder;
                    mr.enabled = sr.enabled;
                    sr.enabled = false;
                    polygonsApplied++;
                }

                if (i % 200 == 0)
                    EditorUtility.DisplayProgressBar("Escape Stage 7",
                        "Applying corrected sprite pivots and meshes...",
                        0.70f + 0.25f * i / Math.Max(1, spriteManifest.renderers.Length));
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayProgressBar("Escape Stage 7",
                "Saving a separate diagnostic scene...", 0.97f);
            if (!EditorSceneManager.SaveScene(scene, outputPath, false))
                throw new IOException("Failed to save " + outputPath);

            Debug.Log("[Escape Stage 7] Corrected source sprite assets: " +
                spriteById.Count + "; special pivots: " + customPivots +
                "; original polygon types: " + polygonDefinitions +
                "; corrected SpriteRenderers: " + assigned +
                "; polygon MeshRenderers: " + polygonsApplied +
                "; skipped SpriteRenderers: " + skipped +
                ". Source sprites and previous scenes remain intact.");

            EditorUtility.DisplayDialog("Escape Stage 7 completed",
                "Sprite assets copied: " + spriteById.Count +
                "\nNoncentral pivots: " + customPivots +
                "\nPolygon sprite types: " + polygonDefinitions +
                "\nSpriteRenderers updated: " + assigned +
                "\nPolygon MeshRenderers: " + polygonsApplied +
                "\nSkipped: " + skipped +
                "\n\nSaved: " + outputPath +
                "\n\nThis is a diagnostic visual pass, not a gameplay restoration.",
                "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Stage 7 failed", e.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;
        int slash = assetPath.LastIndexOf('/');
        string parent = assetPath.Substring(0, slash);
        string name = assetPath.Substring(slash + 1);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static Mesh GetOrCreatePolygonMesh(GeometryRecord rec)
    {
        if (rec.vertices == null || rec.uv == null || rec.triangles == null ||
            rec.vertices.Length != rec.vertexCount * 2 ||
            rec.uv.Length != rec.vertexCount * 2 ||
            rec.triangles.Length != rec.triangleCount * 3)
            throw new InvalidDataException("Malformed original polygon " + rec.name);

        foreach (int index in rec.triangles)
            if (index < 0 || index >= rec.vertexCount)
                throw new InvalidDataException("Out-of-range triangle in " + rec.name);

        string path = MeshDir + "/escape_polygon_" + rec.id + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        var mesh = new Mesh();
        mesh.name = "Escape original polygon " + rec.name;
        var verts = new Vector3[rec.vertexCount];
        var uv = new Vector2[rec.vertexCount];
        for (int i = 0; i < rec.vertexCount; ++i)
        {
            verts[i] = new Vector3(rec.vertices[i * 2],
                rec.vertices[i * 2 + 1], 0f);
            uv[i] = new Vector2(rec.uv[i * 2], rec.uv[i * 2 + 1]);
        }
        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.triangles = rec.triangles;
        mesh.RecalculateBounds();

        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
}
#endif
