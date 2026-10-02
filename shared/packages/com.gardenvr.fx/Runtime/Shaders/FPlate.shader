// Passthrough stand-ins. Plate: the real-room photo, drawn first, unlit. Catcher: an invisible desk plane that only darkens
// where the main light's shadow falls (the drawn dial's shadow on the real table) - multiplied over whatever is behind.
Shader "Fidelity/Plate"
{
    Properties { _MainTex ("Room plate", 2D) = "black" {} _Exposure ("Exposure", Float) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial) float4 _MainTex_ST; half _Exposure; CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert (A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }
            half4 frag (V i) : SV_Target { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Exposure; }
            ENDHLSL
        }
    }
}
