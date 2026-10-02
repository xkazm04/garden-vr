// The drawn dial's shadow on the real table: a transparent desk plane that multiplies the room by a painted shadow tone
// where the main-light shadow map says the dial blocks the sun. On Quest the same plane sits on the scene-model desk.
Shader "Fidelity/ShadowCatcher"
{
    Properties { _ShadowColor ("Shadow tone (multiply)", Color) = (0.62, 0.55, 0.50, 1) _Fade ("Fade radius (m)", Float) = 0.4 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend DstColor Zero
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _ShadowColor; half _Fade; CBUFFER_END
            #pragma multi_compile_instancing
            struct A { float4 pos : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A i) { V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.wp = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.wp); return o; }
            half4 frag (V i) : SV_Target
            {
                half s = GetMainLight(TransformWorldToShadowCoord(i.wp)).shadowAttenuation;
                half f = saturate(1 - length(i.wp.xz - TransformObjectToWorld(float3(0, 0, 0)).xz) / _Fade);   // fade around the dial, wherever it is placed
                return half4(lerp(half3(1, 1, 1), _ShadowColor.rgb, (1 - s) * f), 1);
            }
            ENDHLSL
        }
    }
}
