#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Visual pass 1: recover SpriteRenderer sprite references/color/order from the
/// user's local APK metadata. This code is NOT from the original game.
/// No copyrighted textures or scene data are stored in this source file.
/// </summary>
public static class EscapeSpritePass
{
    [Serializable]
    private sealed class SpriteManifest
    {
        public SpriteRecord[] sprites;
        public RendererRecord[] renderers;
    }

    [Serializable]
    private sealed class SpriteRecord
    {
        public int id;
        public string name;
        public string texturePath;
        public float[] textureRect;
        public float pixelsPerUnit;
    }

    [Serializable]
    private sealed class RendererRecord
    {
        public int id;
        public int goId;
        public int spriteId;
        public bool enabled;
        public int sortingOrder;
        public int sortingLayerId;
        public float[] color;
    }

    [MenuItem("Escape Recovery/3 - Restore 2D SpriteRenderers (visual pass 1)")]
    public static void RestoreSprites()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Escape recovery", "Exit Play Mode first.", "OK");
            return;
        }

        string hierarchyPath = Path.Combine(Application.streamingAssetsPath, "escape_hierarchy.json");
        string spritesPath = Path.Combine(Application.streamingAssetsPath, "escape_sprite_manifest.json");
        if (!File.Exists(hierarchyPath) || !File.Exists(spritesPath))
        {
            EditorUtility.DisplayDialog("Missing local recovery metadata",
                "Expected:\n" + hierarchyPath + "\nand\n" + spritesPath +
                "\n\nCopy the Stage 2 metadata archive into the PROJECT ROOT, " +
                "or run the local sprite manifest extractor.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || !scene.name.StartsWith("Escape_", StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog("Open the reconstructed scene",
                "First open Assets/Scenes/Escape_Structure_RECONSTRUCTED.unity. " +
                "Do not run this command in an empty scene.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Escape: restore sprites",
            "This will attempt to link SpriteRenderers to the original 35k-object " +
            "scene using a verified hierarchy mapping. It saves a NEW scene " +
            "Escape_Sprites_PASS1.unity and does not overwrite the structural scene.\n\n" +
            "Textures still in ETC2 will be skipped for now. Continue?", "Restore sprites", "Cancel"))
            return;

        try
        {
            EditorUtility.DisplayProgressBar("Escape graphics", "Reading local metadata...", 0);
            var sceneData = JsonUtility.FromJson<EscapeSceneRebuilder.SceneData>(File.ReadAllText(hierarchyPath));
            var manifest = JsonUtility.FromJson<SpriteManifest>(File.ReadAllText(spritesPath));
            if (sceneData == null || manifest == null || manifest.sprites == null || manifest.renderers == null)
                throw new InvalidDataException("Invalid or incomplete recovery JSON.");

            // Match original IDs to CURRENT Unity GameObjects before making any changes.
            // The Stage 1 rebuilder preserves root names and child sibling order.
            // A strict name/child-count check avoids silently assigning wrong images.
            var objects = MatchExistingScene(sceneData, scene);
            EditorUtility.DisplayProgressBar("Escape graphics", "Preparing recovered sprites...", 0.1f);

            if (!AssetDatabase.IsValidFolder("Assets/RecoveredSprites"))
                AssetDatabase.CreateFolder("Assets", "RecoveredSprites");

            var spriteById = new Dictionary<int, Sprite>();
            int sourceSuccess = 0, sourceMissing = 0;
            for (int i = 0; i < manifest.sprites.Length; i++)
            {
                SpriteRecord src = manifest.sprites[i];
                Sprite sprite = LoadOrCreateSprite(src);
                if (sprite != null) { spriteById[src.id] = sprite; sourceSuccess++; }
                else sourceMissing++;
                if (i % 5 == 0)
                    EditorUtility.DisplayProgressBar("Escape graphics", "Importing sprite images...",
                        0.1f + 0.45f * i / Math.Max(1, manifest.sprites.Length));
            }

            int applied = 0, skipped = 0;
            for (int i = 0; i < manifest.renderers.Length; i++)
            {
                RendererRecord entry = manifest.renderers[i];
                GameObject go;
                Sprite spr;
                if (!objects.TryGetValue(entry.goId, out go) ||
                    !spriteById.TryGetValue(entry.spriteId, out spr))
                {
                    skipped++;
                    continue;
                }

                SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = spr;
                sr.enabled = entry.enabled;
                sr.sortingOrder = entry.sortingOrder;
                if (entry.color != null && entry.color.Length == 4)
                    sr.color = new Color(entry.color[0], entry.color[1], entry.color[2], entry.color[3]);

                // Original nondefault SortingLayer IDs are hashes. The matching
                // original layer names/settings have not yet been reconstructed.
                applied++;
                if (i % 200 == 0)
                    EditorUtility.DisplayProgressBar("Escape graphics", "Assigning SpriteRenderers...",
                        0.55f + 0.4f * i / Math.Max(1, manifest.renderers.Length));
            }

            if (applied == 0)
                throw new InvalidOperationException("No SpriteRenderers were linked. Check PNG extraction and manifest paths.");

            const string outputScene = "Assets/Scenes/Escape_Sprites_PASS1.unity";
            EditorUtility.DisplayProgressBar("Escape graphics", "Saving new preview scene...", 0.96f);
            if (!EditorSceneManager.SaveScene(scene, outputScene, false))
                throw new IOException("Unity failed to save " + outputScene);

            Debug.Log("Escape sprite recovery: sprite assets " + sourceSuccess +
                      ", missing source sprites " + sourceMissing +
                      "; SpriteRenderers restored " + applied + ", skipped " + skipped +
                      ". Original SortingLayer IDs and meshes are NOT restored.");
            EditorUtility.DisplayDialog("Visual pass 1 complete",
                "Sprite images created: " + sourceSuccess + " / " + manifest.sprites.Length +
                "\nSpriteRenderers assigned: " + applied + " / " + manifest.renderers.Length +
                "\nMissing / compressed: " + skipped +
                "\n\nSaved: " + outputScene +
                "\n\nThis is still NOT a playable reconstruction. ETC2 images, meshes, cameras, " +
                "sorting layers, shaders, animations and gameplay remain incomplete.", "OK");
        }
        catch (Exception exc)
        {
            Debug.LogException(exc);
            EditorUtility.DisplayDialog("Sprite recovery failed", exc.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static Dictionary<int, GameObject> MatchExistingScene(
        EscapeSceneRebuilder.SceneData original, Scene scene)
    {
        var goById = new Dictionary<int, EscapeSceneRebuilder.GameObjectRecord>();
        foreach (var go in original.gameObjects) goById[go.id] = go;

        var transformByGo = new Dictionary<int, EscapeSceneRebuilder.TransformRecord>();
        foreach (var t in original.transforms) transformByGo[t.goId] = t;

        var childrenByParent = new Dictionary<int, List<int>>();
        foreach (var t in original.transforms)
        {
            List<int> children;
            if (!childrenByParent.TryGetValue(t.parentId, out children))
            {
                children = new List<int>();
                childrenByParent.Add(t.parentId, children);
            }
            children.Add(t.goId);
        }

        var actualRoots = new Dictionary<string, GameObject>();
        foreach (var go in scene.GetRootGameObjects())
        {
            // The Stage 1 builder adds two diagnostic roots after the originals.
            if (go.name == "Recovery preview camera" ||
                go.name.StartsWith("[ARCHIVAL RECONSTRUCTION", StringComparison.Ordinal))
                continue;
            if (actualRoots.ContainsKey(go.name))
                throw new InvalidDataException("Duplicate root in current scene: " + go.name);
            actualRoots.Add(go.name, go);
        }

        var matched = new Dictionary<int, GameObject>(original.gameObjects.Length);
        var stack = new Stack<KeyValuePair<int, Transform>>();
        int rootCount = 0;
        foreach (var go in original.gameObjects)
        {
            EscapeSceneRebuilder.TransformRecord t;
            if (!transformByGo.TryGetValue(go.id, out t))
                throw new InvalidDataException("Object without transform: " + go.name);
            if (t.parentId != 0) continue;

            GameObject actual;
            if (!actualRoots.TryGetValue(go.name, out actual))
                throw new InvalidDataException("Missing root: " + go.name);
            stack.Push(new KeyValuePair<int, Transform>(go.id, actual.transform));
            rootCount++;
        }

        if (rootCount != actualRoots.Count)
            throw new InvalidDataException("Hierarchy differs: " + rootCount +
                " expected roots, " + actualRoots.Count + " actual roots.");

        while (stack.Count > 0)
        {
            var pair = stack.Pop();
            int goId = pair.Key;
            Transform actual = pair.Value;
            EscapeSceneRebuilder.GameObjectRecord expected;
            if (!goById.TryGetValue(goId, out expected) || actual.name != expected.name)
                throw new InvalidDataException("Object name/order mismatch near " + actual.name);

            matched.Add(goId, actual.gameObject);
            int transformId = transformByGo[goId].id;
            List<int> originalChildren;
            if (!childrenByParent.TryGetValue(transformId, out originalChildren))
                originalChildren = new List<int>();

            if (actual.childCount != originalChildren.Count)
                throw new InvalidDataException("Child count mismatch on " + expected.name +
                    " [" + goId + "]: expected " + originalChildren.Count +
                    ", found " + actual.childCount);

            for (int i = originalChildren.Count - 1; i >= 0; i--)
            {
                int childId = originalChildren[i];
                if (!goById.ContainsKey(childId) ||
                    actual.GetChild(i).name != goById[childId].name)
                    throw new InvalidDataException("Sibling order mismatch under " + expected.name +
                        ". Use the original Escape_Structure_RECONSTRUCTED scene.");
                stack.Push(new KeyValuePair<int, Transform>(childId, actual.GetChild(i)));
            }
        }

        if (matched.Count != original.gameObjects.Length)
            throw new InvalidDataException("Mapped only " + matched.Count + " / " +
                original.gameObjects.Length + " objects.");
        return matched;
    }

    private static Sprite LoadOrCreateSprite(SpriteRecord src)
    {
        if (string.IsNullOrEmpty(src.texturePath)) return null;  // ETC2, not decoded yet
        string sourcePath = src.texturePath.Replace('\\', '/');
        if (!sourcePath.StartsWith("Assets/RecoveredTextures/", StringComparison.Ordinal) ||
            !sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            return null;

        string assetPath = "Assets/RecoveredSprites/escape_sprite_" + src.id + ".png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (existing != null) return existing;

        var sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
        if (sourceImporter == null) return null;
        // Keep original pixel resolution; we use exact pixel coordinates from Unity 4.
        if (!sourceImporter.isReadable || sourceImporter.npotScale != TextureImporterNPOTScale.None ||
            sourceImporter.textureCompression != TextureImporterCompression.Uncompressed)
        {
            sourceImporter.isReadable = true;
            sourceImporter.npotScale = TextureImporterNPOTScale.None;
            sourceImporter.textureCompression = TextureImporterCompression.Uncompressed;
            sourceImporter.maxTextureSize = 8192;
            sourceImporter.SaveAndReimport();
        }

        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
        if (source == null || src.textureRect == null || src.textureRect.Length != 4) return null;
        var coords = src.textureRect;
        int x = Mathf.RoundToInt(coords[0]);
        int y = Mathf.RoundToInt(coords[1]);
        int w = Mathf.RoundToInt(coords[2]);
        int h = Mathf.RoundToInt(coords[3]);
        if (x < 0 || y < 0 || w < 1 || h < 1 || x + w > source.width || y + h > source.height)
        {
            Debug.LogWarning("Escape: invalid crop for sprite " + src.name +
                             " (texture " + source.name + "): " + x + "," + y + "," + w + "," + h);
            return null;
        }

        var target = new Texture2D(w, h, TextureFormat.RGBA32, false);
        try
        {
            target.SetPixels(source.GetPixels(x, y, w, h));
            target.Apply();
            string root = Directory.GetParent(Application.dataPath).FullName;
            string diskPath = Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(diskPath, target.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(target); }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return null;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = src.pixelsPerUnit > 0 ? src.pixelsPerUnit : 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }
}
#endif
