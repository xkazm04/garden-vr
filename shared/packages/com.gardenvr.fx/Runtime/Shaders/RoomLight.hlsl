#ifndef GARDENVR_ROOM_LIGHT_INCLUDED
#define GARDENVR_ROOM_LIGHT_INCLUDED
// The room-light cookie (Spike S2), written once. Include after Core.hlsl.
// The globals are set by RoomLightGlobals (apps/sundial). They sit outside UnityPerMaterial on purpose, so the
// SRP batcher is unchanged. Each shader keeps its own sample point and calls the gain with it.
// _GvrRoomParams: x = cookie size (m), y = amplitude, z = contrast, w = bias.
TEXTURE2D(_GvrRoomCookie); SAMPLER(sampler_GvrRoomCookie);
float4x4 _GvrRoomW2C;
float4 _GvrRoomParams;

float2 RoomLightUV(float3 wp)
{
    float3 lp = mul(_GvrRoomW2C, float4(wp, 1.0)).xyz;
    return lp.xz / max(_GvrRoomParams.x, 1e-4) + 0.5;
}

// d is the cookie's red mapped to -1..1. The cap is on the encoded (display) luma, so the linear multiply is that gain to the 2.2.
float RoomLightShape(float d)
{
    return pow(1.0 + _GvrRoomParams.y * clamp(d * _GvrRoomParams.z + _GvrRoomParams.w, -1.0, 1.0), 2.2);
}

// Fragment stage (implicit mip).
float RoomLightGain(float3 wp)
{
    return RoomLightShape(SAMPLE_TEXTURE2D(_GvrRoomCookie, sampler_GvrRoomCookie, RoomLightUV(wp)).r * 2.0 - 1.0);
}

// Vertex stage: no derivatives, so mip 0.
float RoomLightGainLod(float3 wp)
{
    return RoomLightShape(SAMPLE_TEXTURE2D_LOD(_GvrRoomCookie, sampler_GvrRoomCookie, RoomLightUV(wp), 0).r * 2.0 - 1.0);
}
#endif
