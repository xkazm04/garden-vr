// Drawn dial body. Painted albedo times a two-step toon ramp (lit 1, shade about 0.78 with a warm shift),
// paper grain in object space, and an inverted-hull ink outline. The hull pushes back faces along the
// smoothed normal stored in UV1/UV2 (Nxy, Nz), so hard shading normals can stay on the mesh.
// _OutlinePx is the width in screen pixels. _BoilPx is the 10 fps jitter in pixels (keep it at or under 1.2).
// A negative _BoilTime falls back to _T, which is what older materials set.
// _TileMode draws one instanced tile: UV3.x is the tile index, _StateTex is a 21-wide point texture
// (R = state/4, G = arc/2). _TileMode 0 leaves the ramp path unchanged.
Shader "Fidelity/Toon"
{
    Properties
    {
        _MainTex ("Painted albedo", 2D) = "white" {}
        _StateTex ("Tile state (21 wide)", 2D) = "black" {}
        _Lit ("Lit colour", Color) = (1, 1, 1, 1)
        _Shade ("Shade colour", Color) = (0.78, 0.72, 0.64, 1)
        _Step ("Ramp step (N.L)", Range(-1, 1)) = 0.15
        _Feather ("Ramp feather", Range(0.001, 0.5)) = 0.04
        _Spec ("Toon highlight colour", Color) = (0, 0, 0, 0)
        _SpecStep ("Highlight step", Range(0.5, 1)) = 0.93
        _Ink ("Ink colour", Color) = (0.165, 0.149, 0.133, 1)
        _WashMorning ("Morning tile wash", Color) = (0.886, 0.722, 0.400, 1)
        _WashMidday ("Midday tile wash", Color) = (0.890, 0.612, 0.510, 1)
        _WashDusk ("Dusk tile wash", Color) = (0.655, 0.604, 0.839, 1)
        _Outline ("Outline width (m, used when _OutlinePx is 0)", Float) = 0.0009
        _OutlinePx ("Outline width (pixels)", Float) = 0
        _Boil ("Outline boil (fraction of the metre width)", Range(0, 1)) = 0
        _BoilPx ("Outline boil (pixels)", Range(0, 1.2)) = 0
        _BoilTime ("Boil time (negative uses _T)", Float) = -1
        _T ("Time", Float) = 0
        _Grain ("Paper grain strength", Range(0, 0.3)) = 0.06
        _ShadowStrength ("Shadow strength", Range(0, 1)) = 0.85
        _TileMode ("Tile state mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_StateTex); SAMPLER(sampler_StateTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Lit, _Shade, _Spec, _Ink, _WashMorning, _WashMidday, _WashDusk;
            half _Step, _Feather, _SpecStep, _Outline, _OutlinePx, _Boil, _BoilPx, _BoilTime, _T, _Grain, _ShadowStrength, _TileMode;
        CBUFFER_END
        float h31(float3 p) { p = frac(p * 0.1031); p += dot(p, p.zyx + 31.32); return frac((p.x + p.y) * p.z); }
        float BoilClock()
        {
            float t = _BoilTime >= 0 ? _BoilTime : _T;
            return floor(t * 10.0 + 0.0001);
        }
        // UV channels win. They are linear. Vertex colour is only a fallback, and a near-white
        // colour means the mesh never stored a normal (Unity hands the shader white).
        float3 OutlineNormal(float3 meshNormal, float2 nxy, float2 nz, float4 color)
        {
            float3 fromUv = float3(nxy.x, nxy.y, nz.x);
            if (dot(fromUv, fromUv) > 0.04)
                return normalize(fromUv * 2.0 - 1.0);
            if (dot(color.rgb, float3(1, 1, 1)) < 2.7)
                return normalize(color.rgb * 2.0 - 1.0);
            return meshNormal;
        }
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
            struct A
            {
                float4 pos : POSITION;
                float3 n : NORMAL;
                float2 uv : TEXCOORD0;
                float2 nxy : TEXCOORD1;
                float2 nz : TEXCOORD2;
                float2 tile : TEXCOORD3;
                float4 col : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct V
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wn : TEXCOORD1;
                float3 wp : TEXCOORD2;
                float3 op : TEXCOORD3;
                float tile : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };
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
                o.tile = i.tile.x;
                return o;
            }
            half3 TileAlbedo(float2 uv, float tile)
            {
                int id = (int)round(tile);
                id = id < 0 ? 0 : (id > 20 ? 20 : id);
                half4 st = LOAD_TEXTURE2D(_StateTex, int2(id, 0));
                int state = (int)round(st.r * 4.0);
                int arc = (int)round(st.g * 2.0);
                half3 wash = arc <= 0 ? _WashMorning.rgb : (arc == 1 ? _WashMidday.rgb : _WashDusk.rgb);
                half3 paper = half3(0.953, 0.933, 0.886);
                half3 pale = half3(0.937, 0.910, 0.855);
                half3 pencil = half3(0.541, 0.506, 0.471);
                half3 ink = _Ink.rgb;
                float edge = smoothstep(0.10, 0.16, min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)));
                half3 col = paper;
                if (state == 1) col = wash;
                else if (state == 2)
                {
                    float hatch = step(0.55, frac((uv.x + uv.y) * 7.0));
                    col = lerp(paper, pencil, hatch * 0.7);
                    col = lerp(col, wash, 0.45);
                }
                else if (state == 3)
                {
                    col = lerp(pencil, pale, edge);
                }
                else if (state == 4)
                {
                    float dash = step(0.5, frac((uv.x + uv.y) * 5.0));
                    col = lerp(ink, paper, lerp(dash, 1.0, edge));
                }
                return col;
            }
            half4 frag (V i, bool front : SV_IsFrontFace) : SV_Target
            {
                half3 alb = _TileMode > 0.5 ? TileAlbedo(i.uv, i.tile) : SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                float3 n = normalize(i.wn) * (front ? 1 : -1);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.wp));
                half ndl = dot(n, L.direction);
                half sh = lerp(1, L.shadowAttenuation, _ShadowStrength);
                half lit = smoothstep(_Step - _Feather, _Step + _Feather, ndl) * smoothstep(0.35, 0.65, sh);
                half3 ramp = lerp(_Shade.rgb, _Lit.rgb, lit);
                float3 v = normalize(GetWorldSpaceViewDir(i.wp));
                float3 h = normalize(L.direction + v);
                half spec = smoothstep(_SpecStep - 0.01, _SpecStep + 0.01, dot(n, h)) * lit;
                // Object space, so the grain is stuck to the paper and does not swim when the dial is moved.
                half grain = (h31(floor(i.op * 2400)) - 0.5) * _Grain;
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
            struct A
            {
                float4 pos : POSITION;
                float3 n : NORMAL;
                float2 nxy : TEXCOORD1;
                float2 nz : TEXCOORD2;
                float4 col : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct V { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 nObj = OutlineNormal(i.n, i.nxy, i.nz, i.col);
                float3 wp = TransformObjectToWorld(i.pos.xyz);
                float3 wn = normalize(TransformObjectToWorldNormal(nObj));
                float frame = BoilClock();
                float j = h31(floor(wp * 220.0) + frame) * 2.0 - 1.0;
                float4 clip;
                if (_OutlinePx > 0.001)
                {
                    float3 nudged = wp + wn * 0.02;
                    float4 clip0 = TransformWorldToHClip(wp);
                    float4 clip1 = TransformWorldToHClip(nudged);
                    float2 dir = (clip1.xy / max(clip1.w, 1e-5)) - (clip0.xy / max(clip0.w, 1e-5));
                    float len = length(dir);
                    dir = len > 1e-5 ? dir / len : float2(0, 1);
                    float px = max(_OutlinePx + j * _BoilPx, 0);
                    clip = clip0;
                    clip.xy += dir * px * (2.0 / _ScreenParams.y) * clip.w;
                }
                else
                {
                    float w = _Outline * (1 + _Boil * j);
                    clip = TransformWorldToHClip(wp + wn * w);
                }
                o.pos = clip;
                return o;
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
                V o;
                float3 wp = TransformObjectToWorld(i.pos.xyz);
                float3 wn = TransformObjectToWorldNormal(i.n);
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
