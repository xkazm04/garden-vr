// Every card in both heroes: halo cards (instead of bloom), spores, mist, the breath ring, the desk light pool, the plant
// cards and the pinch halo. _Mode: 0 additive (glow), 1 alpha-blended (mist, drawn plants).
// _Ring > 0 turns on the breath-ring sweep: R = line, G = pool; _Fill is the breath progress 0..1 around the circle.
// _Boil > 0 jitters UVs on a held 10 fps clock: the redraw shimmer of hand-drawn animation ("boiling lines").
Shader "Fidelity/Card"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Colour (HDR)", Color) = (1,1,1,1)
        _Color2 ("Second colour (ring pool / halo glow)", Color) = (0,0,0,0)
        [Enum(UnityEngine.Rendering.BlendMode)] _Src ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Dst", Float) = 1
        _Ring ("Ring mode", Float) = 0
        _Fill ("Ring fill 0..1", Range(0,1)) = 1
        _Boil ("Boil amount (UV)", Float) = 0
        _BoilFps ("Boil fps", Float) = 10
        _T ("Time (driven by script for deterministic captures)", Float) = 0
        _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend [_Src] [_Dst]
            ZWrite Off Cull Off ZTest [_ZTest]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; half4 _Color, _Color2; half _Ring, _Fill, _Boil, _BoilFps, _T, _Src, _Dst, _ZTest;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = TRANSFORM_TEX(i.uv, _MainTex); o.col = i.col; return o;
            }
            float h21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            half4 frag (V i) : SV_Target
            {
                float2 uv = i.uv;
                if (_Boil > 0)
                {
                    float k = floor(_T * _BoilFps);
                    float2 cell = floor(uv * 6);
                    uv += (float2(h21(cell + k), h21(cell + k + 17.3)) - 0.5) * _Boil;
                }
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half4 c;
                if (_Ring > 0)
                {
                    float2 p = i.uv * 2 - 1;
                    float ang = frac(atan2(p.x, -p.y) / 6.2831853 + 0.5);   // 0 at the front of the jar, clockwise
                    half lit = smoothstep(_Fill + 0.004, _Fill - 0.004, ang);
                    half ln = t.r * (0.18 + 0.82 * lit);
                    c = half4(_Color.rgb * ln + _Color2.rgb * t.g, 1);
                }
                else if (_Src > 4.5) // alpha-blended: SrcAlpha / OneMinusSrcAlpha
                    c = half4(t.rgb * _Color.rgb, t.a * _Color.a);
                else                 // additive glow; for the halo line R = line, G = soft glow
                    c = half4(_Color.rgb * t.r + _Color2.rgb * t.g, 1);
                return c * i.col;
            }
            ENDHLSL
        }
    }
}
