// Independent unlit colour shader; Culling Off supports two-sided silhouettes.
Shader "EscapeRecovery/SolidColor"
{
 Properties
 {
  _Color ("Recovered solid color", Color) = (1,1,1,1)
  _Cull ("Face culling (0=off, 2=back)", Float) = 2
 }
 SubShader
 {
  Tags { "Queue"="Geometry" "RenderType"="Opaque" }
  Cull [_Cull]
  ZWrite On
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Color;
   struct appdata { float4 vertex : POSITION; };
   struct v2f { float4 pos : SV_POSITION; };
   v2f vert(appdata input)
   {
    v2f o;
    o.pos = UnityObjectToClipPos(input.vertex);
    return o;
   }
   fixed4 frag(v2f input) : SV_Target { return fixed4(_Color.rgb, 1); }
   ENDCG
  }
 }
 FallBack Off
}
