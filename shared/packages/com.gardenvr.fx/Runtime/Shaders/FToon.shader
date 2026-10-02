// The drawn dial. Painted albedo x a two-step toon ramp (lit / shade, hard but slightly feathered), real-time main-light
// shadows quantised into the same two steps (so the gnomon throws a painted, not a rendered, shadow), paper grain, and a
// second pass that draws the inverted-hull ink outline with boiling width.
Shader "Fidelity/Toon"
{
    Properties
    {
        _MainTex ("Painted albedo", 2D) = "white" {}
        _Lit ("Lit colour", Color) = (1.0, 0.97, 0.90, 1)
        _Shade ("Shade colour", Color) = (0.72, 0.68, 0.74, 1)
        _Step ("Ramp step (N.L)", Range(-1,1)) = 0.15
        _Feather ("Ramp feather", Range(0.001, 0.5)) = 0.04
        _Spec ("Toon highlight colour", Color) = (0,0,0,0)
        _SpecStep ("Highlight step", Range(0.5, 1)) = 0.93
        _Ink ("Ink colour", Color) = (0.17, 0.13, 0.10, 1)
        _Outline ("Outline width (m)", Float) = 0.0009
        _Boil ("Outline boil", Range(0, 1)) = 0.35
        _T ("Time", Float) = 0
        _Grain ("Paper grain strength", Range(0, 0.3)) = 0.08
        _ShadowStrength ("Shadow strength", Range(0, 1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; half4 _Lit, _Shade, _Spec, _Ink; half _Step, _Feather, _SpecStep, _Outline, _Boil, _T, _Grain, _ShadowStrength;
        CBUFFER_END
        float h31(float3 p) { p = frac(p * 0.1031); p += dot(p, p.zyx + 31.32); return frac((p.x + p.y) * p.z); }
        ENDHLSL
        Pass
        {
            Name "Toon"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.wp = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.wp);
                o.wn = TransformObjectToWorldNormal(i.n); o.uv = TRANSFORM_TEX(i.uv, _MainTex); return o;
            }
            half4 frag (V i, bool front : SV_IsFrontFace) : SV_Target
            {
                half3 alb = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                float3 n = normalize(i.wn) * (front ? 1 : -1);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.wp));
                half ndl = dot(n, L.direction);
                half sh = lerp(1, L.shadowAttenuation, _ShadowStrength);
                half lit = smoothstep(_Step - _Feather, _Step + _Feather, ndl) * smoothstep(0.35, 0.65, sh);
                half3 ramp = lerp(_Shade.rgb, _Lit.rgb, lit);
                float3 v = normalize(GetWorldSpaceViewDir(i.wp)); float3 h = normalize(L.direction + v);
                half spec = smoothstep(_SpecStep - 0.01, _SpecStep + 0.01, dot(n, h)) * lit;
                half grain = (h31(floor(i.wp * 2400)) - 0.5) * _Grain;
                return half4(alb * ramp * L.color * (1 + grain) + _Spec.rgb * spec, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "InkHull"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct A { float4 pos : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 wp = TransformObjectToWorld(i.pos.xyz); float3 wn = TransformObjectToWorldNormal(i.n);
                float k = floor(_T * 10);                                   // held 10 fps clock: the line "boils"
                float w = _Outline * (1 + _Boil * (h31(floor(wp * 300) + k) * 2 - 1));
                o.pos = TransformWorldToHClip(wp + wn * w); return o;
            }
            half4 frag (V i) : SV_Target { return _Ink; }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float3 _LightDirection;
            struct A { float4 pos : POSITION; float3 n : NORMAL; };
            struct V { float4 pos : SV_POSITION; };
            V vert (A i)
            {
                V o; float3 wp = TransformObjectToWorld(i.pos.xyz); float3 wn = TransformObjectToWorldNormal(i.n);
                o.pos = TransformWorldToHClip(ApplyShadowBias(wp, wn, _LightDirection));
                #if UNITY_REVERSED_Z
                    o.pos.z = min(o.pos.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.pos.z = max(o.pos.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 frag (V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
