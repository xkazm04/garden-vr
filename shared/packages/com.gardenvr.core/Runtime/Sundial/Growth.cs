namespace GardenVR.Core
{
    /// <summary>Closed tile vocabulary. Copy comes from a catalog, never from the raw name.</summary>
    public enum TileState
    {
        Kept,
        Late,
        Missed,
        Today,
        Before
    }

    /// <summary>
    /// Plant size. Declaration order is growth order. It follows lifetime kept days and only rises:
    /// 0 seed, 1-2 sprout, 3-6 young, 7-13 leafy, 14 or more full (PLAN D2).
    /// </summary>
    public enum Stage
    {
        Seed,
        Sprout,
        Young,
        Leafy,
        Full
    }

    /// <summary>
    /// The week's flower. It follows kept days inside the 7-tile window and may close:
    /// under 3 none, 3-4 bud, 5 or more open (PLAN D2).
    /// </summary>
    public enum Bloom
    {
        None,
        Bud,
        Open
    }

    public static partial class SundialRules
    {
        public static Stage StageFor(int lifetimeKept)
        {
            if (lifetimeKept <= 0) return Stage.Seed;
            if (lifetimeKept <= 2) return Stage.Sprout;
            if (lifetimeKept <= 6) return Stage.Young;
            if (lifetimeKept <= 13) return Stage.Leafy;
            return Stage.Full;
        }

        public static Bloom BloomFor(int windowKept)
        {
            if (windowKept < 3) return Bloom.None;
            if (windowKept <= 4) return Bloom.Bud;
            return Bloom.Open;
        }
    }
}
