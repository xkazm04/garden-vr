// Moss at two lenses. The JarG1 plate (28.2 deg) takes the Fidelity/Glow equation unchanged.
// The seated rig (60 deg) is a wider lens, so the same sample picks a blurrier mip,
// a low-frequency colour, and less emission on upward faces.
// _FarEnd <= _FarStart leaves every other assignment identical to Glow.
Shader "Fidelity/MossVelvet"
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
        _LightPos ("Focused light (xyz, radius; 0 = off)", Vector) = (0, 0, 0, 0)
        _LightColor ("Focused light colour", Color) = (0, 0, 0, 1)
        _Trans ("Translucency", Range(0, 3)) = 0
        _Soft ("Soft edge dither", Range(0, 0.6)) = 0
        _Tip ("Tip glow (world y0, y1, floor, peak)", Vector) = (0, 1, 1, 1)
        _Edge ("Shape-edge glow", Range(0, 2)) = 0
        _FarStart ("Seated start (tan of half the vertical FOV)", Float) = 0
        _FarEnd ("Seated end (tan of half the vertical FOV)", Float) = 0
        _FarMip ("Extra mip bias at the seated end", Float) = 0
        _Velvet ("Low-frequency colour at the seated end", Range(0, 1)) = 0
        _TopEmit ("Emission kept on upward faces at the seated end", Range(0, 1)) = 1
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
            // Same cap as Fidelity/Glow. The JarG1 plate uses this equation, so the moss has to share it.
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
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; half4 _Tint, _Emission, _Rim; half _RimPower, _GradBottom, _GradTop, _Cutoff, _TopTile, _TopAmount, _Shell, _Tri; float4 _GradY;
                float4 _LightPos; half4 _LightColor; half _Trans, _Soft; float4 _Tip; half _Edge;
                half _FarStart, _FarEnd, _FarMip, _Velvet, _TopEmit;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V vert (A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.wn = TransformObjectToWorldNormal(i.n);
                o.wp = TransformObjectToWorld(i.pos.xyz) + normalize(o.wn) * _Shell; o.pos = TransformWorldToHClip(o.wp);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }
            float MossHash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float MossNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = MossHash(i);
                float b = MossHash(i + float2(1, 0));
                float c = MossHash(i + float2(0, 1));
                float d = MossHash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            // The chair is a 60 deg lens (tan of half ~ 0.58). JarG1 is 28.2 deg (tan of half ~ 0.25).
            // A per-pixel footprint is smaller at the chair, because that eye sits closer, so it cannot
            // separate the two frames. The lens does, and it is the same value on every pixel of a shot.
            float SeatedFar()
            {
                if (_FarEnd <= _FarStart + 1e-8) return 0;
                float tanHalf = 1.0 / max(abs(unity_CameraProjection._m11), 1e-3);
                return saturate((tanHalf - _FarStart) / max(1e-4, _FarEnd - _FarStart));
            }
            void SampleBiased(float3 n, float3 wp, float2 uv, float bias, out half4 alb, out half em)
            {
                if (_Tri > 0)
                {
                    float3 w = pow(abs(n), 4); w /= (w.x + w.y + w.z);
                    float3 p = wp * _Tri;
                    alb = SAMPLE_TEXTURE2D_BIAS(_MainTex, sampler_MainTex, p.zy, bias) * w.x
                        + SAMPLE_TEXTURE2D_BIAS(_MainTex, sampler_MainTex, p.xz, bias) * w.y
                        + SAMPLE_TEXTURE2D_BIAS(_MainTex, sampler_MainTex, p.xy, bias) * w.z;
                    alb *= _Tint; em = dot(alb.rgb, half3(0.3, 0.6, 0.1));
                }
                else
                {
                    alb = SAMPLE_TEXTURE2D_BIAS(_MainTex, sampler_MainTex, uv, bias) * _Tint;
                    // Green tips emit. The red channel of a moss photo is the dark one.
                    em = dot(SAMPLE_TEXTURE2D_BIAS(_EmissionTex, sampler_EmissionTex, uv, bias).rgb, half3(0.25, 0.65, 0.10));
                }
                half topw = _TopAmount * smoothstep(0.45, 0.75, abs(n.y));
                half3 top = SAMPLE_TEXTURE2D_BIAS(_TopTex, sampler_TopTex, wp.xz * _TopTile, bias).rgb * _Tint.rgb;
                alb.rgb = lerp(alb.rgb, top, topw); em = lerp(em, dot(top, half3(0.3, 0.6, 0.1)), topw);
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
                    // Green tips emit. The red channel of a moss photo is the dark one.
                    em = dot(SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, i.uv).rgb, half3(0.25, 0.65, 0.10));
                }
                if (_Shell > 0) clip(abs(n.y) - 0.45);   // fuzz only on the cap, never on the vertical skirt
                // caps (moss top, cork top) take a planar texture so the lathe's pole never shows its radial pinch
                half topw = _TopAmount * smoothstep(0.45, 0.75, abs(n.y));
                half3 top = SAMPLE_TEXTURE2D(_TopTex, sampler_TopTex, i.wp.xz * _TopTile).rgb * _Tint.rgb;
                alb.rgb = lerp(alb.rgb, top, topw); em = lerp(em, dot(top, half3(0.3, 0.6, 0.1)), topw);

                float far = SeatedFar();
                // Zero on the JarG1 plate. One on the seated rig, so the locked moss rect does not move.
                float amt = far;
                if (amt > 0.001)
                {
                    half4 albFar; half emFar;
                    SampleBiased(n, i.wp, i.uv, _FarMip * amt, albFar, emFar);
                    // Alpha stays at mip 0. A biased cutout would eat the tuft silhouette.
                    alb.rgb = lerp(alb.rgb, albFar.rgb, (half)amt);
                    em = lerp(em, emFar, (half)amt);
                    float n1 = MossNoise(i.wp.xz * 8.0);
                    float n2 = MossNoise(i.wp.xz * 15.0 + 4.2);
                    half3 velvet = alb.rgb * (half)lerp(0.64, 0.96, n1);
                    velvet.r *= (half)lerp(0.78, 0.96, n1);
                    velvet.b *= (half)lerp(1.08, 0.88, n2);
                    velvet *= (half)lerp(0.88, 1.02, n2);
                    half blend = (half)(amt * _Velvet);
                    alb.rgb = lerp(alb.rgb, velvet, blend);
                    em *= lerp(1.0h, (half)lerp(0.70, 0.96, n1), blend);
                    em *= lerp(1.0h, 0.72h, (half)amt);
                }

                // Height window. Equal floor and peak multiply by 1, which is every material that does not opt in.
                half tipT = saturate((i.wp.y - _Tip.x) / max(1e-4, _Tip.y - _Tip.x));
                em *= lerp(_Tip.z, _Tip.w, tipT);
                if (amt > 0.001)
                {
                    half up = smoothstep(0.42h, 0.86h, (half)n.y);
                    em *= lerp(1.0h, lerp(1.0h, _TopEmit, up), (half)amt);
                }
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
