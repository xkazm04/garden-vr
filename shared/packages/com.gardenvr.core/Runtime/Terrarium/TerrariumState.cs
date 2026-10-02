using System;
using System.Globalization;

namespace GardenVR.Core
{
    /// <summary>
    /// Pure snapshot of one terrarium moment. The Operator tool returns <see cref="ToJson"/>;
    /// tests treat that string as the state oracle. No engine types.
    /// </summary>
    public sealed class TerrariumState
    {
        public BreathPhase Phase { get; }
        public int Breaths { get; }
        public float Uncoil { get; }
        public float Fog { get; }
        public int Fronds { get; }
        public int Flowers { get; }
        public int DewToday { get; }
        public float Vitality { get; }
        public int Rituals { get; }

        public TerrariumState(BreathPhase phase, int breaths, float uncoil, float fog,
            int fronds, int flowers, int dewToday, float vitality, int rituals)
        {
            Phase = phase;
            Breaths = breaths;
            Uncoil = uncoil;
            Fog = fog;
            Fronds = fronds;
            Flowers = flowers;
            DewToday = dewToday;
            Vitality = vitality;
            Rituals = rituals;
        }

        /// <summary>Read the live breath session and garden. Vitality is evaluated on <paramref name="today"/>.</summary>
        public static TerrariumState Capture(BreathSession breath, Garden garden, int today)
        {
            if (breath == null) throw new ArgumentNullException(nameof(breath));
            if (garden == null) throw new ArgumentNullException(nameof(garden));
            return new TerrariumState(
                breath.Phase, breath.Breaths, breath.Uncoil, breath.Fog,
                garden.Fronds, garden.Flowers, garden.DewToday,
                garden.Vitality(today), garden.RitualsCompleted);
        }

        /// <summary>Hand-written JSON, invariant culture. Field order is the oracle contract.</summary>
        public string ToJson()
        {
            var culture = CultureInfo.InvariantCulture;
            return "{\"phase\":\"" + Phase.ToString()
                + "\",\"breaths\":" + Breaths.ToString(culture)
                + ",\"uncoil\":" + FormatFloat(Uncoil)
                + ",\"fog\":" + FormatFloat(Fog)
                + ",\"fronds\":" + Fronds.ToString(culture)
                + ",\"flowers\":" + Flowers.ToString(culture)
                + ",\"dewToday\":" + DewToday.ToString(culture)
                + ",\"vitality\":" + FormatFloat(Vitality)
                + ",\"rituals\":" + Rituals.ToString(culture)
                + "}";
        }

        /// <summary>Six decimal places, then trimmed, so 0.6 and 1 stay stable across cultures.</summary>
        static string FormatFloat(float value)
        {
            var rounded = Math.Round((double)value, 6, MidpointRounding.AwayFromZero);
            return rounded.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
