#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Read-only index of original animation assets and RocketChips C# method names.
/// No Unity 6 animation clips/state machines or gameplay are created here.
/// Original binary assets stay local and outside the public GitHub repository.
/// </summary>
public sealed class EscapeAnimationInventoryWindow : EditorWindow
{
    [Serializable] private class Inventory
    {
        public string unityVersion;
        public string format;
        public Clip[] animationClips;
        public Controller[] animatorControllers;
        public AnimatorItem[] animators;
        public ScriptClass[] rocketControlMethodNames;
    }
    [Serializable] private class Clip
    {
        public int fileId;
        public int id;
        public string name;
        public int serializedBytes;
    }
    [Serializable] private class Controller
    {
        public int fileId;
        public int id;
        public string name;
    }
    [Serializable] private class AnimatorItem
    {
        public int id;
        public int goId;
        public string goPath;
        public int controllerId;
        public string controllerName;
    }
    [Serializable] private class ScriptClass
    {
        public string className;
        public string[] methodNames;
    }

    private static readonly string[] Tabs =
        {"Clips", "Controllers", "Scene Animators", "Rocket Methods"};
    private Inventory inventory;
    private string error;
    private string searchText = "";
    private int tab;
    private Vector2 scroll;

    [MenuItem("Escape Recovery/15 - Inspect original animations and rocket (read-only)")]
    public static void Open()
    {
        var w = GetWindow<EscapeAnimationInventoryWindow>("Escape Animation Audit");
        w.minSize = new Vector2(580, 340);
        w.Show();
    }

    private void OnEnable()
    {
        Load();
    }

    private void Load()
    {
        error = null;
        inventory = null;
        string path = Path.Combine(Application.streamingAssetsPath,
            "escape_animation_inventory.json");
        if (!File.Exists(path))
        {
            error = "No Stage 12 inventory found. Extract the Stage 12 ZIP into " +
                "the Escape project root or run " +
                "py -3 tools/extract_animation_inventory.py.";
            return;
        }
        try
        {
            Inventory value = JsonUtility.FromJson<Inventory>(
                File.ReadAllText(path));
            if (value == null || value.format != "ESCANIM1" ||
                value.unityVersion != "4.6.5f1" ||
                value.animationClips == null ||
                value.animationClips.Length != 152 ||
                value.animatorControllers == null ||
                value.animatorControllers.Length != 108 ||
                value.animators == null || value.animators.Length != 1408 ||
                value.rocketControlMethodNames == null)
                throw new InvalidDataException("Invalid Escape animation inventory");
            inventory = value;
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
    }

    private static bool Matches(string s, string filter)
    {
        return string.IsNullOrEmpty(filter) ||
            (!string.IsNullOrEmpty(s) &&
             s.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Escape 2015 — verified animation assets",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Read-only original Unity 4.6.5f1 asset metadata. " +
            "No animation curves, controller state machines or playable ship " +
            "behaviour have been imported into Unity 6.", MessageType.Info);
        if (inventory == null)
        {
            EditorGUILayout.HelpBox(error ?? "Data unavailable", MessageType.Warning);
            if (GUILayout.Button("Retry loading")) Load();
            return;
        }

        EditorGUILayout.LabelField(
            "152 clips | 108 controllers | 1408 scene Animators | 824 linked",
            EditorStyles.miniBoldLabel);
        tab = GUILayout.Toolbar(tab, Tabs);
        searchText = EditorGUILayout.TextField("Search", searchText);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            switch (tab)
            {
                case 0:
                    foreach (Clip c in inventory.animationClips)
                        if (Matches(c.name, searchText))
                            EditorGUILayout.LabelField(
                                c.name + "  [" + c.fileId + ":" + c.id + "]");
                    break;
                case 1:
                    foreach (Controller c in inventory.animatorControllers)
                        if (Matches(c.name, searchText))
                            EditorGUILayout.LabelField(
                                c.name + "  [" + c.fileId + ":" + c.id + "]");
                    break;
                case 2:
                    int shown = 0;
                    foreach (AnimatorItem a in inventory.animators)
                    {
                        if (!Matches(a.goPath, searchText) &&
                            !Matches(a.controllerName, searchText))
                            continue;
                        EditorGUILayout.LabelField("GO #" + a.goId + " " +
                            a.goPath + " | " +
                            (a.controllerId == 0 ? "No controller" : a.controllerName));
                        if (++shown >= 100) break;
                    }
                    if (shown == 100)
                        EditorGUILayout.HelpBox(
                            "Showing first 100 matches. Use Search to narrow results.",
                            MessageType.Info);
                    break;
                case 3:
                    foreach (ScriptClass s in inventory.rocketControlMethodNames)
                    {
                        bool visible = Matches(s.className, searchText);
                        if (!visible && s.methodNames != null)
                            foreach (string name in s.methodNames)
                                if (Matches(name, searchText))
                                {
                                    visible = true;
                                    break;
                                }
                        if (!visible) continue;
                        EditorGUILayout.LabelField(s.className, EditorStyles.boldLabel);
                        if (s.methodNames == null) continue;
                        foreach (string name in s.methodNames)
                            if (Matches(s.className, searchText) ||
                                Matches(name, searchText))
                                EditorGUILayout.LabelField("    " + name);
                    }
                    break;
            }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
