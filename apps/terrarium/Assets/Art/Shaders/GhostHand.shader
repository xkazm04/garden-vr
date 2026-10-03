// First-run hint. A mask pair (open, pinch) becomes a light in the jar's mint:
// translucent body, brighter rim, feathered edge. No hard silhouette.
Shader "Fidelity/GhostHand"
{
    Properties
    {
        _MainTex ("Open mask", 2D) = "white" {}
        _PinchTex ("Pinch mask", 2D) = "white" {}
        _Pinch ("Pinch 0 open, 1 hold", Range(0, 1)) = 0
        _Color ("Body mint", Color) = (0.55, 0.92, 0.72, 1)
        _RimColor ("Rim mint", Color) = (0.72, 1.05, 0.90, 1)
        _Body ("Body opacity", Range(0, 1)) = 0.30
        _Rim ("Rim strength", Range(0, 2)) = 1.05
        _SoftPx ("Body feather (pixels)", Float) = 11
        _HaloPx ("Rim feather (pixels)", Float) = 26
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_PinchTex); SAMPLER(sampler_PinchTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color, _RimColor;
                half _Pinch, _Body, _Rim, _SoftPx, _HaloPx;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert (A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = i.uv;
                return o;
            }
            float Mask(float2 uv, float bias)
            {
                float2 u = saturate(uv);
                float open = SAMPLE_TEXTURE2D_BIAS(_MainTex, sampler_MainTex, u, bias).a;
                float hold = SAMPLE_TEXTURE2D_BIAS(_PinchTex, sampler_PinchTex, u, bias).a;
                return lerp(open, hold, saturate(_Pinch));
            }
            half4 frag (V i) : SV_Target
            {
                // One mip falloff. A separate rim used to leave a dark line on the wood,
                // and a wide tap blur used to draw extra copies of the hand.
                float motion = saturate(_Pinch * (1.0 - _Pinch) * 4.0);
                float wide = Mask(i.uv, lerp(2.6, 3.6, motion));
                float tight = Mask(i.uv, lerp(0.6, 1.3, motion));
                float cover = smoothstep(0.0, 0.92, wide);
                float core = smoothstep(0.18, 0.82, tight);
                float fromMoss = lerp(1.05, 0.92, saturate(i.uv.y));
                float a = saturate(cover * _Body * 0.90 + core * 0.06);
                half bright = (half)lerp(0.92, 1.08, saturate(_Rim * 0.5));
                half3 rgb = (_Color.rgb * bright * (half)a + _RimColor.rgb * (half)(core * 0.045)) * (half)fromMoss;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
