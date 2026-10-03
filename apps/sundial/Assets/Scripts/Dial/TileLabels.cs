using System.Globalization;
using GardenVR.Core;
using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>
    /// One label per <see cref="TileState"/>. The dev overlay and later accessibility text
    /// read this catalog. They never show the raw enum name. An unknown value falls back
    /// to the neutral label and is logged once.
    /// </summary>
    public static class TileLabels
    {
        public const string Kept = "Kept";
        public const string Late = "Late";
        public const string Missed = "Missed";
        public const string Today = "Today";
        public const string Before = "Before";
        public const string Neutral = "Quiet";

        static bool _loggedUnknown;

        public static string For(TileState state)
        {
            switch (state)
            {
                case TileState.Kept: return Kept;
                case TileState.Late: return Late;
                case TileState.Missed: return Missed;
                case TileState.Today: return Today;
                case TileState.Before: return Before;
                default: return Unknown((int)state);
            }
        }

        static string Unknown(int value)
        {
            if (!_loggedUnknown)
            {
                _loggedUnknown = true;
                Debug.LogWarning("[Sundial] unknown tile state " + value.ToString(CultureInfo.InvariantCulture) + "; using the neutral label");
            }
            return Neutral;
        }
    }
}
