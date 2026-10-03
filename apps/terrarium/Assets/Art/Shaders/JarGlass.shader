// Night jar glass, with a soft interior volume on the same pass as the shell.
// Fidelity/Glass stays the shared shader. This copy adds _Volume, which is off when its alpha is 0.
// The volume is what makes the jar the light in the room: a mint fill through the cavity, not a second card
// (a full-jar card would push the transparent-layer budget over 1.5).
// Fresnel rim, condensation, breath fog, and the two window streaks match Fidelity/Glass.
// No refraction: a grab pass is the one thing Quest cannot afford here.
Shader "Fidelity/JarGlass"
{
    Properties
    {
        _Cond ("Condensation (R drops, G highlight, B haze)", 2D) = "black" {}
        _Tint ("Body tint (A = base opacity)", Color) = (0.3, 0.8, 0.65, 0.06)
        _Rim ("Rim colour (A = opacity)", Color) = (0.6, 1, 0.85, 0.7)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.6
        _Inner ("Inner scatter (HDR)", Color) = (0.2, 0.9, 0.6, 1)
        _InnerY ("Scatter falloff y (centre, width)", Vector) = (0.04, 0.06, 0, 0)
        _Volume ("Interior volume (HDR, A = opacity)", Color) = (0, 0, 0, 0)
        _VolumeY ("Volume y (centre, width)", Vector) = (0.055, 0.05, 0, 0)
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
            half4 _Tint, _Rim, _Inner, _Streak, _Volume; half _RimPower, _Fog, _Drops; float4 _InnerY, _VolumeY;
        CBUFFER_END
        struct A { float4 pos : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct V { float4 pos : SV_POSITION; float3 wn : TEXCOORD0; float3 wp : TEXCOORD1; float3 op : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
        V vert (A i)
        {
            V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.wp = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.wp);
            o.wn = TransformObjectToWorldNormal(i.n);
            o.op = o.wp - TransformObjectToWorld(float3(0, 0, 0));
            return o;
        }
        half4 shade (V i, half backWall)
        {
            float3 n = normalize(i.wn); float3 v = normalize(GetWorldSpaceViewDir(i.wp));
            half ndv = saturate(abs(dot(n, v)));
            half rim = pow(1 - ndv, _RimPower);
            float ang = atan2(i.op.x, -i.op.z);
            float2 fogUv = float2(ang * 1.15, i.op.y * 16.0);
            half3 fogCond = SAMPLE_TEXTURE2D(_Cond, sampler_Cond, fogUv).rgb;
            float band = saturate((i.op.y - 0.092) / 0.026);
            float2 beadUv = float2(ang * 0.22 + 0.37, lerp(0.82, 0.97, band));
            half3 cond = SAMPLE_TEXTURE2D(_Cond, sampler_Cond, beadUv).rgb;
            half upper = smoothstep(0.035, 0.10, i.op.y);
            half beads = smoothstep(0.094, 0.104, i.op.y) * (1.0 - smoothstep(0.116, 0.124, i.op.y));
            half bead = smoothstep(0.72, 0.94, cond.r);
            float keep = frac(sin(dot(floor(beadUv * float2(14.0, 28.0)), float2(127.1, 311.7))) * 43758.5453);
            half drops = bead * beads * _Drops * step(0.66, keep);
            half fog = saturate(_Fog * (0.5 + 0.7 * fogCond.b) * (0.08 + 0.92 * upper));
            half inner = exp(-pow((i.op.y - _InnerY.x) / max(_InnerY.y, 1e-4), 2));
            // Wide mint through the cavity. Alpha 0 leaves this at zero. Stronger in the middle of the glass,
            // softer toward the left and right edges, so it reads as air and not a green poster.
            half body = exp(-pow((i.op.y - _VolumeY.x) / max(_VolumeY.y, 1e-4), 2.0));
            half cavity = smoothstep(0.016, 0.030, i.op.y) * (1.0 - smoothstep(0.102, 0.122, i.op.y));
            half across = lerp(0.42h, 1.0h, 1.0h - smoothstep(0.010, 0.044, abs(i.op.x)));
            half fill = body * cavity * across;
            float vs = mul(UNITY_MATRIX_V, float4(i.wp, 1)).x - mul(UNITY_MATRIX_V, float4(TransformObjectToWorld(float3(0, 0, 0)) + float3(0, i.op.y, 0), 1)).x;
            half streak = (smoothstep(0.006, 0.0, abs(vs + 0.030)) + 0.6 * smoothstep(0.003, 0.0, abs(vs + 0.022))) * smoothstep(0.02, 0.05, i.op.y) * smoothstep(0.125, 0.10, i.op.y);
            half3 c = _Tint.rgb + _Inner.rgb * inner * (0.35 + rim) + _Rim.rgb * rim;
            c += _Volume.rgb * fill;
            c += drops * (half3(0.42, 0.72, 0.58) + cond.g * 0.20) + fog * _Inner.rgb * 0.5 + _Streak.rgb * streak * (1 - backWall * 0.7);
            half a = saturate(_Tint.a + rim * _Rim.a + inner * 0.10 + fill * _Volume.a + drops * 0.34 + fog * 0.35 + streak * _Streak.a);
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
