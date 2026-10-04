// Spike S4 (T-TER-043). The fuzzy, self-lit fiddlehead. Variant B only; Fidelity/Glow stays the locked A fiddle.
//
// One mesh holds the tube N+1 times (JarView, S4Fiddle.BuildStack). Copy 0 is the opaque tube. Copy k carries
// t = k / N in TEXCOORD1.x. Each shell is pushed out along its normal by t * _FurLen and keeps only the texels whose
// hair height (a tiling strand map, R) is at least t, so the outline is a fringe of short hairs and not a clean tube.
//
// Core gradient: vertex colour R is 0 on the stem and rises to 1 at the centre of the spiral. It lifts the emission
// toward _CoreColor, so the coil core is the brightest yellow-green in the jar (fiddlehead.core in the style bible).
//
// After the albedo the equation is Fidelity/Glow's: gradient, emission mask, rim, focused light, SoftBiolume cap at
// linear #E8FFF4. Property names match Glow, so JarView drives both with one code path.
Shader "Fidelity/FiddleFuzz"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _EmissionTex ("Emission mask", 2D) = "white" {}
        _Emission ("Emission colour (HDR)", Color) = (0,0,0,1)
        _Rim ("Fuzz rim colour", Color) = (0,0,0,1)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.5
        _GradBottom ("Self-light at bottom", Float) = 0.6
        _GradTop ("Self-light at top", Float) = 1.2
        _GradY ("Gradient y range (min,max, world)", Vector) = (0, 0.1, 0, 0)
        _LightPos ("Focused light (xyz, radius; 0 = off)", Vector) = (0, 0, 0, 0)
        _LightColor ("Focused light colour", Color) = (0, 0, 0, 1)
        _Trans ("Translucency", Range(0, 3)) = 0
        _Tip ("Tip glow (world y0, y1, floor, peak)", Vector) = (0, 1, 1, 1)
        _StrandTex ("Hair height (R)", 2D) = "black" {}
        _StrandTile ("Hair tiles (u around, v along)", Vector) = (1, 1, 0, 0)
        _FurLen ("Shell stack depth (m)", Float) = 0.0016
        _AoBase ("Self-occlusion at the base of the fuzz", Range(0, 1)) = 0.55
        _FuzzTint ("Fuzz tip colour (multiplies the albedo)", Color) = (1.15, 1.18, 1.0, 1)
        _EmScale ("Emission scale (the flat albedo has no dark hair gaps)", Range(0, 2)) = 1
        _Flat ("Albedo flattening toward the tint (0 = hair photo)", Range(0, 1)) = 0
        _CoreColor ("Core glow colour (HDR)", Color) = (0.30, 0.34, 0.06, 1)
        _CoreBoost ("Core emission multiplier", Range(1, 4)) = 1.6
        _CorePower ("Core falloff", Range(0.5, 4)) = 1.4
        [Enum(Off,0,On,1)] _AlphaToMask ("Alpha to coverage", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            AlphaToMask [_AlphaToMask]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
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
            TEXTURE2D(_StrandTex); SAMPLER(sampler_StrandTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; half4 _Tint, _Emission, _Rim; half _RimPower, _GradBottom, _GradTop; float4 _GradY;
                float4 _LightPos; half4 _LightColor; half _Trans; float4 _Tip;
                float4 _StrandTile; float _FurLen; half _AoBase, _CoreBoost, _CorePower, _AlphaToMask;
                half4 _FuzzTint, _CoreColor; half _Flat, _EmScale;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; float4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; float shell : TEXCOORD3; half core : TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A i)
            {
                V o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float t = i.uv2.x;
                o.wn = TransformObjectToWorldNormal(i.n);
                // A little lean per hair so the shells do not stack into rings along the tube.
                float2 curl = float2(sin(i.uv.x * 97.0 + i.uv.y * 41.0), cos(i.uv.y * 83.0 - i.uv.x * 59.0));
                float3 sway = float3(curl.x, curl.y * 0.5, curl.y) * (_FurLen * 0.25 * t * t);
                o.wp = TransformObjectToWorld(i.pos.xyz) + normalize(o.wn) * (_FurLen * t) + sway;
                o.pos = TransformWorldToHClip(o.wp);
                o.uv = i.uv;
                o.shell = t;
                o.core = (half)i.col.r;
                return o;
            }
            half4 frag (V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.wn) * (front ? 1 : -1);
                half4 alb = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv * _MainTex_ST.xy + _MainTex_ST.zw) * _Tint;
                alb.rgb = lerp(alb.rgb, _Tint.rgb, _Flat);
                half em = SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, i.uv * _MainTex_ST.xy + _MainTex_ST.zw).r;
                half t = (half)i.shell;
                half outA = 1;
                half ao = 1;
                if (t > 0.001)
                {
                    float height = SAMPLE_TEXTURE2D(_StrandTex, sampler_StrandTex, i.uv * _StrandTile.xy).r;
                    float w = clamp(fwidth(height), 1e-3, 0.30);
                    float cov = saturate((height - t) / w + 0.5);
                    outA = (half)cov;
                    clip(cov - lerp(0.5, 0.02, _AlphaToMask));
                }
                // Hairs are dark at the root and take the lighter fuzz tint at the tip.
                if (t > 0.001) ao = lerp(_AoBase, 1.0h, (half)pow(t, 0.7));
                alb.rgb *= ao * lerp(half3(1, 1, 1), _FuzzTint.rgb, t);
                em *= ao * _EmScale;

                // Core gradient. The spiral centre is the brightest part of the coil.
                half core = pow(saturate(i.core), _CorePower);
                em *= lerp(1.0h, _CoreBoost, core);

                half tipT = saturate((i.wp.y - _Tip.x) / max(1e-4, _Tip.y - _Tip.x));
                em *= lerp(_Tip.z, _Tip.w, tipT);
                float3 v = normalize(GetWorldSpaceViewDir(i.wp));
                // The fuzz catches the rim light, which is what softens the outline.
                half rim = pow(1 - saturate(abs(dot(n, v))), _RimPower) * lerp(0.6h, 1.0h, t);
                half g = lerp(_GradBottom, _GradTop, saturate((i.wp.y - _GradY.x) / max(1e-4, _GradY.y - _GradY.x)));
                half3 c = alb.rgb * g + _Emission.rgb * em + _Rim.rgb * rim + _CoreColor.rgb * core * ao;
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
                    c += _LightColor.rgb * (half)atten * wrap * (alb.rgb * 0.55h + 0.45h);
                    c += _LightColor.rgb * (half)atten * back * _Trans * lerp(0.35h, 1.0h, blade);
                }
                c = SoftBiolume(c);
                return half4(c, outA);
            }
            ENDHLSL
        }
    }
}
