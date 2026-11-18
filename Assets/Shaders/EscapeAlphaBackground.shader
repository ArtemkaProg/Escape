// Alpha-only original masks carry no RGB image; preserve source vertex gradients.
Shader "EscapeRecovery/AlphaBackground"
{
 Properties {_MainTex ("Original alpha mask",2D)="white" {}}
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent"}
  Cull Back ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _MainTex_ST;
   struct Input{float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   struct Output{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   Output vert(Input i){Output o;o.pos=UnityObjectToClipPos(i.vertex);o.uv=TRANSFORM_TEX(i.uv,_MainTex);o.color=i.color;return o;}
   fixed4 frag(Output i):SV_Target{fixed4 c=i.color;c.a*=tex2D(_MainTex,i.uv).a;clip(c.a-.001);return c;}
   ENDCG
  }
 }
}
