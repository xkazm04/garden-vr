// The drawn dial's contact on the real table. A transparent desk plane multiplies the room by a painted shadow tone.
// The shadow map still darkens where a caster blocks the sun. _Contact (default 0) adds a soft disc so the dial
// sits on the desk when realtime casters are off. Terrarium does not set _Contact, so its result is unchanged.
Shader "Fidelity/ShadowCatcher"
{
    Properties
    {
        _ShadowColor ("Shadow tone (multiply)", Color) = (0.62, 0.55, 0.50, 1)
        _Fade ("Fade radius (m)", Float) = 0.4
        _Contact ("Painted contact 0..1", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend DstColor Zero
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half _Fade;
                half _Contact;
            CBUFFER_END
            #pragma multi_compile_instancing
            struct A { float4 pos : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.wp = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.wp);
                return o;
            }
            half4 frag (V i) : SV_Target
            {
                // No shadow keyword (the sundial lights cast none) must stay at 1. Sampling the
                // shadow map then returns garbage and the multiply blows out into a bright band.
                half s = 1;
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    s = saturate(GetMainLight(TransformWorldToShadowCoord(i.wp)).shadowAttenuation);
                #endif
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                half f = saturate(1 - length(i.wp.xz - origin.xz) / max(_Fade, 0.001));
                half k = saturate((1 - s) * f + _Contact * f);
                return half4(lerp(half3(1, 1, 1), _ShadowColor.rgb, k), 1);
            }
            ENDHLSL
        }
    }
}
