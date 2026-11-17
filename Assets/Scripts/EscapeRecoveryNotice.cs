using UnityEngine;

// New diagnostic placeholder, not a decompiled original script.
public sealed class EscapeRecoveryNotice : MonoBehaviour
{
    private void OnGUI()
    {
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(24, 24, 660, 150));
        GUILayout.Label("ESCAPE (2015) - archival reconstruction", GUI.skin.box);
        GUILayout.Label("Scene GameObjects and transforms restored from Unity 4.6.5f1 APK.");
        GUILayout.Label("This is NOT the original playable game: scripts, renderers and physics still require porting.");
        GUILayout.EndArea();
    }
}
