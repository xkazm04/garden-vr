#ifndef GARDENVR_GLASS_COMMON_INCLUDED
#define GARDENVR_GLASS_COMMON_INCLUDED
// Shared night-jar glass.
// _Refract at or below 0.5 keeps the previous Fidelity/Glass look (no opaque-copy sample).
// Above that, the pane stays clear and the silhouette becomes thick glass: a bright outer
// line, a dark edge-on band, a second inner line, and a screen-space bend of the opaque
// copy. The bend is inward and strongest at the edge. No ray trace.
// Beads are an alpha-cut normal map in the upper third. _Fog lifts the clear line.
// _S1_ON is the T-TER-040 spike. It is compiled only when the pass defines that keyword
// (Fidelity/JarGlass). It does not sample the opaque copy. Fidelity/Glass leaves it off.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

TEXTURE2D(_Cond); SAMPLER(sampler_Cond);
TEXTURE2D(_Bead); SAMPLER(sampler_Bead);
TEXTURE2D(_Studio); SAMPLER(sampler_Studio);
TEXTURE2D(_DropN); SAMPLER(sampler_DropN);
CBUFFER_START(UnityPerMaterial)
    half4 _Tint, _Rim, _Inner, _Streak, _Volume;
    half _RimPower, _Fog, _Drops, _Refract;
    float4 _InnerY, _VolumeY;
CBUFFER_END

struct A { float4 pos : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct V { float4 pos : SV_POSITION; float3 wn : TEXCOORD0; float3 wp : TEXCOORD1; float3 op : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

V GlassVert(A i)
{
    V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.wp = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.wp);
    o.wn = TransformObjectToWorldNormal(i.n);
    o.op = o.wp - TransformObjectToWorld(float3(0, 0, 0));
    return o;
}

// Previous Fidelity/Glass. Condensation still comes from _Cond. No scene sample,
// so a project that does not require an opaque texture does not get a black rim.
half4 GlassLegacy(V i, half backWall)
{
    float3 n = normalize(i.wn);
    float3 v = normalize(GetWorldSpaceViewDir(i.wp));
    half ndv = saturate(abs(dot(n, v)));
    half rim = pow(1.0 - ndv, max(_RimPower, 0.5));
    float ang = atan2(i.op.x, -i.op.z);
    float2 fogUv = float2(ang * 1.15, i.op.y * 16.0);
    half3 fogCond = SAMPLE_TEXTURE2D(_Cond, sampler_Cond, fogUv).rgb;
    float band = saturate((i.op.y - 0.092) / 0.026);
    float2 beadUv = float2(ang * 0.22 + 0.37, lerp(0.82, 0.97, band));
    half3 cond = SAMPLE_TEXTURE2D(_Cond, sampler_Cond, beadUv).rgb;
    half upper = smoothstep(0.035, 0.10, i.op.y);
    half beads = smoothstep(0.094, 0.104, i.op.y) * (1.0 - smoothstep(0.116, 0.124, i.op.y));
    half bead = smoothstep(0.72, 0.94, cond.r);
    float keep = frac(sin(dot(floor(beadUv * float2(14.0, 28.0)), float2(127.1, 311.7))) * 43758.5453);
    half drops = bead * beads * _Drops * step(0.66, keep);
    half fogMask = saturate((0.40 + 0.60 * fogCond.b) * (0.10 + 0.90 * upper));
    half fogA = saturate(_Fog) * fogMask * 0.30;
    half column = (1.0 - smoothstep(_InnerY.x, _InnerY.x + max(_InnerY.y, 1e-4), i.op.y)) * smoothstep(0.008, 0.022, i.op.y);
    half wall = smoothstep(0.014, 0.046, abs(i.op.x));
    half cavity = 1.0 - wall;
    float vs = mul(UNITY_MATRIX_V, float4(i.wp, 1)).x - mul(UNITY_MATRIX_V, float4(TransformObjectToWorld(float3(0, 0, 0)) + float3(0, i.op.y, 0), 1)).x;
    half streak = (smoothstep(0.006, 0.0, abs(vs + 0.030)) + 0.6 * smoothstep(0.003, 0.0, abs(vs + 0.022))) * smoothstep(0.02, 0.05, i.op.y) * smoothstep(0.125, 0.10, i.op.y);
    half3 rimRgb = min(_Rim.rgb, half3(0.804, 1.0, 0.903));
    half3 innerRgb = min(_Inner.rgb, half3(0.804, 1.0, 0.903));
    half rimA = rim * _Rim.a;
    half dropA = drops * 0.42;
    half wallA = wall * column * 0.10;
    half baseA = _Tint.a;
    half streakA = streak * _Streak.a * (1.0 - backWall * 0.7);
    half a = saturate(baseA + rimA + dropA + fogA + wallA + streakA);
    half3 light = innerRgb * column * lerp(0.35, 1.0, cavity) * 0.22;
    half3 fogRgb = lerp(innerRgb, half3(0.82, 0.94, 0.90), 0.55);
    half3 c =
        _Tint.rgb * baseA
        + rimRgb * rimA
        + innerRgb * wallA
        + (half3(0.62, 0.92, 0.78) + cond.g * 0.25) * dropA
        + fogRgb * fogA
        + _Streak.rgb * streakA
        + light;
    if (backWall > 0.5) { c *= 0.55; a *= 0.55; }
    return half4(c, a);
}

half4 GlassThick(V i, half backWall)
{
    float3 n = normalize(i.wn);
    float3 v = normalize(GetWorldSpaceViewDir(i.wp));
    half ndv = saturate(abs(dot(n, v)));
    half f = 1.0 - ndv;
    float ang = atan2(i.op.x, -i.op.z);
    float rad = length(i.op.xz);

    // Breath haze is a wisp on the condensation plate. No flat floor.
    float2 fogUv = float2(ang * 1.15, i.op.y * 16.0);
    half3 fogCond = SAMPLE_TEXTURE2D(_Cond, sampler_Cond, fogUv).rgb;
    half upper = smoothstep(0.04, 0.11, i.op.y);
    half fogMask = saturate(fogCond.b) * upper;
    half fogA = saturate(_Fog) * fogMask * 0.10;

    // Upper third of the straight wall (below the shoulder at 0.090), so the caps
    // are whole circles on the pane and not clipped into the cork.
    // presence is fully on by fog 0.30. The clear line rises as _Fog falls.
    half presence = smoothstep(0.02, 0.30, saturate(_Fog));
    float clearY = lerp(0.092, 0.064, presence);
    half beadWindow = smoothstep(clearY, clearY + 0.004, i.op.y) * (1.0 - smoothstep(0.088, 0.094, i.op.y));
    // One texel is about the same world size in u and v, so the caps stay round.
    float2 beadUv = float2(ang * 2.40 / 6.2831853 + 0.5, saturate((i.op.y - 0.064) / 0.026) * 0.22);
    half4 beadTex = SAMPLE_TEXTURE2D(_Bead, sampler_Bead, beadUv);
    half beadCut = step(0.50, beadTex.a);
    half dropMask = beadCut * beadWindow * presence;

    float3 up = float3(0, 1, 0);
    float3 tangent = cross(up, n);
    if (dot(tangent, tangent) < 1e-6) tangent = float3(1, 0, 0);
    tangent = normalize(tangent);
    float3 bitangent = normalize(cross(n, tangent));
    if (dot(bitangent, up) < 0) bitangent = -bitangent;
    float3 tn = float3(beadTex.r * 2.0 - 1.0, beadTex.g * 2.0 - 1.0, beadTex.b * 2.0 - 1.0);
    float3 nPert = normalize(n + tangent * tn.x * 1.6 + bitangent * tn.y * 1.6);
    float3 lightDir = normalize(float3(0.15, 0.82, -0.42));
    float3 h = normalize(v + lightDir);
    half spec = pow(saturate(dot(nPert, h)), 48.0);
    half edgeN = saturate((length(tn.xy) - 0.12) * 2.4);
    half3 cap = half3(0.804, 1.0, 0.903);
    half3 specC = min(half3(0.78, 0.98, 0.94), cap);
    half3 beadDark = half3(0.012, 0.040, 0.046);
    half dropScale = saturate(_Drops);

    // Air light, unchanged. Added, never used as coverage.
    half air = (1.0 - smoothstep(_InnerY.x, _InnerY.x + max(_InnerY.y, 1e-4), i.op.y)) * smoothstep(0.008, 0.022, i.op.y);
    half wall = smoothstep(0.014, 0.046, abs(i.op.x));
    half cavity = 1.0 - wall;
    half3 innerRgb = min(_Inner.rgb, cap);
    half3 light = innerRgb * air * lerp(0.35, 1.0, cavity) * 0.22;

    // Pixel width, not a raw fresnel range. On the base the normal turns slowly, and a
    // fresnel window there became a milky band. fwidth keeps each line a few pixels wide.
    // _RimPower still tightens the outer line.
    // px is pixels inward from the silhouette. Do not saturate it: the middle of the
    // pane is hundreds of pixels in, and clamping that to 1 painted the whole jar.
    float fw = max(fwidth(f), 1e-4);
    half rimTight = saturate((_RimPower - 0.5) / 4.0);
    float edgeF = lerp(0.97, 0.90, rimTight);
    float px = max((edgeF - f) / fw, 0.0);
    half outer = 1.0 - smoothstep(0.4, 2.0, px);
    half dark = smoothstep(1.2, 2.6, px) * (1.0 - smoothstep(10.0, 18.0, px));
    half innerLine = smoothstep(15.0, 17.5, px) * (1.0 - smoothstep(20.0, 24.0, px));
    // _VolumeY is live so the batcher keeps it. It only eases the band through the shoulder.
    dark *= lerp(1.0, 0.90, smoothstep(_VolumeY.x, _VolumeY.x + _VolumeY.y, i.op.y));

    half lip = smoothstep(0.120, 0.123, i.op.y) * (1.0 - smoothstep(0.1252, 0.1264, i.op.y));
    half lipLine = lip * smoothstep(0.45, 0.75, f);

    // Inward screen-space bend. Left-edge view normal x is negative; negating it samples toward center.
    float3 viewN = mul((float3x3)UNITY_MATRIX_V, n);
    float2 inward = -viewN.xy;
    inward /= max(length(inward), 1e-4);
    float pix = pow(saturate(f), 1.15) * _Refract + dropMask * 8.0;
    float2 suv = GetNormalizedScreenSpaceUV(i.pos.xy);
    float2 uv = suv + (inward * pix + tn.xy * dropMask * 11.0) / _ScaledScreenParams.xy;
    half3 scene = SampleSceneColor(uv);
    half3 volumeRgb = max(_Volume.rgb, half3(0.02, 0.04, 0.05));
    half absorb = saturate(_Volume.a);
    // Darken the shifted copy. The framebuffer behind a high band alpha is hidden,
    // so the plants move instead of ghosting against themselves.
    half3 edgeCol = scene * lerp(half3(1.0, 1.0, 1.0), volumeRgb, 0.72 * absorb);
    edgeCol *= lerp(0.78, 0.50, absorb);

    // Bead: a small lens of the shifted copy, a bright cap, and a dark rim so it
    // reads on the green air light.
    half3 beadLens = SampleSceneColor(suv + tn.xy * 14.0 / _ScaledScreenParams.xy);
    // Dark rim plus a small cap highlight. The centre stays a lens so the bead is not a white disc.
    half rimBand = smoothstep(0.06, 0.55, length(tn.xy));
    half3 glint = lerp(beadLens, specC, 0.55) * (0.45 + spec);
    half3 dropRgb = lerp(glint, beadDark, rimBand) * dropMask * dropScale;
    dropRgb += specC * saturate(tn.y) * (1.0 - rimBand) * dropMask * 0.40;
    half dropA = dropMask * lerp(0.70, 0.92, rimBand) * dropScale;

    half3 rimRgb = min(_Rim.rgb, cap);
    half3 hi = min(lerp(rimRgb, half3(0.80, 0.98, 0.94), 0.72), cap);
    half rimScale = saturate(_Rim.a / 0.32);
    half outerA = outer * 0.92 * rimScale;
    half innerA = innerLine * 0.62 * rimScale;
    half lipA = lipLine * 0.70 * rimScale;
    half bandA = dark * lerp(0.55, 0.92, absorb);

    float vs = mul(UNITY_MATRIX_V, float4(i.wp, 1)).x - mul(UNITY_MATRIX_V, float4(TransformObjectToWorld(float3(0, 0, 0)) + float3(0, i.op.y, 0), 1)).x;
    half streak = (smoothstep(0.006, 0.0, abs(vs + 0.030)) + 0.6 * smoothstep(0.003, 0.0, abs(vs + 0.022))) * smoothstep(0.02, 0.05, i.op.y) * smoothstep(0.125, 0.10, i.op.y);
    half streakA = streak * _Streak.a * (1.0 - backWall * 0.7);
    half baseA = _Tint.a;
    half3 fogRgb = lerp(innerRgb, half3(0.82, 0.94, 0.90), 0.55);
    // A little of the inner light in the base rim, so the heel reads as glowing glass.
    half3 heelGlow = innerRgb * dark * smoothstep(0.030, 0.006, i.op.y) * smoothstep(0.030, 0.042, rad) * 0.16;

    half a = saturate(baseA + outerA + innerA + lipA + dropA + fogA + bandA + streakA);
    half3 c =
        edgeCol * bandA
        + hi * (outerA + innerA + lipA)
        + _Tint.rgb * baseA
        + dropRgb
        + fogRgb * fogA
        + _Streak.rgb * streakA
        + heelGlow
        + light;
    if (backWall > 0.5) { c *= 0.55; a *= 0.55; }
    return half4(c, a);
}

// Six faces in a horizontal strip: +X, -X, +Y, -Y, +Z, -Z. One sample. No resolve.
half3 SampleStudio(float3 dir)
{
    float3 a = abs(dir);
    float face;
    float2 uv;
    if (a.x >= a.y && a.x >= a.z)
    {
        if (dir.x >= 0.0) { face = 0.0; uv = float2(-dir.z, dir.y) / max(a.x, 1e-4); }
        else { face = 1.0; uv = float2(dir.z, dir.y) / max(a.x, 1e-4); }
    }
    else if (a.y >= a.z)
    {
        if (dir.y >= 0.0) { face = 2.0; uv = float2(dir.x, -dir.z) / max(a.y, 1e-4); }
        else { face = 3.0; uv = float2(dir.x, dir.z) / max(a.y, 1e-4); }
    }
    else
    {
        if (dir.z >= 0.0) { face = 4.0; uv = float2(dir.x, dir.y) / max(a.z, 1e-4); }
        else { face = 5.0; uv = float2(-dir.x, dir.y) / max(a.z, 1e-4); }
    }
    uv = uv * 0.5 + 0.5;
    uv = clamp(uv, 1.0 / 256.0, 1.0 - 1.0 / 256.0);
    return SAMPLE_TEXTURE2D(_Studio, sampler_Studio, float2((face + uv.x) * (1.0 / 6.0), uv.y)).rgb;
}

#if defined(_S1_ON)
// Structured clear glass. No veil, no air light, no opaque-copy sample.
// Schlick covers the pane. NdotV draws the thickness bands. The foot is a light pipe.
// Droplets are a normal atlas; fog wipes clear under each drop.
half4 GlassStructured(V i, half backWall)
{
    float3 n = normalize(i.wn);
    float3 v = normalize(GetWorldSpaceViewDir(i.wp));
    half ndv = saturate(abs(dot(n, v)));
    float3 dx = ddx(n);
    float3 dy = ddy(n);
    half rough = saturate((dot(dx, dx) + dot(dy, dy)) * 24.0);
    half expo = lerp(5.0, 2.4, rough);
    half F = 0.04 + 0.96 * pow(saturate(1.0 - ndv), expo);
    half3 cap = half3(0.804, 1.0, 0.903);

    half outer = 1.0 - smoothstep(0.0, 0.040, ndv);
    half dark = smoothstep(0.025, 0.070, ndv) * (1.0 - smoothstep(0.15, 0.26, ndv));
    half innerLine = smoothstep(0.18, 0.25, ndv) * (1.0 - smoothstep(0.32, 0.42, ndv));

    float3 r = reflect(-v, n);
    half3 env = min(SampleStudio(r), cap);
    half3 refl = env * F;

    float y = i.op.y;
    float rad = length(i.op.xz);
    // The glow sits on the outer heel, not across the whole base.
    half foot = (1.0 - smoothstep(0.001, 0.011, y)) * smoothstep(0.034, 0.045, rad);
    half footEdge = foot * lerp(0.65, 1.0, saturate(1.0 - ndv));
    half3 footCol = min(_Inner.rgb, cap);

    half lip = smoothstep(0.1185, 0.1212, y) * (1.0 - smoothstep(0.1246, 0.1262, y));
    half lipLine = lip * smoothstep(0.20, 0.65, 1.0 - ndv);

    float ang = atan2(i.op.x, -i.op.z);
    float2 dropUv = float2(ang * 4.5 / 6.2831853 + 0.5, saturate((y - 0.072) / 0.050));
    half4 drop = SAMPLE_TEXTURE2D(_DropN, sampler_DropN, dropUv);
    half presence = smoothstep(0.08, 0.40, saturate(_Fog));
    half window = smoothstep(0.076, 0.090, y) * (1.0 - smoothstep(0.118, 0.124, y));
    half bead = smoothstep(0.50, 0.90, drop.a);
    half dropMask = bead * window * presence * saturate(_Drops);
    half wipe = saturate(drop.b);
    half shoulder = smoothstep(0.074, 0.092, y) * (1.0 - smoothstep(0.116, 0.123, y));
    // The JarG1 glass-mean window is this shoulder. The plate behind it is about 0.03.
    half mistA = shoulder * presence * (1.0 - wipe) * 0.20;

    float3 up = float3(0, 1, 0);
    float3 tangent = cross(up, n);
    if (dot(tangent, tangent) < 1e-6) tangent = float3(1, 0, 0);
    tangent = normalize(tangent);
    float3 bitangent = normalize(cross(n, tangent));
    if (dot(bitangent, up) < 0) bitangent = -bitangent;
    float2 nxy = drop.rg * 2.0 - 1.0;
    float3 nPert = normalize(n + tangent * nxy.x * 1.35 + bitangent * nxy.y * 1.35);
    half rimD = saturate((length(nxy) - 0.12) * 1.7);
    half3 specC = min(half3(0.80, 0.98, 0.94), cap);
    float3 lamp = normalize(float3(-0.42, 0.64, -0.64));
    half phong = pow(saturate(dot(nPert, normalize(v + lamp))), lerp(52.0, 18.0, rough));
    half3 dropEnv = min(SampleStudio(reflect(-v, nPert)), cap);
    half3 dropRgb = lerp(dropEnv, specC, 0.40) * (0.30 + phong);
    dropRgb = lerp(dropRgb, half3(0.012, 0.032, 0.038), rimD) * dropMask;

    half3 lineCol = min(half3(0.78, 0.96, 0.92), cap);
    half3 bandCol = half3(0.035, 0.085, 0.095);
    half3 mistCol = min(half3(0.58, 0.84, 0.72), cap);
    // A few thousandths, so soil through the clear pane is not darker than the reference p5.
    // This is not the old air-light veil.
    half3 body = half3(0.0025, 0.0055, 0.0035);

    half a = saturate(F * 0.80 + outer * 0.50 + innerLine * 0.35 + dark * 0.70 + mistA + lipLine * 0.45 + dropMask * 0.55 + footEdge * 0.25);
    half3 c =
        refl
        + lineCol * (outer * 0.92 + innerLine * 0.50 + lipLine * 0.75)
        + bandCol * dark
        + mistCol * mistA
        + footCol * footEdge * 0.85
        + dropRgb
        + body;
    c = min(c, cap);
    if (backWall > 0.5) { c *= 0.40; a *= 0.40; }
    return half4(c, a);
}
#endif

half4 GlassShade(V i, half backWall)
{
#if defined(_S1_ON)
    return GlassStructured(i, backWall);
#else
    if (_Refract <= 0.5)
        return GlassLegacy(i, backWall);
    return GlassThick(i, backWall);
#endif
}
#endif
