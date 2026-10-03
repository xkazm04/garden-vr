// Moss mound. Two Nano Banana macros, blended per patch by UV2.x.
// Mesh UV is one sheared crop, so the kaleidoscope is not a tiled honeycomb.
// A second sample and world-space noise break whatever regularity the crop
// still has. Steep sides can take a short triplanar sample. Lighting matches
// Fidelity/Glow (tip, rim, crozier light).
Shader "Fidelity/Moss"
{
    Properties
    {
        _MainTex ("Macro A", 2D) = "white" {}
        _MacroB ("Macro B", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _EmissionTex ("Emission mask", 2D) = "white" {}
        _Emission ("Emission colour (HDR)", Color) = (0,0,0,1)
        _Rim ("Fuzz rim colour", Color) = (0,0,0,1)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.5
        _GradBottom ("Self-light at bottom", Float) = 0.6
        _GradTop ("Self-light at top", Float) = 1.2
        _GradY ("Gradient y range (min,max, world)", Vector) = (0, 0.1, 0, 0)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.0
        _TopTex ("Top texture (unused, kept so older setups do not warn)", 2D) = "white" {}
        _TopTile ("Top tiling", Float) = 1
        _TopAmount ("Top blend", Range(0,1)) = 0
        _Shell ("Shell offset along normal (m)", Float) = 0
        _Tri ("Triplanar scale on steep sides (0 = off)", Float) = 0
        _LightPos ("Focused light (xyz, radius; 0 = off)", Vector) = (0, 0, 0, 0)
        _LightColor ("Focused light colour", Color) = (0, 0, 0, 1)
        _Trans ("Translucency", Range(0, 3)) = 0
        _Soft ("Soft edge dither", Range(0, 0.6)) = 0
        _Tip ("Tip glow (world y0, y1, floor, peak)", Vector) = (0, 1, 1, 1)
        _Edge ("Shape-edge glow", Range(0, 2)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MacroB); SAMPLER(sampler_MacroB);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint, _Emission, _Rim;
                half _RimPower, _GradBottom, _GradTop, _Cutoff, _TopTile, _TopAmount, _Shell, _Tri;
                float4 _GradY;
                float4 _LightPos; half4 _LightColor; half _Trans, _Soft; float4 _Tip; half _Edge;
            CBUFFER_END
            struct A
            {
                float4 pos : POSITION;
                float3 n : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct V
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float blend : TEXCOORD1;
                float3 wn : TEXCOORD2;
                float3 wp : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            V vert (A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.wn = TransformObjectToWorldNormal(i.n);
                float3 wp = TransformObjectToWorld(i.pos.xyz);
                o.wp = wp + normalize(o.wn) * _Shell;
                o.pos = TransformWorldToHClip(o.wp);
                o.uv = i.uv;
                o.blend = saturate(i.uv2.x);
                return o;
            }
            float mossHash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float mossNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = mossHash(i);
                float b = mossHash(i + float2(1, 0));
                float c = mossHash(i + float2(0, 1));
                float d = mossHash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            half3 triplanar(float3 p, float3 w)
            {
                half3 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.zy).rgb * w.x
                    + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.xz).rgb * w.y
                    + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.xy).rgb * w.z;
                half3 b = SAMPLE_TEXTURE2D(_MacroB, sampler_MacroB, p.zy).rgb * w.x
                    + SAMPLE_TEXTURE2D(_MacroB, sampler_MacroB, p.xz).rgb * w.y
                    + SAMPLE_TEXTURE2D(_MacroB, sampler_MacroB, p.xy).rgb * w.z;
                return lerp(a, b, 0.5);
            }
            half4 frag (V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.wn) * (front ? 1 : -1);
                half3 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                half3 a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0.016, -0.011)).rgb;
                half3 b = SAMPLE_TEXTURE2D(_MacroB, sampler_MacroB, i.uv + float2(-0.012, 0.015)).rgb;
                half3 b2 = SAMPLE_TEXTURE2D(_MacroB, sampler_MacroB, i.uv).rgb;
                float grit = mossNoise(i.wp.xz * 70.0);
                float grit2 = mossNoise(i.wp.xz * 31.0 + 4.0);
                // Switch crops instead of averaging them, so the detail stays sharp.
                half3 alb = lerp(lerp(a, a2, step(0.5, grit)), lerp(b2, b, step(0.5, grit2)), (half)saturate(i.blend));
                alb *= (half)lerp(0.78, 1.08, grit);
                alb *= (half)lerp(0.90, 1.05, grit2);
                alb.r = min(alb.r, alb.g * 0.84h);
                if (_Tri > 0.01)
                {
                    float3 w = pow(abs(n), 4);
                    w /= max(w.x + w.y + w.z, 1e-4);
                    // Phase by the cushion's crop so neighbouring pads do not share a grid.
                    float3 p = i.wp * _Tri + float3(i.uv.x, i.blend, i.uv.y) * 2.5;
                    half steep = 1.0 - smoothstep(0.45, 0.82, abs(n.y));
                    alb = lerp(alb, triplanar(p, w), steep * 0.55);
                }
                alb *= _Tint.rgb;
                half em = dot(alb, half3(0.30, 0.60, 0.10));
                half tipT = saturate((i.wp.y - _Tip.x) / max(1e-4, _Tip.y - _Tip.x));
                em *= lerp(_Tip.z, _Tip.w, tipT);
                clip(1.0 - _Cutoff);
                float3 v = normalize(GetWorldSpaceViewDir(i.wp));
                half rim = pow(1.0 - saturate(abs(dot(n, v))), _RimPower);
                half g = lerp(_GradBottom, _GradTop, saturate((i.wp.y - _GradY.x) / max(1e-4, _GradY.y - _GradY.x)));
                half3 c = alb * g + _Emission.rgb * em + _Rim.rgb * rim;
                if (_LightPos.w > 0.001)
                {
                    float3 toL = _LightPos.xyz - i.wp;
                    float dist = length(toL);
                    float3 L = toL / max(dist, 1e-4);
                    float atten = saturate(1.0 - dist / _LightPos.w);
                    atten *= atten;
                    half ndl = dot(n, L);
                    half wrap = saturate(ndl * 0.5 + 0.5);
                    half back = saturate(-ndl);
                    c += _LightColor.rgb * (half)atten * wrap * (alb * 0.55h + 0.45h);
                    c += _LightColor.rgb * (half)atten * back * _Trans * lerp(0.35h, 1.0h, saturate(em));
                }
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
