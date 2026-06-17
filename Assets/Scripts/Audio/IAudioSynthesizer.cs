using YoonseulFishing.Data;

namespace YoonseulFishing.Audio
{
    /// <summary>
    /// Seam for the procedural audio layer (ported in Phase 3 →
    /// <c>Audio/ProceduralAudio.cs</c>). GameController calls into this at exactly
    /// the points where the Android ViewModel called into <c>AudioSynthesizer</c>.
    ///
    /// The controller holds this nullably and guards every call with <c>?.</c>,
    /// matching the Kotlin <c>audioSynthesizer?.</c> pattern — so the full game
    /// logic runs head-less (in EditMode tests, or before the audio layer exists).
    /// Method set mirrors the public surface of the original AudioSynthesizer.kt.
    /// </summary>
    public interface IAudioSynthesizer
    {
        /// <summary>Plays a short bell chime at the given frequency (Hz) and 0..1 volume.</summary>
        void TriggerChime(float freq, float volume = 0.15f);

        /// <summary>Plays the water-splash chime (bobber hitting water / fish leaping).</summary>
        void TriggerSplashChime();

        /// <summary>Plays the reel-click tick used for casting and rhythm guidance.</summary>
        void TriggerReelClick();

        /// <summary>Switches the looping nature-sound bed (wind / water lap / crickets).</summary>
        void SetNatureSound(NatureSound sound);

        /// <summary>Adjusts the ambient synthesis for the current time of day.</summary>
        void SetTimeOfDay(TimeOfDay time);

        /// <summary>Adjusts the ambient synthesis for the current weather.</summary>
        void SetWeather(Weather weather);

        /// <summary>Toggles global mute and persists it inside the audio layer.</summary>
        void ToggleMute();

        /// <summary>Current mute state, owned by the audio layer.</summary>
        bool IsMuted();
    }
}
