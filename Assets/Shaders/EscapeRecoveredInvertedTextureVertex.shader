// Independently implemented from observed blend behaviour; NOT original source.
Shader "EscapeRecovery/InvertedTextureVertex"
{
 Properties { _MainTex ("Recovered texture", 2D) = "white" {} _Color ("Color multiplier", Color) = (1,1,1,1) }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Cull Back
  ZWrite Off
  Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   float4 _MainTex_ST;
   fixed4 _Color;
   struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
   struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR0; };
   v2f vert(appdata input)
   {
    v2f o;
    o.pos = UnityObjectToClipPos(input.vertex);
    o.uv = TRANSFORM_TEX(input.uv, _MainTex);
    o.color = input.color * _Color;
    return o;
   }
   fixed4 frag(v2f input) : SV_Target
   {
    fixed4 texel = tex2D(_MainTex, input.uv);
    fixed4 pixel;
    pixel.rgb = (1.0 - texel.rgb) * input.color.rgb;
    pixel.a = texel.a * input.color.a;
    clip(pixel.a - 0.001);
    return pixel;
   }
   ENDCG
  }
 }
 FallBack Off
}
