// Drawn-leaf assembly (Sundial spike S3). One mesh of many small cards cut from one painted parts sheet.
// New shader, nothing else uses it. The look rules, in order:
//   1. Normals are transferred from a plant-sized ellipsoid (the mesh carries them), not taken from each card. A card
//      then shades like part of one rounded plant, and the one shade step is a clean terminator across the whole plant.
//   2. One shade step. _Lit and _Shade are multiplied in, the edge between them is a few pixels soft.
//   3. Alpha to coverage with a sharpened alpha, so thin ink survives the mips and the card edge is clean with MSAA.
//   4. Stepped sway. The tip moves on a held clock (_SwayFps) from object space and the vertex phase, never from the
//      eye or the screen, so both eyes agree. Vertex colour R is the sway weight (0 at the root), G is a per-card phase.
//   5. Stepped boil off. The card edge holds still. The ink is already a drawing.
//   6. _GVR_ROOMLIGHT (the S2 global keyword, off by default) multiplies the plant by the room-light cookie, read once at the
//      plant's base point in the vertex stage, so the whole plant takes one value as the plant cards do (Fidelity/Card). Off, the
//      shader compiles to the code above. The cap is +-15 percent and cream cannot brighten past 0.985, as in Fidelity/Toon.
// _T is written by the dial (the held clock), so captures are deterministic.
Shader "Fidelity/Leaf"
{
    Properties
    {
        _MainTex ("Parts atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Lit ("Lit multiply", Color) = (1.04, 1.03, 0.98, 1)
        _Shade ("Shade multiply", Color) = (0.74, 0.77, 0.86, 1)
        _LightDir ("Light direction (world, towards the light)", Vector) = (-0.28, 0.86, 0.42, 0)
        _Step ("Terminator", Range(-0.5, 0.8)) = 0.12
        _Soft ("Terminator softness", Range(0.001, 0.3)) = 0.05
        _Cut ("Alpha cut", Range(0.05, 0.95)) = 0.45
        _SwayAmp ("Sway amplitude (object m)", Float) = 0.0012
        _SwayFps ("Sway held rate", Float) = 6
        _T ("Time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest+5" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull Off
            AlphaToMask On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _GVR_ROOMLIGHT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            // Global, set by RoomLightGlobals. Outside the material CBUFFER on purpose.
            TEXTURE2D(_GvrRoomCookie); SAMPLER(sampler_GvrRoomCookie);
            float4x4 _GvrRoomW2C;
            float4 _GvrRoomParams;
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color, _Lit, _Shade;
                float4 _LightDir;
                half _Step, _Soft, _Cut;
                float _SwayAmp, _SwayFps, _T;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; half4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; half room : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = i.pos.xyz;
                // Held clock: the plant moves on 6 fps steps, like animation on threes, and never between them.
                float held = floor(_T * _SwayFps + 0.0001) / max(_SwayFps, 1.0);
                float ph = i.col.g * 6.2831853;
                float w = i.col.r * i.col.r;
                p.x += _SwayAmp * w * sin(held * 1.9 + ph);
                p.z += _SwayAmp * w * 0.6 * cos(held * 1.3 + ph * 1.3);
                o.pos = TransformObjectToHClip(p);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                o.wn = TransformObjectToWorldNormal(i.n);
                o.room = 1;
                #if defined(_GVR_ROOMLIGHT)
                {
                    // The plant's base point (the object origin), so the cookie does not smear up the plant.
                    float3 lp = mul(_GvrRoomW2C, float4(TransformObjectToWorld(float3(0, 0, 0)), 1.0)).xyz;
                    float2 cuv = lp.xz / max(_GvrRoomParams.x, 1e-4) + 0.5;
                    float cd = SAMPLE_TEXTURE2D_LOD(_GvrRoomCookie, sampler_GvrRoomCookie, cuv, 0).r * 2.0 - 1.0;
                    o.room = (half)pow(1.0 + _GvrRoomParams.y * clamp(cd * _GvrRoomParams.z + _GvrRoomParams.w, -1.0, 1.0), 2.2);
                }
                #endif
                return o;
            }
            half4 frag (V i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                // Sharpened alpha: the mip that blurs a thin stroke keeps its coverage (Golus, alpha to coverage).
                half a = saturate((t.a - _Cut) / max(fwidth(t.a), 0.0001) + 0.5);
                if (a < 0.02) discard;
                float3 L = normalize(_LightDir.xyz);
                float ndl = dot(normalize(i.wn), L);
                half lit = smoothstep(_Step - _Soft, _Step + _Soft, ndl);
                half3 rgb = t.rgb * _Color.rgb * lerp(_Shade.rgb, _Lit.rgb, lit);
                #if defined(_GVR_ROOMLIGHT)
                {
                    half peak = max(max(rgb.r, rgb.g), max(rgb.b, 1e-3));
                    rgb *= min(i.room, max((half)1.0, (half)(0.985 / peak)));
                }
                #endif
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
