// Night jar, opaque/cutout parts: albedo x a fake "lit from the jar's own glow" gradient, plus emission and a fuzz rim.
// Unlit by design (no realtime light loop): on Quest the jar is the light source, so the lighting is authored, not computed.
Shader "Fidelity/Glow"
{
    Properties
    {
        _MainTex ("Albedo (A = cutout)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _EmissionTex ("Emission mask", 2D) = "white" {}
        _Emission ("Emission colour (HDR)", Color) = (0,0,0,1)
        _Rim ("Fuzz rim colour", Color) = (0,0,0,1)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.5
        _GradBottom ("Self-light at bottom", Float) = 0.6
        _GradTop ("Self-light at top", Float) = 1.2
        _GradY ("Gradient y range (min,max, world)", Vector) = (0, 0.1, 0, 0)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.0
        _TopTex ("Top texture (planar, world xz)", 2D) = "white" {}
        _TopTile ("Top tiling", Float) = 10
        _TopAmount ("Top blend", Range(0,1)) = 0
        _Shell ("Shell offset along normal (m)", Float) = 0
        _Tri ("Triplanar tiling for _MainTex (0 = mesh UV)", Float) = 0
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
            TEXTURE2D(_EmissionTex); SAMPLER(sampler_EmissionTex);
            TEXTURE2D(_TopTex); SAMPLER(sampler_TopTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; half4 _Tint, _Emission, _Rim; half _RimPower, _GradBottom, _GradTop, _Cutoff, _TopTile, _TopAmount, _Shell, _Tri; float4 _GradY;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.wn = TransformObjectToWorldNormal(i.n);
                o.wp = TransformObjectToWorld(i.pos.xyz) + normalize(o.wn) * _Shell; o.pos = TransformWorldToHClip(o.wp);   // fuzz shells push out along the normal
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }
            half4 frag (V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.wn) * (front ? 1 : -1);
                half4 alb; half em;
                if (_Tri > 0)
                {   // triplanar: the moss texture never stretches down the steep front of the mound
                    float3 w = pow(abs(n), 4); w /= (w.x + w.y + w.z);
                    float3 p = i.wp * _Tri;
                    alb = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.zy) * w.x + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.xz) * w.y + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.xy) * w.z;
                    alb *= _Tint; em = dot(alb.rgb, half3(0.3, 0.6, 0.1));
                }
                else
                {
                    alb = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Tint;
                    em = SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, i.uv).r;
                }
                clip(alb.a - _Cutoff);
                if (_Shell > 0) clip(abs(n.y) - 0.45);   // fuzz only on the cap, never on the vertical skirt
                // caps (moss top, cork top) take a planar texture so the lathe's pole never shows its radial pinch
                half topw = _TopAmount * smoothstep(0.45, 0.75, abs(n.y));
                half3 top = SAMPLE_TEXTURE2D(_TopTex, sampler_TopTex, i.wp.xz * _TopTile).rgb * _Tint.rgb;
                alb.rgb = lerp(alb.rgb, top, topw); em = lerp(em, dot(top, half3(0.3, 0.6, 0.1)), topw);
                float3 v = normalize(GetWorldSpaceViewDir(i.wp));
                half rim = pow(1 - saturate(abs(dot(n, v))), _RimPower);
                half g = lerp(_GradBottom, _GradTop, saturate((i.wp.y - _GradY.x) / max(1e-4, _GradY.y - _GradY.x)));
                half3 c = alb.rgb * g + _Emission.rgb * em + _Rim.rgb * rim;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
