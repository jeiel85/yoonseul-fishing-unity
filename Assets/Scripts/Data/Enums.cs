namespace YoonseulFishing.Data
{
    /// <summary>Game state enums, ported from FishingViewModel.kt.</summary>

    public enum TimeOfDay { Day, Sunset, Night }

    public enum Weather { Clear, Mist, Rain }

    public enum NatureSound { Wind, WaterLap, Crickets }

    public enum FishingState
    {
        Idle,       // Sitting on the boat, holding the rod
        Casting,    // Casting the rod, bobber flying
        Waiting,    // Bobber in water, waiting for fish
        Nibble,     // Fish is tasting, bobber dips slightly, ripples appear
        Bite,       // Fish is hooked, bobber goes fully under! PULL NOW!
        Reeling,    // Hooked, rhythm mini-game
        Splashing,  // Massive splash! The fish leaps out!
        Caught,     // Detail card dialog is open
        Lost        // Catch failed, fish swam away
    }

    /// <summary>
    /// Order is significant: persisted as the integer index (ordinal), matching
    /// the Android build's <c>AppLanguage.ordinal</c> persistence. Do not reorder.
    /// </summary>
    public enum AppLanguage { Ko = 0, En = 1, Ja = 2 }
}
