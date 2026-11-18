using System;
using UnityEngine;
var game=UnityEngine.Object.FindFirstObjectByType<EscapePlayableZone>();var c=game.gameCamera;var s=c.GetComponent<EscapeSourceCameraStack>();
var cp=c.transform.position;var rt=new RenderTexture(320,180,24);var original=c.targetTexture;float aspect=c.aspect;var skyPos=s.sky.transform.position;var bgPos=s.perspective.transform.position;
try
{
 c.targetTexture=rt;c.aspect=16f/9;Vector3 point=cp+new Vector3(3,0,0);point.z=2;
 c.Render();float main0=c.WorldToViewportPoint(point).x,bg0=s.perspective.WorldToViewportPoint(point).x;
 c.transform.position=cp+Vector3.right*4;c.Render();float main1=c.WorldToViewportPoint(point).x,bg1=s.perspective.WorldToViewportPoint(point).x;
 float ratio=(bg1-bg0)/(main1-main0);
 if(!(ratio>.5f && ratio<1))throw new Exception("Background does not lag foreground: "+ratio);
 var result=new{parallaxRatio=ratio,relativeRightMotion=true,adaptedFov=s.perspective.fieldOfView};System.IO.File.WriteAllText("Reports/pass17-parallax.txt",result.ToString());return result;
}
finally{c.transform.position=cp;c.targetTexture=original;c.aspect=aspect;s.sky.targetTexture=original;s.perspective.targetTexture=original;s.sky.transform.position=skyPos;s.perspective.transform.position=bgPos;UnityEngine.Object.DestroyImmediate(rt);}
