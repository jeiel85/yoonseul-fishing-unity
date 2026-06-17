using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using YoonseulFishing.Data;

namespace YoonseulFishing.Audio
{
    /// <summary>
    /// Real-time procedural soundscape — the Unity port of <c>AudioSynthesizer.kt</c>.
    /// Where the Android build filled an <c>AudioTrack</c> stream from a coroutine,
    /// this fills Unity's audio DSP buffer via <see cref="OnAudioFilterRead"/>.
    ///
    /// Synthesises (all code, zero audio assets): soft wind (filtered noise), gentle
    /// water lapping, night crickets, rain drops, a time-of-day ambient pad, and the
    /// pentatonic glass-bell chimes that game events trigger.
    ///
    /// THREADING — <see cref="OnAudioFilterRead"/> runs on the audio thread, so it
    /// must not touch main-thread-only Unity APIs (UnityEngine.Random, Time, most of
    /// the engine). It uses <see cref="System.Random"/> and <see cref="System.Math"/>
    /// only. The shared chime list is guarded by <see cref="_chimeLock"/>; the
    /// ambient/mix parameters are <c>volatile</c> scalars written from the main thread.
    ///
    /// Delayed follow-up chimes (splash/reel) are scheduled by a future start sample
    /// rather than coroutine delays — sample-accurate and audio-thread-friendly.
    ///
    /// Self-test: add this component to a GameObject (an AudioSource is required and
    /// auto-added) in a scene that has an AudioListener (the default Main Camera) and
    /// press Play — the ambient bed should fade in and chimes ring periodically.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralAudio : MonoBehaviour, IAudioSynthesizer
    {
        private const float MasterVolume = 0.35f;
        private const double ChimeDurationSeconds = 2.5;

        // Pentatonic note palettes per time of day (Hz), from the original.
        private static readonly float[] DayNotes =
            { 392.00f, 440.00f, 493.88f, 587.33f, 659.25f, 783.99f, 880.00f, 987.77f, 1174.66f };
        private static readonly float[] SunsetNotes =
            { 293.66f, 329.63f, 392.00f, 440.00f, 493.88f, 587.33f, 659.25f, 783.99f };
        private static readonly float[] NightNotes =
            { 164.81f, 196.00f, 220.00f, 246.94f, 329.63f, 392.00f, 440.00f, 493.88f };

        // --- Shared state (main thread writes, audio thread reads) ---
        private volatile bool _isMuted;
        private volatile NatureSound _activeNature = NatureSound.WaterLap;
        private volatile TimeOfDay _activeTime = TimeOfDay.Day;
        private volatile Weather _activeWeather = Weather.Clear;

        private volatile float _targetWindVol = 0.15f;
        private volatile float _targetWaterVol = 1.0f;
        private volatile float _targetCricketsVol = 0.0f;

        // --- Audio-thread-only state ---
        private float _currentWindVol = 0.15f;
        private float _currentWaterVol = 1.0f;
        private float _currentCricketsVol = 0.0f;
        private float _lastWindFilter;
        private float _lastWaterFilter;
        private long _sampleCounter;
        private int _sampleRate;
        private readonly System.Random _audioRng = new System.Random();

        // --- Main-thread-only state ---
        private readonly System.Random _mainRng = new System.Random();
        private float _nextRandomChimeTime;

        private sealed class ActiveChime
        {
            public float Freq;
            public long StartSample;     // may be in the future (delayed chime)
            public long DurationSamples;
            public float Volume;
        }

        private readonly List<ActiveChime> _chimes = new List<ActiveChime>();
        private readonly object _chimeLock = new object();

        // ------------------------------------------------------------------
        //  Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            _sampleRate = AudioSettings.outputSampleRate;

            var source = GetComponent<AudioSource>();
            // A looping silent clip keeps the DSP chain (OnAudioFilterRead) firing.
            int len = Mathf.Max(_sampleRate, 1);
            source.clip = AudioClip.Create("ProceduralAudioSilence", len, 1, _sampleRate, false);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D
            source.Play();

            UpdateTargets();
        }

        private void Update()
        {
            // Schedule a gentle random chime every ~1.8–4.0 s (main thread).
            if (_isMuted) return;
            if (Time.time >= _nextRandomChimeTime)
            {
                TriggerRandomChime();
                _nextRandomChimeTime = Time.time + 1.8f + (float)_mainRng.NextDouble() * 2.2f;
            }
        }

        // ------------------------------------------------------------------
        //  IAudioSynthesizer
        // ------------------------------------------------------------------

        public void TriggerChime(float freq, float volume = 0.15f) => AddChime(freq, volume, 0f);

        public void TriggerSplashChime()
        {
            TriggerChime(987.77f, 0.25f);          // B5 now
            AddChime(1174.66f, 0.20f, 0.120f);     // D6 after 120 ms
            AddChime(1480.00f, 0.12f, 0.200f);     // F#6 after 200 ms
        }

        public void TriggerReelClick()
        {
            TriggerChime(110f, 0.3f);              // low click now
            AddChime(220f, 0.15f, 0.040f);         // after 40 ms
        }

        public void SetNatureSound(NatureSound sound) { _activeNature = sound; UpdateTargets(); }
        public void SetTimeOfDay(TimeOfDay time) { _activeTime = time; UpdateTargets(); }
        public void SetWeather(Weather weather) { _activeWeather = weather; UpdateTargets(); }

        public void ToggleMute() => _isMuted = !_isMuted;
        public bool IsMuted() => _isMuted;

        // ------------------------------------------------------------------
        //  Chime scheduling (callable from any thread)
        // ------------------------------------------------------------------

        /// <summary>Queues a chime starting <paramref name="delaySeconds"/> from now.</summary>
        private void AddChime(float freq, float volume, float delaySeconds)
        {
            if (_sampleRate <= 0) return;
            long now = Interlocked.Read(ref _sampleCounter);
            var chime = new ActiveChime
            {
                Freq = freq,
                Volume = volume,
                StartSample = now + (long)(delaySeconds * _sampleRate),
                DurationSamples = (long)(_sampleRate * ChimeDurationSeconds),
            };
            lock (_chimeLock) { _chimes.Add(chime); }
        }

        private void TriggerRandomChime()
        {
            float[] palette = _activeTime switch
            {
                TimeOfDay.Day => DayNotes,
                TimeOfDay.Sunset => SunsetNotes,
                _ => NightNotes,
            };
            float freq = palette[_mainRng.Next(palette.Length)];
            float vol = 0.04f + (float)_mainRng.NextDouble() * 0.08f;
            TriggerChime(freq, vol);
        }

        // ------------------------------------------------------------------
        //  Ambient mix targets
        // ------------------------------------------------------------------

        private void UpdateTargets()
        {
            float windBase = 0.1f;
            float waterBase = 0.15f;
            float cricketsBase = 0.0f;

            switch (_activeNature)
            {
                case NatureSound.Wind:
                    windBase = 1.0f; waterBase = 0.25f; cricketsBase = 0.05f;
                    break;
                case NatureSound.WaterLap:
                    windBase = 0.15f; waterBase = 1.0f; cricketsBase = 0.05f;
                    break;
                case NatureSound.Crickets:
                    windBase = 0.1f; waterBase = 0.2f; cricketsBase = 1.0f;
                    break;
            }

            if (_activeTime == TimeOfDay.Night)
                cricketsBase += 0.35f; // cosy starry-night ambience

            if (_activeWeather == Weather.Mist)
            {
                windBase += 0.2f;
                cricketsBase *= 0.3f;
            }
            else if (_activeWeather == Weather.Rain)
            {
                windBase += 0.4f;
                cricketsBase *= 0.05f;
                waterBase += 0.2f;
            }

            _targetWindVol = Mathf.Clamp01(windBase);
            _targetWaterVol = Mathf.Clamp01(waterBase);
            _targetCricketsVol = Mathf.Clamp01(cricketsBase);
        }

        // ------------------------------------------------------------------
        //  Synthesis (AUDIO THREAD — keep main-thread Unity APIs out of here)
        // ------------------------------------------------------------------

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (_sampleRate <= 0 || channels <= 0) { Array.Clear(data, 0, data.Length); return; }

            if (_isMuted)
            {
                // Silence without advancing the clock (matches the original).
                Array.Clear(data, 0, data.Length);
                return;
            }

            int frames = data.Length / channels;

            // Snapshot the chime list once per buffer (avoids holding the lock per sample).
            ActiveChime[] chimes;
            lock (_chimeLock) { chimes = _chimes.ToArray(); }

            for (int f = 0; f < frames; f++)
            {
                double t = _sampleCounter / (double)_sampleRate;

                // Slowly glide channel volumes toward their targets.
                _currentWindVol += (_targetWindVol - _currentWindVol) * 0.00003f;
                _currentWaterVol += (_targetWaterVol - _currentWaterVol) * 0.00003f;
                _currentCricketsVol += (_targetCricketsVol - _currentCricketsVol) * 0.00003f;

                float rawWhite = (float)(_audioRng.NextDouble() - 0.5) * 2f;

                // 1. Soft wind — resonant low-passed noise, gusting.
                float windGust = 0.5f + 0.5f * (float)Math.Sin(2.0 * Math.PI * t / 7.2) * (float)Math.Cos(2.0 * Math.PI * t / 3.4);
                float windFilterAlpha = 0.015f + 0.02f * windGust;
                _lastWindFilter = (1f - windFilterAlpha) * _lastWindFilter + windFilterAlpha * rawWhite;
                float windOut = _lastWindFilter * (0.045f * windGust * _currentWindVol);

                // 2. Gentle water lapping — periodic swell + splash.
                float waterSwell = 0.4f + 0.6f * (float)Math.Cos(2.0 * Math.PI * t / 5.5);
                double lapPhase = t % 4.0;
                float lapSwell = lapPhase < 1.0 ? (float)Math.Sin(Math.PI * lapPhase / 1.0) : 0f;
                float waterFilterAlpha = 0.035f + 0.04f * (waterSwell + lapSwell * 0.5f);
                _lastWaterFilter = (1f - waterFilterAlpha) * _lastWaterFilter + waterFilterAlpha * rawWhite;
                float waterOut = _lastWaterFilter * (0.022f * waterSwell + 0.028f * lapSwell) * _currentWaterVol;

                // 3. Night crickets — high textured chirps.
                double cricketCycle = t % 1.6;
                float cricketOut = 0f;
                if (cricketCycle < 0.65)
                {
                    double subCycle = cricketCycle % 0.20;
                    if (subCycle < 0.09)
                    {
                        float carrier = (float)Math.Sin(2.0 * Math.PI * 4000.0 * t);
                        float buzz = (float)Math.Sin(2.0 * Math.PI * 65.0 * t);
                        float envelope = (float)Math.Sin(Math.PI * (subCycle / 0.09));
                        cricketOut = carrier * (buzz * 0.35f + 0.65f) * envelope * 0.012f * _currentCricketsVol;
                    }
                }

                // 4. Occasional rain drops (schedule a tiny chime).
                if (_activeWeather == Weather.Rain && _audioRng.NextDouble() < 0.0006)
                {
                    float dropFreq = 1500f + (float)_audioRng.NextDouble() * 1000f;
                    float dropVol = 0.004f + (float)_audioRng.NextDouble() * 0.01f;
                    AddChime(dropFreq, dropVol, 0f); // appears in the next buffer's snapshot
                }

                // 5. Time-of-day ambient pad.
                float mixed = windOut + waterOut + cricketOut + AmbientPad(t);

                // 6. Active pentatonic chimes (exp-decay glass bells + 2nd harmonic).
                for (int c = 0; c < chimes.Length; c++)
                {
                    var ch = chimes[c];
                    long elapsed = _sampleCounter - ch.StartSample;
                    if (elapsed < 0 || elapsed >= ch.DurationSamples) continue; // not started / finished
                    double progress = elapsed / (double)_sampleRate;
                    float decay = (float)Math.Exp(-progress * 2.2);
                    float sine = (float)Math.Sin(2.0 * Math.PI * ch.Freq * progress);
                    float secondHarmonic = (float)Math.Sin(4.0 * Math.PI * ch.Freq * progress) * 0.15f;
                    mixed += (sine + secondHarmonic) * ch.Volume * decay;
                }

                // Soft clamp + master volume.
                if (mixed > 1f) mixed = 1f;
                else if (mixed < -1f) mixed = -1f;
                float sample = mixed * MasterVolume;

                int baseIdx = f * channels;
                for (int ch2 = 0; ch2 < channels; ch2++)
                    data[baseIdx + ch2] = sample;

                _sampleCounter++;
            }

            // Drop finished chimes (those whose window has fully elapsed).
            lock (_chimeLock)
            {
                for (int i = _chimes.Count - 1; i >= 0; i--)
                {
                    long elapsed = _sampleCounter - _chimes[i].StartSample;
                    if (elapsed >= _chimes[i].DurationSamples) _chimes.RemoveAt(i);
                }
            }
        }

        private float AmbientPad(double t)
        {
            switch (_activeTime)
            {
                case TimeOfDay.Day:
                {
                    float o1 = (float)Math.Sin(2.0 * Math.PI * 196.00 * t); // G3
                    float o2 = (float)Math.Sin(2.0 * Math.PI * 246.94 * t); // B3
                    float swell = 0.5f + 0.5f * (float)Math.Sin(2.0 * Math.PI * t / 11.0);
                    return (o1 * 0.5f + o2 * 0.5f) * swell * 0.0035f;
                }
                case TimeOfDay.Sunset:
                {
                    float o1 = (float)Math.Sin(2.0 * Math.PI * 220.00 * t); // A3
                    float o2 = (float)Math.Sin(2.0 * Math.PI * 261.63 * t); // C4
                    float swell = 0.5f + 0.5f * (float)Math.Sin(2.0 * Math.PI * t / 13.0);
                    return (o1 * 0.5f + o2 * 0.5f) * swell * 0.0035f;
                }
                default: // Night
                {
                    float o1 = (float)Math.Sin(2.0 * Math.PI * 164.81 * t); // E3
                    float o2 = (float)Math.Sin(2.0 * Math.PI * 220.00 * t); // A3
                    float swell = 0.5f + 0.5f * (float)Math.Sin(2.0 * Math.PI * t / 15.0);
                    return (o1 * 0.5f + o2 * 0.5f) * swell * 0.0045f;
                }
            }
        }
    }
}
