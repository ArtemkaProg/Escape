using UnityEngine;

/// <summary>Restores the source layer-17 perspective pass inside the PC camera.</summary>
[RequireComponent(typeof(Camera))]
public sealed class EscapeSourceCameraStack : MonoBehaviour
{
    public Camera sky,perspective;public float sourceFov,sourceZ,skySizeRatio;
    private void OnPreCull()
    {
        var main=GetComponent<Camera>();
        foreach(var c in new[]{sky,perspective})
        {
            c.targetTexture=main.targetTexture;c.rect=main.rect;c.aspect=main.aspect;
            c.transform.SetPositionAndRotation(main.transform.position,main.transform.rotation);
        }
        sky.orthographicSize=main.orthographicSize*skySizeRatio;
        perspective.transform.position=new Vector3(main.transform.position.x,main.transform.position.y,sourceZ);
        // Keep the verified PC foreground zoom while preserving the source
        // perspective/foreground size ratio (source main camera half-height 4.05).
        perspective.fieldOfView=2*Mathf.Atan(Mathf.Tan(sourceFov*Mathf.Deg2Rad*.5f)*main.orthographicSize/4.05f)*Mathf.Rad2Deg;
        sky.Render();perspective.Render();
    }
}
