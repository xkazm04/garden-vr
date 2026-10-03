// Night jar glass. Two passes in one material: the back wall first (SRPDefaultUnlit, Cull Front), then the front wall
// (UniversalForward, Cull Back), so the jar reads as a thick shell without sorting two objects.
// Output is premultiplied (Blend One OneMinusSrcAlpha): the pane stays clear, the rim and the beads stay bright,
// and a soft inner light can add without replacing the plants. The mint tint sits on the silhouette, not across
// the cavity. The light is full beside the moss and falls off with height. The lower
// third does not add wall opacity, so it stays as clear as the shoulder. Breath fog is a separate thin layer:
// it is zero when _Fog is zero, and its coverage never exceeds 0.30. Condensation beads stay in the upper third.
// No refraction: a grab pass is the one thing Quest cannot afford here.
// _InnerY is (full-until height, fade length), in metres above the mesh origin. Older gaussian centre/width
// values still land the light on the lower glass.
Shader "Fidelity/Glass"
{
    Properties
    {
        _Cond ("Condensation (R drops, G highlight, B haze)", 2D) = "black" {}
        _Tint ("Body tint (A = base opacity)", Color) = (0.3, 0.8, 0.65, 0.06)
        _Rim ("Rim colour (A = opacity)", Color) = (0.6, 1, 0.85, 0.7)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.6
        _Inner ("Inner scatter (HDR)", Color) = (0.2, 0.9, 0.6, 1)
        _InnerY ("Inner light (full until y, fade length)", Vector) = (0.04, 0.06, 0, 0)
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
            // Fog keeps the original sample, so the breath haze still clears bottom-up.
            float2 fogUv = float2(ang * 1.15, i.op.y * 16.0);
            half3 fogCond = SAMPLE_TEXTURE2D(_Cond, sampler_Cond, fogUv).rgb;
            // The bead plate is dense in its lower half. The shoulder and the short neck
            // read only the sparse top, so the droplets stay a few beads and not a field.
            float band = saturate((i.op.y - 0.078) / 0.034);
            float2 beadUv = float2(ang * 0.22 + 0.37, lerp(0.82, 0.97, band));
            half3 cond = SAMPLE_TEXTURE2D(_Cond, sampler_Cond, beadUv).rgb;
            half upper = smoothstep(0.035, 0.10, i.op.y);
            half beads = smoothstep(0.082, 0.094, i.op.y) * (1.0 - smoothstep(0.114, 0.123, i.op.y));
            half bead = smoothstep(0.72, 0.94, cond.r);
            // The plate is still a field of cores. Keep about a third so the band stays a few beads.
            float keep = frac(sin(dot(floor(beadUv * float2(14.0, 28.0)), float2(127.1, 311.7))) * 43758.5453);
            half drops = bead * beads * _Drops * step(0.66, keep);
            // _Fog is the only driver. Zero stays clear. Coverage tops out at 0.30, and the haze
            // texture keeps it a partial layer, stronger toward the shoulder.
            half fogMask = saturate((0.40 + 0.60 * fogCond.b) * (0.10 + 0.90 * upper));
            half fogA = saturate(_Fog) * fogMask * 0.30;
            // Full beside the moss, then gone as the glass rises. Nothing in the soil line.
            half column = (1.0 - smoothstep(_InnerY.x, _InnerY.x + max(_InnerY.y, 1e-4), i.op.y)) * smoothstep(0.008, 0.022, i.op.y);
            // The middle of the pane is the window. Wall opacity is not added on the lower third.
            half wall = smoothstep(0.014, 0.046, abs(i.op.x));
            half cavity = 1.0 - wall;
            float vs = mul(UNITY_MATRIX_V, float4(i.wp, 1)).x - mul(UNITY_MATRIX_V, float4(TransformObjectToWorld(float3(0, 0, 0)) + float3(0, i.op.y, 0), 1)).x;
            half streak = (smoothstep(0.006, 0.0, abs(vs + 0.030)) + 0.6 * smoothstep(0.003, 0.0, abs(vs + 0.022))) * smoothstep(0.02, 0.05, i.op.y) * smoothstep(0.125, 0.10, i.op.y);
            half rimA = rim * _Rim.a;
            half dropA = drops * 0.42;
            // No extra opacity on the lower wall. The old 0.10 made the heel milky.
            half wallA = 0.0;
            half baseA = _Tint.a;
            half streakA = streak * _Streak.a * (1.0 - backWall * 0.7);
            half a = saturate(baseA + rimA + dropA + fogA + wallA + streakA);
            // Air light is added, not used as coverage, and it thins toward the glass and with height.
            half3 light = _Inner.rgb * column * lerp(0.08, 1.0, cavity) * 0.18;
            half3 fogRgb = lerp(_Inner.rgb, half3(0.82, 0.94, 0.90), 0.55);
            half3 c =
                _Tint.rgb * baseA
                + _Rim.rgb * rimA
                + _Inner.rgb * wallA
                + (half3(0.62, 0.92, 0.78) + cond.g * 0.25) * dropA
                + fogRgb * fogA
                + _Streak.rgb * streakA
                + light;
            if (backWall > 0.5) { c *= 0.55; a *= 0.55; }
            return half4(c, a);
        }
        ENDHLSL
        Pass
        {
            Name "Back"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
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
            Blend One OneMinusSrcAlpha
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
