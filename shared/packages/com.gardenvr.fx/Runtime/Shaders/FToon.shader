// Drawn dial body. Painted albedo times a two-step toon ramp (lit 1, shade about 0.78 with a warm shift),
// paper grain in object space, and an inverted-hull ink outline. The hull pushes back faces along the
// smoothed normal stored in UV1/UV2 (Nxy, Nz), so hard shading normals can stay on the mesh.
// _OutlinePx is the width in screen pixels. _BoilPx is the 10 fps jitter in pixels (keep it at or under 1.2).
// A negative _BoilTime falls back to _T, which is what older materials set.
// _TileMode draws one combined tile mesh: UV3.x is the tile index, _StateTex is a 21-wide point texture
// (R = state/4, G = arc/2, B = ink flood 0-1 from the nib at uv.x = 0). _TileMode 0 leaves the ramp path unchanged.
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
        _WashMorning ("Morning tile wash", Color) = (0.965, 0.871, 0.718, 1)
        _WashMidday ("Midday tile wash", Color) = (0.957, 0.714, 0.631, 1)
        _WashDusk ("Dusk tile wash", Color) = (0.792, 0.690, 0.773, 1)
        _TileTex ("Painted tile atlas (4 columns)", 2D) = "white" {}
        _TilePaint ("Use painted tiles", Float) = 0
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
        TEXTURE2D(_TileTex); SAMPLER(sampler_TileTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _TileTex_ST;
            half4 _Lit, _Shade, _Spec, _Ink, _WashMorning, _WashMidday, _WashDusk;
            half _Step, _Feather, _SpecStep, _Outline, _OutlinePx, _Boil, _BoilPx, _BoilTime, _T, _Grain, _ShadowStrength, _TileMode, _TilePaint;
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
                // B is the ink flood. 1 is a finished tile. A kept tile below 1 is still leaving the nib.
                float fill = saturate(st.b);
                half3 wash = arc <= 0 ? _WashMorning.rgb : (arc == 1 ? _WashMidday.rgb : _WashDusk.rgb);
                half3 paper = half3(0.965, 0.949, 0.914);
                half3 pale = half3(0.937, 0.910, 0.855);
                half3 pencil = half3(0.541, 0.506, 0.471);
                half3 ink = _Ink.rgb;
                // uv.x 0 is the nib end of the raised tile (the -tangent corner).
                float dry = smoothstep(fill - 0.035, fill + 0.012, uv.x);
                int face = state;
                if (state == 1 && fill < 0.995)
                    face = dry > 0.5 ? 4 : 1;

                half3 col = paper;
                if (face == 1) col = wash;
                else if (face == 2) col = lerp(paper, wash, 0.50);
                else if (face == 3) col = pale;

                // Painted atlas: morning, midday, dusk, cream. Before stays blank paper.
                // Hatch and the dashed outline are drawn after this, so the paint cannot erase the shape.
                if (_TilePaint > 0.5 && face != 0)
                {
                    int paintCol = (face == 1 || face == 2) ? arc : 3;
                    float2 tuv = float2((saturate(uv.x) + (float)paintCol) * 0.25, saturate(uv.y));
                    half3 paint = SAMPLE_TEXTURE2D(_TileTex, sampler_TileTex, tuv).rgb;
                    float paintMix = face == 1 ? 0.9 : (face == 3 ? 0.40 : 0.25);
                    col = lerp(col, paint, paintMix);
                }

                float2 q = uv - 0.5;
                float wob = (h31(float3(uv.y * 13.0, tile + 1.7, uv.x * 9.0)) - 0.5) * 0.055;
                float box = max(abs(q.x) + wob, abs(q.y) - wob * 0.6);
                float rim = smoothstep(0.44, 0.50, box);
                float grain = h31(float3(floor(uv * 22.0), tile * 1.3));
                float border = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                if (face == 1)
                {
                    half3 wet = arc <= 0 ? half3(0.93, 0.72, 0.42) : (arc == 1 ? half3(0.90, 0.48, 0.40) : half3(0.62, 0.50, 0.78));
                    col = lerp(col, wet, smoothstep(0.22, 0.42, box) * 0.28);
                    col *= lerp(0.96, 1.05, grain);
                }
                else if (face == 2)
                {
                    col *= lerp(0.95, 1.04, grain);
                    float hatch = step(0.58, frac((uv.x * 1.15 + uv.y) * 8.0));
                    col = lerp(col, pencil, hatch * 0.82);
                }
                else if (face == 3)
                {
                    float edge = smoothstep(0.07, 0.13, border);
                    col = lerp(pencil, col, edge);
                }
                else if (face == 4)
                {
                    float edge = smoothstep(0.08, 0.15, border);
                    float dash = step(0.42, frac(uv.x * 5.0 + uv.y * 2.0));
                    col = lerp(lerp(ink, paper, dash), col, edge);
                }

                // Before is blank paper. Today and missed already drew their own outline.
                float rimAmt = _TilePaint > 0.5 ? 0.28 : 0.7;
                if (face == 0) rimAmt = 0.08;
                if (face == 3 || face == 4) rimAmt = 0.0;
                if (face == 2) rimAmt *= 0.35;
                col = lerp(col, ink, rim * rimAmt);
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
                // Only near-white paper is held up. A floor that starts at mid grey flattens
                // soil stipple and the darker grains in a wash. Ink still takes the shade step.
                half luma = dot(alb, half3(0.2126, 0.7152, 0.0722));
                half keep = smoothstep(0.70, 0.88, luma);
                ramp = lerp(ramp, max(ramp, half3(0.94, 0.91, 0.86)), keep);
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
                // Held thick-and-thin along the stroke. The boil jitter sits on top of it.
                float wobble = lerp(0.86, 1.14, h31(floor(wp * 48.0)));
                float4 clip;
                if (_OutlinePx > 0.001)
                {
                    float3 nudged = wp + wn * 0.02;
                    float4 clip0 = TransformWorldToHClip(wp);
                    float4 clip1 = TransformWorldToHClip(nudged);
                    float2 dir = (clip1.xy / max(clip1.w, 1e-5)) - (clip0.xy / max(clip0.w, 1e-5));
                    float len = length(dir);
                    dir = len > 1e-5 ? dir / len : float2(0, 1);
                    float px = max(_OutlinePx * wobble + j * _BoilPx, 0);
                    clip = clip0;
                    clip.xy += dir * px * (2.0 / _ScreenParams.y) * clip.w;
                }
                else
                {
                    float w = _Outline * wobble * (1 + _Boil * j);
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
