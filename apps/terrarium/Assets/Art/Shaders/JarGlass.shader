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
        _Studio ("S1 studio strip, six faces", 2D) = "black" {}
        _DropN ("S1 droplets (RG normal, B height, A mask)", 2D) = "black" {}
        _RefractStrip ("S2 baked refraction strip, six faces", 2D) = "black" {}
        _S2Cfg ("S2 (wall offset m, dispersion, mix, proxy m)", Vector) = (0.010, 0.06, 1, 0.25)
        _S5Haze ("S5 haze (sigma per m, colour strength, top gain, noise)", Vector) = (0, 0, 0, 0)
        _S5Cyl ("S5 cavity (radius m, bottom y, top y, clock s)", Vector) = (0.041, 0.034, 0.118, 0)
        _S6Bed ("S6 bed glow (strength, centre height m, falloff m)", Vector) = (0, 0, 0, 0)
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
            #pragma multi_compile_local _ _S1_ON
            #pragma multi_compile_local _ _S2_B0 _S2_CUBE _S2_OPAQUE
            #pragma multi_compile_local _ _S5_HAZE
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
            #pragma multi_compile_local _ _S1_ON
            #pragma multi_compile_local _ _S2_B0 _S2_CUBE _S2_OPAQUE
            #pragma multi_compile_local _ _S5_HAZE
            V vert(A i) { return GlassVert(i); }
            half4 fragF(V i) : SV_Target { return GlassShade(i, 0); }
            ENDHLSL
        }
    }
}
