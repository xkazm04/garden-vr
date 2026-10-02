using System;

namespace GardenVR.Core
{
    public enum ArcId
    {
        Morning,
        Midday,
        WindDown
    }

    /// <summary>
    /// One painted arc. <see cref="StartMin"/> is inclusive and <see cref="EndMin"/> is exclusive,
    /// in minutes after local midnight. Wind-down's end sits past midnight, at 03:00 the next calendar day.
    /// </summary>
    public sealed class ArcDef
    {
        public ArcId Id;
        public int StartMin;
        public int EndMin;
    }
}
