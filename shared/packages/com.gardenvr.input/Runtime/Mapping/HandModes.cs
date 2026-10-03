namespace GardenVR.Input
{
    /// <summary>Hold keeps a breath while Space or the left mouse is down. Toggle latches that breath on a Space press.</summary>
    public enum HoldMode
    {
        Hold,
        Toggle
    }

    /// <summary>Development-build commands. Raised only when the mapper is told that this is an editor or development build.</summary>
    public enum DevCommand
    {
        /// <summary>F1, state overlay.</summary>
        StateOverlay,
        /// <summary>F2, auto-pace.</summary>
        AutoPace,
        /// <summary>[ previous day. The core refuses a rewrite of history.</summary>
        PreviousDay,
        /// <summary>] next day.</summary>
        NextDay,
        /// <summary>T, run the clock at sixty times. A second press returns to real time.</summary>
        ClockFast,
        /// <summary>F3, offer the six life-habit seed packets.</summary>
        SeedPackets
    }
}
