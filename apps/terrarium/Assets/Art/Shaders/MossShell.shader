// Spike S3 (T-TER-042). Shell moss and its sprig cards. Variant B only; MossVelvet stays the locked A.
//
// Shell mode (_Cards 0). One mesh holds N copies of the mound. Copy k carries t = k / N in TEXCOORD1.x.
// Each copy is pushed out along its normal by t * _FurLen and keeps only the texels whose strand height
// (s3_strand.png, R) is at least t, so a strand is a stack of shrinking discs. t = 0 is the opaque base.
// Cards mode (_Cards 1). The sprig cards. The cutout is anti-aliased with alpha to coverage, which needs MSAA.
//
// The colour equation after the albedo is MossVelvet's: gradient, emission mask, rim, focused light,
// SoftBiolume cap at linear #E8FFF4. Moss is not given extra emission: the pockets between tips stay dark.
Shader "Fidelity/MossShell"
{
    Properties
    {
        _MainTex ("Albedo (A = cutout in cards mode)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _EmissionTex ("Emission mask", 2D) = "white" {}
        _Emission ("Emission colour (HDR)", Color) = (0,0,0,1)
        _Rim ("Fuzz rim colour", Color) = (0,0,0,1)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.5
        _GradBottom ("Self-light at bottom", Float) = 0.6
        _GradTop ("Self-light at top", Float) = 1.2
        _GradY ("Gradient y range (min,max, world)", Vector) = (0, 0.1, 0, 0)
        _Cutoff ("Alpha cutoff (cards)", Range(0,1)) = 0.04
        _StrandTex ("Strand height (R), clump shade (G), strand tint (B)", 2D) = "black" {}
        _StrandTile ("Strand tiles across the mound UV", Float) = 3.6
        _FurLen ("Shell stack depth (m)", Float) = 0.0026
        _AoBase ("Self-occlusion at the base of the fur", Range(0, 1)) = 0.16
        _TipBoost ("Tip brightening on the top shells", Range(0.5, 2)) = 1.25
        _Cards ("0 = shell stack, 1 = sprig cards", Float) = 0
        [Enum(Off,0,On,1)] _AlphaToMask ("Alpha to coverage", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _LightPos ("Focused light (xyz, radius; 0 = off)", Vector) = (0, 0, 0, 0)
        _LightColor ("Focused light colour", Color) = (0, 0, 0, 1)
        _Trans ("Translucency", Range(0, 3)) = 0
        _Tip ("Tip glow (world y0, y1, floor, peak)", Vector) = (0, 1, 1, 1)
        _FarStart ("Seated start (tan of half the vertical FOV)", Float) = 0
        _FarEnd ("Seated end (tan of half the vertical FOV)", Float) = 0
        _TopEmit ("Emission kept on upward faces at the seated end", Range(0, 1)) = 1
        _SeatedEmit ("Emission kept at the seated end", Range(0, 1)) = 0.72
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
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
                float4 _MainTex_ST; half4 _Tint, _Emission, _Rim; half _RimPower, _GradBottom, _GradTop, _Cutoff; float4 _GradY;
                float _StrandTile, _FurLen; half _AoBase, _TipBoost, _Cards, _AlphaToMask;
                float4 _LightPos; half4 _LightColor; half _Trans; float4 _Tip;
                half _FarStart, _FarEnd, _TopEmit, _SeatedEmit;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; float4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; float shell : TEXCOORD3; float3 op : TEXCOORD4; half cav : TEXCOORD5; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A i)
            {
                V o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float t = _Cards > 0.5 ? 0 : i.uv2.x;
                float scale = _Cards > 0.5 ? 0 : i.uv2.y;
                o.wn = TransformObjectToWorldNormal(i.n);
                // Strands lean and curl a little, so the shell layers do not stack into a clean set of steps.
                float2 curl = float2(sin(i.uv.x * 311.0 + i.uv.y * 127.0), cos(i.uv.y * 353.0 - i.uv.x * 163.0));
                float3 sway = float3(curl.x + 0.5, 0, curl.y) * (_FurLen * 0.20 * t * t);
                o.wp = TransformObjectToWorld(i.pos.xyz) + (normalize(o.wn) * (_FurLen * t) + sway) * scale;
                o.pos = TransformWorldToHClip(o.wp);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                o.shell = t;
                o.op = i.pos.xyz;
                o.cav = (half)(_Cards > 0.5 ? 1.0 : i.col.r);
                return o;
            }
            float SeatedFar()
            {
                if (_FarEnd <= _FarStart + 1e-8) return 0;
                float tanHalf = 1.0 / max(abs(unity_CameraProjection._m11), 1e-3);
                return saturate((tanHalf - _FarStart) / max(1e-4, _FarEnd - _FarStart));
            }
            half4 frag (V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.wn) * (front ? 1 : -1);
                float2 uvA = i.uv;
                float2 uvS = i.uv;
                if (_Cards < 0.5)
                {
                    // The roll hangs down the wall. Planar UVs from above would smear one texel row down it, so on
                    // steep faces the texture follows the wall instead: arc length around, height down.
                    // Object space here is the Blender frame (Z up). The atan seam sits at the back of the jar.
                    float roll = 1.0 - smoothstep(0.35, 0.70, n.y);
                    float arc = atan2(i.op.x, -i.op.y) * 0.0447;
                    float2 wall = float2(arc, i.op.z) / 0.092;
                    float2 mirrorU = float2(1.0 - abs(1.0 - frac(wall.x * 0.5) * 2.0), wall.y + 0.30);
                    uvA = lerp(i.uv, mirrorU * 0.9 + 0.05, roll);
                    uvS = lerp(i.uv, wall, roll);
                }
                half4 alb = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvA) * _Tint;
                half em = dot(SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, uvA).rgb, half3(0.25, 0.65, 0.10));
                half outA = 1;
                half ao = 1;
                half tipUp = 1;
                if (_Cards > 0.5)
                {
                    // Sharpened cutout for alpha to coverage (bgolus). Without MSAA the hard threshold below still holds.
                    float a = alb.a;
                    float w = max(fwidth(a), 1e-4);
                    float cov = saturate((a - _Cutoff) / w + 0.5);
                    outA = (half)lerp(step(0.5, cov), cov, _AlphaToMask);
                    clip(cov - 0.004);
                }
                else
                {
                    half t = (half)i.shell;
                    half4 sd = SAMPLE_TEXTURE2D(_StrandTex, sampler_StrandTex, uvS * _StrandTile);
                    // The carpet photo's own light and dark patches lift and lower the strands, so the mound has lumps.
                    float height = sd.r * lerp(0.80, 1.14, saturate(em * 1.8));
                    if (t > 0.001)
                    {
                        float w = clamp(fwidth(height), 1e-3, 0.30);
                        float cov = saturate((height - t) / w + 0.5);
                        outA = (half)cov;
                        clip(cov - lerp(0.5, 0.02, _AlphaToMask));
                    }
                    // Per-shell darkening is the baked self-occlusion. Strand tint and clump shade break the sheet up.
                    ao = lerp(_AoBase, 1.0h, (half)pow(t, 0.85));
                    // Hollows between clumps are deep shadow, crests carry the light. Vertex colour from the Blender lumps.
                    ao *= lerp(0.30h, 1.15h, i.cav);
                    alb.rgb *= lerp(0.78h, 1.20h, sd.b) * lerp(0.50h, 1.0h, sd.g);
                    tipUp = lerp(1.0h, _TipBoost, (half)smoothstep(0.62, 1.0, t));
                    if (t <= 0.001) outA = 1;
                }
                alb.rgb *= ao;
                em *= ao * ao * tipUp;

                float far = SeatedFar();
                half up = smoothstep(0.42h, 0.86h, (half)n.y);
                half tipT = saturate((i.wp.y - _Tip.x) / max(1e-4, _Tip.y - _Tip.x));
                em *= lerp(_Tip.z, _Tip.w, tipT);
                if (far > 0.001)
                {
                    em *= lerp(1.0h, (half)_SeatedEmit, (half)far);
                    em *= lerp(1.0h, lerp(1.0h, _TopEmit, up), (half)far);
                }
                float3 v = normalize(GetWorldSpaceViewDir(i.wp));
                half rim = pow(1 - saturate(abs(dot(n, v))), _RimPower) * lerp(0.35h, 1.0h, ao);
                half g = lerp(_GradBottom, _GradTop, saturate((i.wp.y - _GradY.x) / max(1e-4, _GradY.y - _GradY.x)));
                half3 c = alb.rgb * g + _Emission.rgb * em + _Rim.rgb * rim;
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
                    c += _LightColor.rgb * (half)atten * wrap * (alb.rgb * 0.55h + 0.45h) * lerp(0.4h, 1.0h, ao);
                    c += _LightColor.rgb * (half)atten * back * _Trans * lerp(0.35h, 1.0h, blade);
                }
                c = SoftBiolume(c);
                return half4(c, outA);
            }
            ENDHLSL
        }
    }
}
