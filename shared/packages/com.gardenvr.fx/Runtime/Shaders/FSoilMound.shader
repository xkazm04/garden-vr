// Raised soil mound (Sundial spike S4). New shader, nothing else uses it.
//   1. Planar top-down paint. The mesh UV is the dial's x and z, so a painting made from above lands on the heap
//      without stretch. The view cone onto a mound seen from above is narrow, which is why a projected painting holds.
//   2. The same two-step toon ramp the other dial pieces use, plus the real main-light shadow, so the nib still shades it.
//   3. Paper granulation from the same height map as the face (_PaperH), one tap, so the earth sits on the same sheet.
//   4. A wash-bleed edge (T-SUN-051, replaces the S4 stipple cut). The paint's alpha is an edge parameter e: 0 on the wash,
//      1 on the earth, a smooth 3 to 12 mm ramp between. The face's own texture is read at the same dial position (_WashTex,
//      u = _WashU . (x, z, 1), v = _WashV . (x, z, 1)) and mixed in by 1 - e, so the wash enters the soil edge and the
//      outermost fragments are the wash colour itself: the cut at e = 0 has no seam. The paper height map breaks the ramp up
//      like pigment on tooth. Opaque: no blend, no alpha to coverage, no sorting, no transparent layer.
//   5. Pebbles share the mesh. Vertex alpha 1 marks them: vertex colour is the stone, the underside takes the ink.
// _GVR_ROOMLIGHT is the S2 global cookie, so the mound composes with variant=roomlight on the same terms as Fidelity/Toon.
Shader "Fidelity/SoilMound"
{
    Properties
    {
        _MainTex ("Painted earth (RGB) and edge (A)", 2D) = "white" {}
        _PaperH ("Tiled paper height", 2D) = "gray" {}
        _Lit ("Lit colour", Color) = (1.02, 0.99, 0.95, 1)
        _Shade ("Shade colour", Color) = (0.80, 0.76, 0.72, 1)
        _Ink ("Ink colour", Color) = (0.165, 0.149, 0.133, 1)
        _Step ("Ramp step (N.L)", Range(-1, 1)) = 0.05
        _Feather ("Ramp feather", Range(0.001, 0.5)) = 0.07
        _Cut ("Alpha cut (unused, kept for the material)", Range(0.05, 0.95)) = 0.45
        _Gran ("Paper granulation", Range(0, 1)) = 0.22
        _PaperTiling ("Paper repeats across the paint", Range(1, 16)) = 3.75
        _Grain ("Paper grain strength", Range(0, 0.3)) = 0.045
        _ShadowStrength ("Shadow strength", Range(0, 1)) = 0.85
        _PebbleInk ("Pebble underside ink", Range(0, 1)) = 0.75
        _WashTex ("Face texture the edge bleeds into", 2D) = "white" {}
        _WashU ("Face u = x, z, 1 coefficients", Vector) = (0, 0, 0, 0)
        _WashV ("Face v = x, z, 1 coefficients", Vector) = (0, 0, 0, 0)
        _WashOn ("Wash bleed on", Float) = 0
        _WashFallback ("Colour the edge fades to without a face texture", Color) = (0.957, 0.929, 0.875, 1)
        _WashTint ("Wash tint carried into the earth side of the ramp", Range(0, 1)) = 0.55
        _EdgeGran ("Paper tooth in the ramp", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+4" "RenderType"="Opaque" }
        Pass
        {
            Name "SoilMound"
            Tags { "LightMode"="UniversalForward" }
            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GVR_ROOMLIGHT
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_PaperH); SAMPLER(sampler_PaperH);
            TEXTURE2D(_WashTex); SAMPLER(sampler_WashTex);
            TEXTURE2D(_GvrRoomCookie); SAMPLER(sampler_GvrRoomCookie);
            float4x4 _GvrRoomW2C;
            float4 _GvrRoomParams;
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _PaperH_ST;
                half4 _Lit, _Shade, _Ink, _WashFallback;
                float4 _WashU, _WashV, _WashTex_ST;
                half _WashOn, _WashTint, _EdgeGran;
                half _Step, _Feather, _Cut, _Gran, _PaperTiling, _Grain, _ShadowStrength, _PebbleInk;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; half4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; float3 op : TEXCOORD3; half4 col : TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
            float h31(float3 p) { p = frac(p * 0.1031); p += dot(p, p.zyx + 31.32); return frac((p.x + p.y) * p.z); }
            float RoomLightGain(float3 wp)
            {
                float3 lp = mul(_GvrRoomW2C, float4(wp, 1.0)).xyz;
                float2 uv = lp.xz / max(_GvrRoomParams.x, 1e-4) + 0.5;
                float d = SAMPLE_TEXTURE2D(_GvrRoomCookie, sampler_GvrRoomCookie, uv).r * 2.0 - 1.0;
                return pow(1.0 + _GvrRoomParams.y * clamp(d * _GvrRoomParams.z + _GvrRoomParams.w, -1.0, 1.0), 2.2);
            }
            V vert (A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.op = i.pos.xyz;
                o.wp = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.wp);
                o.wn = TransformObjectToWorldNormal(i.n);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                o.col = i.col;
                return o;
            }
            half4 frag (V i) : SV_Target
            {
                bool pebble = i.col.a > 0.5;
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half e = t.a;
                if (!pebble && e < 0.004) discard;
                half3 alb = pebble ? i.col.rgb : t.rgb;
                float3 n = normalize(i.wn);
                // Same grain and paper tooth the washes carry, so the earth is on the same sheet. The mean stays put.
                float2 puv = i.uv * _PaperTiling;
                float H = SAMPLE_TEXTURE2D(_PaperH, sampler_PaperH, puv).r;
                alb *= 1.0 + _Gran * (H - 0.5) * 2.0;
                if (!pebble)
                {
                    // The wash under this fragment: the face's own texture at the same dial position.
                    half3 wash = _WashFallback.rgb;
                    if (_WashOn > 0.5)
                    {
                        float3 q = float3(i.op.x, i.op.z, 1.0);
                        wash = SAMPLE_TEXTURE2D(_WashTex, sampler_WashTex, float2(dot(_WashU.xyz, q), dot(_WashV.xyz, q))).rgb;
                    }
                    // Tooth breaks the ramp where it is steepest, so the edge is pigment catching on paper, not a gradient strip.
                    half mid = 1.0 - abs(e * 2.0 - 1.0);
                    half w = saturate(e + _EdgeGran * (H - 0.5) * 0.9 * mid);
                    // The earth side of the ramp already carries some of the wash hue (multiplied in, mean luma kept), then it dissolves.
                    half3 hue = wash / max(dot(wash, half3(0.333, 0.333, 0.333)), 1e-3);
                    half3 earth = lerp(alb, alb * hue, _WashTint * (1.0 - smoothstep(0.35, 1.0, w)));
                    alb = lerp(wash, earth, smoothstep(0.0, 1.0, w));
                }
                if (pebble)
                {
                    // A lit top and an ink underside. The side of the stone that faces down or away takes the ink.
                    half up = smoothstep(0.05, 0.55, n.y);
                    alb = lerp(lerp(alb, _Ink.rgb, _PebbleInk), alb, up);
                }
                Light L = GetMainLight(TransformWorldToShadowCoord(i.wp));
                half ndl = dot(n, L.direction);
                half sh = lerp(1, L.shadowAttenuation, _ShadowStrength);
                half lit = smoothstep(_Step - _Feather, _Step + _Feather, ndl) * smoothstep(0.35, 0.65, sh);
                half3 ramp = lerp(_Shade.rgb, _Lit.rgb, lit);
                half grain = (h31(floor(i.op * 2400)) - 0.5) * _Grain;
                half3 diffuse = alb * ramp * L.color * (1 + grain);
                #if defined(_GVR_ROOMLIGHT)
                {
                    half g = (half)RoomLightGain(i.wp);
                    half peak = max(max(diffuse.r, diffuse.g), max(diffuse.b, 1e-3));
                    g = min(g, max((half)1.0, (half)(0.985 / peak)));
                    diffuse *= g;
                }
                #endif
                return half4(diffuse, 1.0);
            }
            ENDHLSL
        }
    }
}
