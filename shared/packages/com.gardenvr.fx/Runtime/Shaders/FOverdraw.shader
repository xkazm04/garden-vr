// Measurement only: every transparent fragment adds 1/32 to red, depth-tested against the opaque scene.
// Read back, R*32 = how many transparent layers each pixel paid for.
Shader "Fidelity/Overdraw"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One ZWrite Off Cull Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; };
            V vert (A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); return o; }
            half4 frag (V i) : SV_Target { return half4(1.0 / 32.0, 0, 0, 1); }
            ENDHLSL
        }
    }
}
