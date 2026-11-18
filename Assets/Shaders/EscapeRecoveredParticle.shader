Shader "EscapeRecovery/Particle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint", Color) = (.5,.5,.5,.5)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off Cull Off ColorMask RGB
            Blend SrcAlpha [_DstBlend]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _TintColor;
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=TRANSFORM_TEX(v.uv,_MainTex); return o; }
            fixed4 frag(Output i):SV_Target { fixed4 c=2*i.color*_TintColor*tex2D(_MainTex,i.uv); clip(c.a-.01); return c; }
            ENDCG
        }
    }
}
