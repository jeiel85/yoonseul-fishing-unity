namespace YoonseulFishing.Core
{
    /// <summary>
    /// Persistence seam. The Android build scattered writes across
    /// <c>SharedPreferences</c> (progress, settings) and a Room/SQLite table
    /// (caught fish), persisting on every mutation. This port unifies both into a
    /// single JSON snapshot of <see cref="GameState"/> (implemented in Phase 2 →
    /// <c>Data/SaveService.cs</c>, written to <c>Application.persistentDataPath</c>).
    ///
    /// <see cref="GameController"/> calls <see cref="Save"/> at the same moments the
    /// original called <c>prefs.edit().apply()</c> / <c>repository.insert()</c>.
    /// Several consecutive Kotlin pref writes collapse into one whole-state
    /// <see cref="Save"/> here, which is equivalent because the snapshot is atomic.
    /// </summary>
    public interface ISaveService
    {
        /// <summary>
        /// Persist the full current game state now. Called frequently during play;
        /// the Phase 2 implementation may debounce or write asynchronously, but must
        /// guarantee no progress is silently lost (the Android build's contract).
        /// </summary>
        void Save();
    }
}
