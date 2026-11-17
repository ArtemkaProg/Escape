// Independent transparent vertex-colour replacement; original source not included.
Shader "EscapeRecovery/TransparentVertexColor"
{
 Properties { _Color ("Vertex tint", Color) = (1,1,1,1) }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Cull Back
  ZWrite Off
  Blend SrcAlpha OneMinusSrcAlpha
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
   fixed4 frag(v2f input) : SV_Target
   {
    clip(input.color.a - 0.001);
    return input.color;
   }
   ENDCG
  }
 }
 FallBack Off
}
