// Night jar glass. Two passes in one material: the back wall first (SRPDefaultUnlit, Cull Front), then the front wall
// (UniversalForward, Cull Back), so the jar reads as a thick shell without sorting two objects.
// Fresnel rim, an inner mint scatter that brightens toward the moss, painted condensation droplets (cylindrical UV),
// breath fog, and two window streaks. No refraction: a grab pass is the one thing Quest cannot afford here.
Shader "Fidelity/Glass"
{
    Properties
    {
        _Cond ("Condensation (R drops, G highlight, B haze)", 2D) = "black" {}
        _Tint ("Body tint (A = base opacity)", Color) = (0.3, 0.8, 0.65, 0.06)
        _Rim ("Rim colour (A = opacity)", Color) = (0.6, 1, 0.85, 0.7)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.6
        _Inner ("Inner scatter (HDR)", Color) = (0.2, 0.9, 0.6, 1)
        _InnerY ("Scatter falloff y (centre, width)", Vector) = (0.04, 0.06, 0, 0)
        _Fog ("Breath fog", Range(0,1)) = 0.3
        _Drops ("Droplet strength", Range(0,2)) = 1
        _Streak ("Streak", Color) = (0.8, 1, 0.95, 0.35)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_Cond); SAMPLER(sampler_Cond);
        CBUFFER_START(UnityPerMaterial)
            half4 _Tint, _Rim, _Inner, _Streak; half _RimPower, _Fog, _Drops; float4 _InnerY;
        CBUFFER_END
        struct A { float4 pos : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct V { float4 pos : SV_POSITION; float3 wn : TEXCOORD0; float3 wp : TEXCOORD1; float3 op : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
        V vert (A i)
        {
            V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.wp = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.wp);
            o.wn = TransformObjectToWorldNormal(i.n);
            o.op = o.wp - TransformObjectToWorld(float3(0, 0, 0));   // world-aligned, y up, origin at the jar's base
            return o;
        }
        half4 shade (V i, half backWall)
        {
            float3 n = normalize(i.wn); float3 v = normalize(GetWorldSpaceViewDir(i.wp));
            half ndv = saturate(abs(dot(n, v)));
            half rim = pow(1 - ndv, _RimPower);
            // Angle around the jar. A planar (x, z) UV barely changes across the front,
            // which stretched each bead into a horizontal pill. The seam sits on the back.
            // G is a highlight from an estimated normal (Sobel of the height), not a measured map.
            float ang = atan2(i.op.x, -i.op.z);
            float2 cuv = float2(ang * 1.15, i.op.y * 16.0);
            half3 cond = SAMPLE_TEXTURE2D(_Cond, sampler_Cond, cuv).rgb;
            // Droplets sit denser on the lower glass. `upper` is only the fog mask, so a falling
            // _Fog still empties the base first and the breath fog keeps its bottom-up sweep.
            half upper = smoothstep(0.035, 0.10, i.op.y);
            half low = 1.0 - smoothstep(0.018, 0.085, i.op.y);
            half drops = cond.r * _Drops * (0.12 + 1.15 * low) * smoothstep(0.006, 0.02, i.op.y);
            half fog = saturate(_Fog * (0.5 + 0.7 * cond.b) * (0.08 + 0.92 * upper));
            half inner = exp(-pow((i.op.y - _InnerY.x) / _InnerY.y, 2));   // glow is strongest beside the moss
            float vs = mul(UNITY_MATRIX_V, float4(i.wp, 1)).x - mul(UNITY_MATRIX_V, float4(TransformObjectToWorld(float3(0, 0, 0)) + float3(0, i.op.y, 0), 1)).x;
            half streak = (smoothstep(0.006, 0.0, abs(vs + 0.030)) + 0.6 * smoothstep(0.003, 0.0, abs(vs + 0.022))) * smoothstep(0.02, 0.05, i.op.y) * smoothstep(0.125, 0.10, i.op.y);
            half3 c = _Tint.rgb + _Inner.rgb * inner * (0.35 + rim) + _Rim.rgb * rim;
            c += drops * (_Inner.rgb * 0.6 + cond.g * 1.5) + fog * _Inner.rgb * 0.5 + _Streak.rgb * streak * (1 - backWall * 0.7);
            half a = saturate(_Tint.a + rim * _Rim.a + inner * 0.10 + drops * 0.55 + fog * 0.35 + streak * _Streak.a);
            a *= backWall > 0.5 ? 0.55 : 1;
            return half4(c, a);
        }
        ENDHLSL
        Pass
        {
            Name "Back"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragB
            #pragma multi_compile_instancing
            half4 fragB (V i) : SV_Target { return shade(i, 1); }
            ENDHLSL
        }
        Pass
        {
            Name "Front"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragF
            #pragma multi_compile_instancing
            half4 fragF (V i) : SV_Target { return shade(i, 0); }
            ENDHLSL
        }
    }
}
