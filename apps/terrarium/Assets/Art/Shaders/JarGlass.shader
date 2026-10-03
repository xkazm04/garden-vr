// Night jar glass. The shared function is GlassCommon.hlsl (Fidelity/Glass uses it too).
// Output is premultiplied. The centre of the pane is clear. Thickness, refraction, and
// the upper-third beads live in the include.
Shader "Fidelity/JarGlass"
{
    Properties
    {
        _Cond ("Condensation (R drops, G highlight, B haze)", 2D) = "black" {}
        _Bead ("Droplet normal (RGB) and alpha cut (A)", 2D) = "black" {}
        _Tint ("Body tint (A = base opacity)", Color) = (0.75, 0.94, 0.84, 0.004)
        _Rim ("Rim colour (A = opacity)", Color) = (0.48, 0.72, 0.78, 0.32)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.4
        _Inner ("Inner light (HDR)", Color) = (0.34, 0.86, 0.48, 1)
        _InnerY ("Inner light (full until y, fade length)", Vector) = (0.078, 0.052, 0, 0)
        _Volume ("Edge absorption (RGB tint, A = darken)", Color) = (0.42, 0.62, 0.66, 0.75)
        _VolumeY ("Kept for the material. Absorption follows the fresnel.", Vector) = (0.038, 0.072, 0, 0)
        _Fog ("Breath fog", Range(0,1)) = 0.3
        _Drops ("Droplet strength", Range(0,2)) = 1.15
        _Refract ("Edge refraction, pixels", Range(0, 48)) = 36
        _Streak ("Streak", Color) = (0.50, 0.66, 0.74, 0.12)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        HLSLINCLUDE
        #include "Packages/com.gardenvr.fx/Runtime/Shaders/GlassCommon.hlsl"
        ENDHLSL
        Pass
        {
            Name "Back"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragB
            #pragma multi_compile_instancing
            V vert(A i) { return GlassVert(i); }
            half4 fragB(V i) : SV_Target { return GlassShade(i, 1); }
            ENDHLSL
        }
        Pass
        {
            Name "Front"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragF
            #pragma multi_compile_instancing
            V vert(A i) { return GlassVert(i); }
            half4 fragF(V i) : SV_Target { return GlassShade(i, 0); }
            ENDHLSL
        }
    }
}
