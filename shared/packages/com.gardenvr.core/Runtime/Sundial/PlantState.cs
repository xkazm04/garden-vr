namespace GardenVR.Core
{
    /// <summary>
    /// One plant, derived from the ledger. Never stored.
    /// <see cref="Window"/> has 7 tiles, oldest first and today last.
    /// </summary>
    public sealed class PlantState
    {
        public string HabitId;
        public TileState[] Window;
        public int WindowKept;
        public int LifetimeKept;
        public Stage Stage;
        public Bloom Bloom;

        /// <summary>True only while the clock is inside this habit's arc and today is still open.</summary>
        public bool DueNow;

        /// <summary>
        /// Yesterday is on or after the day the habit was created, and it has no live tend.
        /// The clock boundary is enforced by <see cref="SundialRules.Backfill"/>, which is the call that writes.
        /// </summary>
        public bool CanBackfillYesterday;
    }
}
