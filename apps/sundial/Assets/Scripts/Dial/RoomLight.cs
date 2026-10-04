using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Where the room's key light comes from (Spike S2, F1). The PC look-dev reads the room plate
    /// (<c>room_cookie.png</c>). Quest reads the room itself. This is the seam, nothing more:
    /// <see cref="MrukRoomLightStub"/> is a stub and is not built in S2.
    /// </summary>
    public interface IRoomLightSource
    {
        /// <summary>
        /// False until an estimate exists. <paramref name="towardLight"/> is a world-space unit vector from the
        /// dial toward the key (the window). <paramref name="brightness"/> is the room's mean luminance, 0 to 1,
        /// smoothed over seconds.
        /// </summary>
        bool TryGetKey(out Vector3 towardLight, out float brightness);
    }

    /// <summary>
    /// Quest variant, stub only. Plan: the key direction is the MRUK scene model's window anchor
    /// (<c>MRUKAnchor.SceneLabels.WINDOW_FRAME</c>) when the room has one. Brightness and white point come from the
    /// Passthrough Camera Access frame mean, smoothed with a 2 to 4 second time constant. Both feed
    /// <see cref="RoomLightGlobals.Apply"/> through a painted window-light cookie turned to that direction, held to
    /// +-10 to 15 percent. Until then every device build returns false and the room-light look stays off.
    /// </summary>
    public sealed class MrukRoomLightStub : IRoomLightSource
    {
        public bool TryGetKey(out Vector3 towardLight, out float brightness)
        {
            towardLight = Vector3.zero;
            brightness = 0f;
            return false;
        }
    }

    /// <summary>
    /// The shader seam for the room-light multiply. <c>_GVR_ROOMLIGHT</c> is a global keyword, off by default.
    /// While it is off, <c>Fidelity/Toon</c> and <c>Fidelity/Card</c> compile to the look-A code.
    /// </summary>
    public static class RoomLightGlobals
    {
        public const string Keyword = "_GVR_ROOMLIGHT";
        /// <summary>The cookie covers this many metres across, in dial space. Matches roomlight_s2.py.</summary>
        public const float CookieSize = 0.40f;
        /// <summary>The cap from the dossier: +-15 percent luma.</summary>
        public const float Amplitude = 0.15f;
        /// <summary>
        /// The cookie is a lit-fraction map. Cream paper cannot brighten, so the gain is
        /// 1 + Amplitude * clamp(d * Contrast + Bias): the lit side holds near 1 and the shade side falls toward 0.85.
        /// </summary>
        public const float Contrast = 1.3f;
        public const float Bias = -0.5f;

        static readonly int CookieId = Shader.PropertyToID("_GvrRoomCookie");
        static readonly int WorldToCookieId = Shader.PropertyToID("_GvrRoomW2C");
        static readonly int ParamsId = Shader.PropertyToID("_GvrRoomParams");

        public static bool IsOn => Shader.IsKeywordEnabled(Keyword);

        /// <summary>The cookie is authored in the dial's local space, so <paramref name="worldToDial"/> is the dial root's worldToLocalMatrix.</summary>
        public static void Apply(Texture2D cookie, Matrix4x4 worldToDial, float amplitude)
        {
            Shader.SetGlobalTexture(CookieId, cookie);
            Shader.SetGlobalMatrix(WorldToCookieId, worldToDial);
            Shader.SetGlobalVector(ParamsId, new Vector4(CookieSize, Mathf.Clamp(amplitude, 0f, Amplitude), Contrast, Bias));
            Shader.EnableKeyword(Keyword);
        }

        public static void Clear()
        {
            Shader.DisableKeyword(Keyword);
        }
    }
}
