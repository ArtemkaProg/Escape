Shader "EscapeRecovery/LegacyParticle"
{
    Properties
    {
        _MainTex ("Original texture",2D)="white" {}
        _Cutoff ("Cutout",Float)=.5
        _Mode ("Mode",Float)=0
        _SrcBlend ("Source blend",Float)=5
        _DstBlend ("Destination blend",Float)=10
        _ZWrite ("Depth write",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite [_ZWrite] Blend [_SrcBlend] [_DstBlend]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; float _Cutoff,_Mode;
            struct Input {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct Output {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            Output vert(Input v) {Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o;}
            fixed4 frag(Output i):SV_Target
            {
                fixed4 t=tex2D(_MainTex,i.uv);
                // Original Unlit/Transparent Cutout ignores particle vertex colors.
                if(_Mode>1.5) {clip(t.a-_Cutoff);return t;}
                fixed4 c=t*i.color;
                // Original two-stage fixed-function multiply: neutral at alpha zero.
                if(_Mode>.5) return fixed4(lerp(fixed3(.5,.5,.5),2*c.rgb,c.a),c.a);
                return c;
            }
            ENDCG
        }
    }
}
