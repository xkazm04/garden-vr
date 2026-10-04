// Spike S4 (T-TER-043). Backlit fronds. Variant B only; Fidelity/Glow stays the locked A.
//
// The colour equation is Fidelity/Glow's, property for property, so JarView drives both with the same code. The
// addition is a transmission term (Barre-Brisebois GDC 2011, Crysis back-lighting, GPU Gems 3 ch.16):
//   trans = (1 - thickness) * pow(saturate(dot(V, -L)), _BackPower) * wrap
// L is the direction from the surface to a light that sits behind the jar centre on the eye ray, so the pinnae
// between the eye and the glow light up and the pinnae off to the side do not. The thickness map is thin at the
// pinna margins and thick on the rachis and veins. It is ALU only: one extra texture sample, no extra pass.
Shader "Fidelity/FrondBacklit"
{
    Properties
    {
        _MainTex ("Albedo (A = cutout)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _EmissionTex ("Emission mask", 2D) = "white" {}
        _Emission ("Emission colour (HDR)", Color) = (0,0,0,1)
        _Rim ("Fuzz rim colour", Color) = (0,0,0,1)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.5
        _GradBottom ("Self-light at bottom", Float) = 0.6
        _GradTop ("Self-light at top", Float) = 1.2
        _GradY ("Gradient y range (min,max, world)", Vector) = (0, 0.1, 0, 0)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.0
        _TopTex ("Top texture (planar, world xz)", 2D) = "white" {}
        _TopTile ("Top tiling", Float) = 10
        _TopAmount ("Top blend", Range(0,1)) = 0
        _Shell ("Shell offset along normal (m)", Float) = 0
        _Tri ("Triplanar tiling for _MainTex (0 = mesh UV)", Float) = 0
        // Focused light is off unless the radius (_LightPos.w) is positive, so older materials keep the flat gradient.
        _LightPos ("Focused light (xyz, radius; 0 = off)", Vector) = (0, 0, 0, 0)
        _LightColor ("Focused light colour", Color) = (0, 0, 0, 1)
        _Trans ("Translucency", Range(0, 3)) = 0
        _Soft ("Soft edge dither", Range(0, 0.6)) = 0
        // floor == peak leaves emission flat. (0, 1, 1, 1) is that default, so older materials do not change.
        _Tip ("Tip glow (world y0, y1, floor, peak)", Vector) = (0, 1, 1, 1)
        _Edge ("Shape-edge glow", Range(0, 2)) = 0
        _ThickTex ("Thickness (R): 0 thin, 1 thick", 2D) = "white" {}
        _BackColor ("Transmitted light colour", Color) = (0.55, 0.95, 0.70, 1)
        _BackStrength ("Transmission strength", Range(0, 3)) = 1
        _BackPower ("Transmission falloff", Range(0.5, 16)) = 3
        _BackWrap ("Light wrap through the blade", Range(0, 1)) = 0.5
        // xyz = the jar glow centre in world space. w = how far behind it (along the eye ray) the light sits.
        _BackCenter ("Backlight centre (xyz), depth behind (w)", Vector) = (0, 0, 0, 0.04)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // #E8FFF4 in linear. A bright saturated green used to clip one channel and read as neon.
            // Warm pixels (cork, soil, flower) and anything under the cap stay as authored.
            half3 SoftBiolume(half3 c)
            {
                half greenness = c.g - max(c.r, c.b);
                if (greenness <= 0.02) return c;
                half3 cap = half3(0.804, 1.0, 0.903);
                half peak = max(c.r, max(c.g, c.b));
                half lo = min(c.r, min(c.g, c.b));
                half sat = (peak - lo) / max(peak, 1e-3);
                half hot = saturate((peak - 0.55) / 0.40) * saturate((sat - 0.28) / 0.40) * saturate(greenness / 0.15);
                half y = dot(c, half3(0.2126, 0.7152, 0.0722));
                half capY = dot(cap, half3(0.2126, 0.7152, 0.0722));
                half3 pale = cap * (y / max(capY, 1e-3));
                c = lerp(c, pale, hot * 0.70);
                half3 over = max(c - cap, 0);
                return min(c, cap) + over / (1.0 + over * 4.0);
            }
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionTex); SAMPLER(sampler_EmissionTex);
            TEXTURE2D(_TopTex); SAMPLER(sampler_TopTex);
            TEXTURE2D(_ThickTex); SAMPLER(sampler_ThickTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; half4 _Tint, _Emission, _Rim; half _RimPower, _GradBottom, _GradTop, _Cutoff, _TopTile, _TopAmount, _Shell, _Tri; float4 _GradY;
                float4 _LightPos; half4 _LightColor; half _Trans, _Soft; float4 _Tip; half _Edge;
                half4 _BackColor; half _BackStrength, _BackPower, _BackWrap; float4 _BackCenter;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.wn = TransformObjectToWorldNormal(i.n);
                o.wp = TransformObjectToWorld(i.pos.xyz) + normalize(o.wn) * _Shell; o.pos = TransformWorldToHClip(o.wp);   // fuzz shells push out along the normal
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }
            half4 frag (V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.wn) * (front ? 1 : -1);
                half4 alb; half em;
                if (_Tri > 0)
                {   // triplanar: the moss texture never stretches down the steep front of the mound
                    float3 w = pow(abs(n), 4); w /= (w.x + w.y + w.z);
                    float3 p = i.wp * _Tri;
                    alb = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.zy) * w.x + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.xz) * w.y + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.xy) * w.z;
                    alb *= _Tint; em = dot(alb.rgb, half3(0.3, 0.6, 0.1));
                }
                else
                {
                    alb = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Tint;
                    em = SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, i.uv).r;
                }
                if (_Shell > 0) clip(abs(n.y) - 0.45);   // fuzz only on the cap, never on the vertical skirt
                // caps (moss top, cork top) take a planar texture so the lathe's pole never shows its radial pinch
                half topw = _TopAmount * smoothstep(0.45, 0.75, abs(n.y));
                half3 top = SAMPLE_TEXTURE2D(_TopTex, sampler_TopTex, i.wp.xz * _TopTile).rgb * _Tint.rgb;
                alb.rgb = lerp(alb.rgb, top, topw); em = lerp(em, dot(top, half3(0.3, 0.6, 0.1)), topw);
                // Height window. Equal floor and peak multiply by 1, which is every material that does not opt in.
                half tipT = saturate((i.wp.y - _Tip.x) / max(1e-4, _Tip.y - _Tip.x));
                em *= lerp(_Tip.z, _Tip.w, tipT);
                // A zero _Soft keeps the old hard cutoff. A small dither feathers a card silhouette without a blend pass.
                if (_Soft > 0.001)
                {
                    float h = frac(sin(dot(floor(i.pos.xy), float2(12.9898, 78.233))) * 43758.5453);
                    clip(alb.a - (_Cutoff + (h - 0.5) * _Soft));
                }
                else
                    clip(alb.a - _Cutoff);
                float3 v = normalize(GetWorldSpaceViewDir(i.wp));
                half rim = pow(1 - saturate(abs(dot(n, v))), _RimPower);
                half g = lerp(_GradBottom, _GradTop, saturate((i.wp.y - _GradY.x) / max(1e-4, _GradY.y - _GradY.x)));
                half3 c = alb.rgb * g + _Emission.rgb * em + _Rim.rgb * rim;
                // Radius 0 leaves the equation above untouched. A positive radius is a soft point at the crozier or flower.
                if (_LightPos.w > 0.001)
                {
                    float3 toL = _LightPos.xyz - i.wp;
                    float dist = length(toL);
                    float3 L = toL / max(dist, 1e-4);
                    float atten = saturate(1.0 - dist / _LightPos.w);
                    atten *= atten;
                    half ndl = dot(n, L);
                    half wrap = saturate(ndl * 0.5 + 0.5);
                    half back = saturate(-ndl);
                    half blade = saturate(em);
                    // Part of the light is the glow itself. A dark albedo used to swallow the crozier point.
                    c += _LightColor.rgb * (half)atten * wrap * (alb.rgb * 0.55h + 0.45h);
                    c += _LightColor.rgb * (half)atten * back * _Trans * lerp(0.35h, 1.0h, blade);
                }
                // Backlight through the blade. Zero strength leaves the Glow equation untouched.
                if (_BackStrength > 0.001)
                {
                    half thick = SAMPLE_TEXTURE2D(_ThickTex, sampler_ThickTex, i.uv).r;
                    float3 behind = _BackCenter.xyz + normalize(_BackCenter.xyz - _WorldSpaceCameraPos) * _BackCenter.w;
                    float3 Lb = normalize(behind - i.wp);
                    half facing = pow(saturate(dot(v, -Lb)), _BackPower);
                    // n faces the eye. The light is on the far side when dot(n, Lb) is negative. Wrap lets it bleed round.
                    half through = saturate(-dot(n, Lb) * (1.0h - _BackWrap) + _BackWrap);
                    half trans = (1.0h - thick) * facing * through;
                    // Tinted by the blade's own colour so the pinna keeps its shape, and a floor so the margin glows even on a pale texel.
                    c += _BackColor.rgb * trans * _BackStrength * (alb.rgb * 0.6h + 0.4h);
                }
                // Alpha contour of a cutout card (frond pinnae). Zero leaves the silhouette alone.
                if (_Edge > 0.001)
                {
                    half outline = saturate(fwidth(alb.a) * 4.5);
                    c += _Rim.rgb * outline * _Edge * saturate(alb.a * 2.0);
                }
                c = SoftBiolume(c);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
