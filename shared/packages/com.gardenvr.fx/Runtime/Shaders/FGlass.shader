// Night jar glass. Two passes: back wall (Cull Front), then front wall (Cull Back).
// The terrarium jar mesh has the back faces removed and turns the back pass off.
// _Refract 0 keeps the previous look and does not sample the opaque copy.
// Above 0.5, GlassCommon adds thickness, the edge bend, and the droplet map.
Shader "Fidelity/Glass"
{
    Properties
    {
        _Cond ("Condensation (R drops, G highlight, B haze)", 2D) = "black" {}
        _Bead ("Droplet normal (RGB) and alpha cut (A)", 2D) = "black" {}
        _Tint ("Body tint (A = base opacity)", Color) = (0.3, 0.8, 0.65, 0.06)
        _Rim ("Rim colour (A = opacity)", Color) = (0.6, 1, 0.85, 0.7)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.6
        _Inner ("Inner scatter (HDR)", Color) = (0.2, 0.9, 0.6, 1)
        _InnerY ("Inner light (full until y, fade length)", Vector) = (0.04, 0.06, 0, 0)
        _Volume ("Edge absorption (RGB tint, A = darken)", Color) = (0.42, 0.62, 0.66, 0.75)
        _VolumeY ("Kept for older materials.", Vector) = (0.038, 0.072, 0, 0)
        _Fog ("Breath fog", Range(0,1)) = 0.3
        _Drops ("Droplet strength", Range(0,2)) = 1
        _Refract ("Edge refraction, pixels", Range(0, 48)) = 0
        _Streak ("Streak", Color) = (0.8, 1, 0.95, 0.35)
        _Studio ("S1 studio strip, six faces", 2D) = "black" {}
        _DropN ("S1 droplets (RG normal, B height, A mask)", 2D) = "black" {}
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
