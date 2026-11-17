// Independent unlit vertex colour replacement; original source not included.
Shader "EscapeRecovery/OpaqueVertexColor"
{
 Properties { _Color ("Vertex tint", Color) = (1,1,1,1) }
 SubShader
 {
  Tags { "Queue"="Geometry" "RenderType"="Opaque" }
  Cull Back
  ZWrite On
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Color;
   struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
   struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR0; };
   v2f vert(appdata input)
   {
    v2f o;
    o.pos = UnityObjectToClipPos(input.vertex);
    o.color = input.color * _Color;
    return o;
   }
   fixed4 frag(v2f input) : SV_Target { return fixed4(input.color.rgb, 1); }
   ENDCG
  }
 }
 FallBack Off
}
