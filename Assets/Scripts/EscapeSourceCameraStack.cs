using UnityEngine;

/// <summary>Restores the source layer-17 perspective pass inside the PC camera.</summary>
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(1000)]
public sealed class EscapeSourceCameraStack : MonoBehaviour
{
    public Camera sky,perspective;public float sourceFov,sourceZ,skySizeRatio;
    private void OnEnable(){Synchronize();}
    private void LateUpdate(){Synchronize();}
    private void OnDisable(){if(sky!=null)sky.enabled=false;if(perspective!=null)perspective.enabled=false;}
    public void Synchronize()
    {
        if(sky==null || perspective==null)return;
        var main=GetComponent<Camera>();
        foreach(var c in new[]{sky,perspective})
        {
            c.targetTexture=main.targetTexture;c.rect=main.rect;c.aspect=main.aspect;
            c.transform.SetPositionAndRotation(main.transform.position,main.transform.rotation);
            // These unoccluded 2D layers must use their own perspective frustum.
            c.useOcclusionCulling=false;c.enabled=main.enabled;
        }
        sky.depth=main.depth-2;perspective.depth=main.depth-1;
        sky.orthographicSize=main.orthographicSize*skySizeRatio;
        perspective.transform.position=new Vector3(main.transform.position.x,main.transform.position.y,sourceZ);
        // Keep the verified PC foreground zoom while preserving the source
        // perspective/foreground size ratio (source main camera half-height 4.05).
        perspective.fieldOfView=2*Mathf.Atan(Mathf.Tan(sourceFov*Mathf.Deg2Rad*.5f)*main.orthographicSize/4.05f)*Mathf.Rad2Deg;
        // Let Unity own the render/culling job lifetime in camera depth order.
        // Do not re-enter camera rendering from a culling callback.
    }
}
