// Independently authored diagnostic version of the recovered inverted background shader.
// This DOES NOT replace the Stage 10 shader or copy proprietary game shader source.
Shader "EscapeRecovery/InvertedTextureTunable"
{
 Properties
 {
  _MainTex ("Recovered Texture", 2D) = "white" {}
  _Color ("Recovered Color", Color) = (1,1,1,1)
  _RecoveryBrightness ("Diagnostic Brightness", Range(0.65,1.65)) = 1
  _RecoveryHaze ("Diagnostic Haze", Range(0,0.5)) = 0
  _RecoveryHazeColor ("Diagnostic Haze Tint", Color) = (0.83,0.86,0.82,1)
 }
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
   float _RecoveryBrightness;
   float _RecoveryHaze;
   fixed4 _RecoveryHazeColor;

   struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
   struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 tint : COLOR0; };

   v2f vert(appdata v)
   {
     v2f o;
     o.pos = UnityObjectToClipPos(v.vertex);
     o.uv = TRANSFORM_TEX(v.uv, _MainTex);
     o.tint = v.color * _Color;
     return o;
   }
   fixed4 frag(v2f i) : SV_Target
   {
     fixed4 texel = tex2D(_MainTex, i.uv);
     fixed4 c;
     fixed3 reconstructed = saturate((1.0 - texel.rgb) * i.tint.rgb * _RecoveryBrightness);
     c.rgb = lerp(reconstructed, _RecoveryHazeColor.rgb, saturate(_RecoveryHaze));
     c.a = texel.a * i.tint.a;
     clip(c.a - 0.001);
     return c;
   }
   ENDCG
  }
 }
 FallBack Off
}
