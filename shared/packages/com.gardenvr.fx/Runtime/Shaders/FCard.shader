// Every card in both heroes: halo cards (instead of bloom), spores, mist, the breath ring, the desk light pool, the plant
// cards and the pinch halo. _Mode stays the old blend: 0 additive (glow), 1 alpha-blended (mist, drawn plants) via _Src.
// _Ring > 0 turns on the breath-ring sweep: R = line, G = pool; _Fill is the breath progress 0..1 around the circle.
// _Coverage > 0 is alpha-to-coverage (plants). Leave it at 0 for Terrarium: blend, ZWrite and the ring are unchanged.
// Boil is a held 10 fps clock. _BoilPx jitters by that many screen pixels (keep it at or under 1.2). _Boil still
// jitters in UV units when _BoilPx is 0, which is the Terrarium path. A negative _BoilTime uses _T.
// _Mask > 0 reads an interim halo whose line lives in B and whose glow lives in G (R is empty).
// _GVR_ROOMLIGHT (global keyword, off by default) multiplies the alpha-to-coverage plant cards by the room-light cookie
// at the card's world position (Spike S2). Glow, halo, ring and shadow cards are not touched.
Shader "Fidelity/Card"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Colour (HDR)", Color) = (1, 1, 1, 1)
        _Color2 ("Second colour (ring pool / halo glow)", Color) = (0, 0, 0, 0)
        [Enum(UnityEngine.Rendering.BlendMode)] _Src ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Dst", Float) = 1
        _Ring ("Ring mode", Float) = 0
        _Fill ("Ring fill 0..1", Range(0, 1)) = 1
        _Boil ("Boil amount (UV)", Float) = 0
        _BoilPx ("Boil amount (pixels)", Range(0, 1.2)) = 0
        _BoilFps ("Boil fps", Float) = 10
        _BoilTime ("Boil time (negative uses _T)", Float) = -1
        _T ("Time (driven by script for deterministic captures)", Float) = 0
        _ZTest ("ZTest", Float) = 4
        _ZWrite ("ZWrite", Float) = 0
        _Coverage ("Alpha to coverage", Float) = 0
        _Mask ("Halo mask (B line, G glow)", Float) = 0
        _Sparkle ("Sparkle", Range(0, 1)) = 0
        // Zero leaves every existing card unchanged. A positive power fades the card from _Focus.
        _Falloff ("Radial falloff (0 = off)", Float) = 0
        _Focus ("Focus uv xy, radius z", Vector) = (0.5, 0.5, 0.5, 0)
        // Zero leaves every existing card unchanged. Above zero, _MainTex is a baked
        // outline: R is the bright core, G is a short falloff. _Fit above 1 insets it.
        _Silhouette ("Silhouette glow px (0 = off)", Float) = 0
        _Fit ("Silhouette fit", Float) = 1
        // Zero leaves every existing card unchanged. A positive value shades a curved card from the window.
        _CardLight ("Card light (0 = off)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend [_Src] [_Dst]
            ZWrite [_ZWrite]
            Cull Off
            ZTest [_ZTest]
            AlphaToMask [_Coverage]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GVR_ROOMLIGHT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_GvrRoomCookie); SAMPLER(sampler_GvrRoomCookie);
            float4x4 _GvrRoomW2C;
            float4 _GvrRoomParams;
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color, _Color2;
                half _Ring, _Fill, _Boil, _BoilPx, _BoilFps, _BoilTime, _T, _Src, _Dst, _ZTest, _ZWrite, _Coverage, _Mask, _Sparkle, _Falloff;
                float _Silhouette, _Fit, _CardLight;
                float4 _Focus;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; half4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; half4 col : COLOR; float3 wp : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                o.wn = TransformObjectToWorldNormal(i.n);
                o.col = i.col;
                o.wp = TransformObjectToWorld(i.pos.xyz);
                return o;
            }
            float h21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            // The sundial halo bakes this mask: R is the core, G is the short falloff.
            // Sampling a mask keeps the dial untinted. A live dilate was a wide wash.
            half4 SilhouetteGlow(float2 uv)
            {
                float fit = max(_Fit, 1.0);
                float2 puv = float2((uv.x - 0.5) * fit + 0.5, uv.y * fit);
                if (puv.x < 0.0 || puv.y < 0.0 || puv.x > 1.0 || puv.y > 1.0)
                    return half4(0, 0, 0, 1);
                half4 m = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, puv);
                half3 rgb = _Color.rgb * m.r + _Color2.rgb * m.g;
                return half4(rgb, 1);
            }
            half4 frag (V i) : SV_Target
            {
                float clock = _BoilTime >= 0 ? _BoilTime : _T;
                float2 uv = i.uv;
                float k = floor(clock * _BoilFps + 0.0001);
                if (_BoilPx > 0)
                {
                    float2 cell = floor(uv * 6);
                    float2 j = float2(h21(cell + k), h21(cell + k + 17.3)) - 0.5;
                    // fwidth is the UV change across one pixel. j is +/- 0.5, so 2*j*_BoilPx is +/- _BoilPx pixels.
                    uv += j * 2.0 * _BoilPx * max(fwidth(i.uv), float2(1e-6, 1e-6));
                }
                else if (_Boil > 0)
                {
                    float2 cell = floor(uv * 6);
                    uv += (float2(h21(cell + k), h21(cell + k + 17.3)) - 0.5) * _Boil;
                }
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half4 c;
                if (_Silhouette > 0.001)
                    c = SilhouetteGlow(uv);
                else if (_Ring > 0)
                {
                    float2 p = i.uv * 2 - 1;
                    float ang = frac(atan2(p.x, -p.y) / 6.2831853 + 0.5);
                    half lit = smoothstep(_Fill + 0.004, _Fill - 0.004, ang);
                    half ln = t.r * (0.18 + 0.82 * lit);
                    c = half4(_Color.rgb * ln + _Color2.rgb * t.g, 1);
                }
                else if (_Coverage > 0.5)
                {
                    c = half4(t.rgb * _Color.rgb, t.a * _Color.a);
                    #if defined(_GVR_ROOMLIGHT)
                    {
                        // The card's base point, so the whole plant takes one value and the cookie does not smear up it.
                        float3 lp = mul(_GvrRoomW2C, float4(i.wp.x, TransformObjectToWorld(float3(0, 0, 0)).y, i.wp.z, 1.0)).xyz;
                        float2 cuv = lp.xz / max(_GvrRoomParams.x, 1e-4) + 0.5;
                        float cd = SAMPLE_TEXTURE2D(_GvrRoomCookie, sampler_GvrRoomCookie, cuv).r * 2.0 - 1.0;
                        half cg = (half)pow(1.0 + _GvrRoomParams.y * clamp(cd * _GvrRoomParams.z + _GvrRoomParams.w, -1.0, 1.0), 2.2);
                        half cpeak = max(max(c.r, c.g), max(c.b, 1e-3));
                        c.rgb *= min(cg, max((half)1.0, (half)(0.985 / cpeak)));
                    }
                    #endif
                    // Drop the empty card. Alpha-to-coverage still softens the ink when MSAA is on.
                    if (c.a < 0.02) discard;
                }
                else if (_Src > 4.5)
                    c = half4(t.rgb * _Color.rgb, t.a * _Color.a);
                else
                {
                    half stroke = _Mask > 0.5 ? max(t.r, t.b) : t.r;
                    half glow = t.g;
                    if (_Mask > 0.5) { stroke *= t.a; glow *= t.a; }
                    c = half4(_Color.rgb * stroke + _Color2.rgb * glow, 1);
                }
                // Pinch halo only. _Sparkle stays 0 everywhere else, so terrarium cards skip this.
                // Sparks and the glow sit on the painted stroke. An empty band above the plant
                // put the dots in the air.
                if (_Sparkle > 0.001)
                {
                    float a = t.a;
                    float2 px = max(fwidth(uv), float2(1e-4, 1e-4)) * 5.0;
                    float neigh =
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(px.x, 0)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(px.x, 0)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, px.y)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(0, px.y)).a;
                    float glow = saturate(neigh * 0.28) * (1.0 - saturate(a * 1.6));
                    c.rgb += half3(1.0, 0.84, 0.42) * (half)glow * 0.7;
                    c.a = max(c.a, (half)(glow * 0.55));

                    float2 cell = floor(uv * float2(9.0, 14.0));
                    float2 f = frac(uv * float2(9.0, 14.0)) - 0.5;
                    float n = h21(cell + 3.1);
                    float on = step(0.72, frac(n * 13.0 + k * 0.37));
                    float dotp = smoothstep(0.22, 0.02, length(f));
                    float edge = smoothstep(0.08, 0.28, a) * smoothstep(0.92, 0.45, a);
                    float rim = saturate(neigh * 0.3) * (1.0 - saturate(a * 2.2));
                    half spark = (half)(on * dotp * max(edge, rim * 0.85) * _Sparkle);
                    c.rgb += half3(1.0, 0.92, 0.55) * spark * 1.8;
                    c.a = max(c.a, spark);
                }
                if (_Falloff > 0.001)
                {
                    float d = length(i.uv - _Focus.xy);
                    float f = saturate(1.0 - d / max(_Focus.z, 1e-3));
                    f = pow(f, _Falloff);
                    c.rgb *= (half)f;
                    if (_Src > 4.5) c.a *= (half)f;
                }
                // Curved plant cards only. Zero on every other card, including Terrarium.
                if (_CardLight > 0.001)
                {
                    float3 L = normalize(float3(-0.28, 0.86, 0.42));
                    float ndl = dot(normalize(i.wn), L);
                    float wrap = saturate(ndl * 0.5 + 0.62);
                    c.rgb *= (half)lerp(1.0, wrap, saturate(_CardLight));
                }
                return c * i.col;
            }
            ENDHLSL
        }
    }
}
