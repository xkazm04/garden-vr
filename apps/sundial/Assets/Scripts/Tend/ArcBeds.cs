using GardenVR.Audio;
using GardenVR.Core;

namespace GardenVR.Sundial
{
    /// <summary>
    /// The three arc beds. They stay silent until the save says Beds.
    /// When the shadow enters another arc, the playing bed fades out over 4 seconds
    /// and the next bed starts at once. The wind-down arc uses <see cref="Dusk"/>.
    /// </summary>
    public sealed class ArcBeds
    {
        public const float CrossfadeSeconds = 4f;
        public const string Morning = "bed.morning";
        public const string Midday = "bed.midday";
        public const string Dusk = "bed.dusk";

        public string Current { get; private set; }

        public static string For(ArcId? arc)
        {
            if (!arc.HasValue) return null;
            switch (arc.Value)
            {
                case ArcId.Morning: return Morning;
                case ArcId.Midday: return Midday;
                case ArcId.WindDown: return Dusk;
                default: return null;
            }
        }

        /// <summary>
        /// The cue that should start, or null when nothing new should start.
        /// A change fades the music bus first, so the caller can play the next bed over the tail.
        /// </summary>
        public string Sync(AudioCueService audio, bool enabled, ArcId? arc)
        {
            string next = enabled ? For(arc) : null;
            if (next == Current)
            {
                if (next != null && (audio == null || !audio.IsPlaying(next)))
                    return next;
                return null;
            }

            bool fadingOut = Current != null || (audio != null && next == null && audio.IsBusActive("music"));
            if (fadingOut && audio != null)
                audio.FadeBus("music", CrossfadeSeconds);
            Current = next;
            return next;
        }
    }
}
