using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using UnityEngine;
using YoonseulFishing.Audio;
using YoonseulFishing.Data;

namespace YoonseulFishing.Core
{
    /// <summary>
    /// The game's brain — a 1:1 logic port of <c>FishingViewModel.kt</c>. Pure C#
    /// (no MonoBehaviour); it reads/writes <see cref="GameState"/>, talks to the
    /// audio layer through <see cref="IAudioSynthesizer"/>, and persists through
    /// <see cref="ISaveService"/>.
    ///
    /// Concurrency model: the Kotlin original ran on coroutines
    /// (<c>viewModelScope.launch{ delay() }</c> with <c>Job.cancel()</c>). This port
    /// uses Unity <c>Awaitable</c> + <see cref="CancellationTokenSource"/> — the
    /// fishing flow (cast → wait → nibble → bite → reel) lives under
    /// <c>_fishingCts</c> (a new one per cast/reel, cancelling the previous, exactly
    /// like reassigning <c>fishingJob</c>); the auto environment cycles and
    /// fire-and-forget chime sequences live under <c>_lifetimeCts</c> (the
    /// <c>viewModelScope</c> equivalent, cancelled on <see cref="Dispose"/>).
    /// Everything runs on Unity's main thread, so no locking is needed.
    ///
    /// Boot wiring (done by the Phase 6 bootstrap MonoBehaviour):
    ///   1. <c>var state = new GameState();</c>
    ///   2. <c>var save = new SaveService(state); save.Load();</c> // Phase 2 — JSON → state
    ///   3. <c>var ctrl = new GameController(state, save, audio);</c>
    ///   4. <c>ctrl.ResetDailyQuestsIfNewDay();</c>    // day-rollover reset
    ///   5. <c>ctrl.StartEnvironmentCycles();</c>      // begin auto time/weather/sound
    /// </summary>
    public class GameController : IDisposable
    {
        private readonly GameState _state;
        private readonly ISaveService _save;
        private IAudioSynthesizer _audio;

        // Cancellation scopes (see class summary).
        private CancellationTokenSource _fishingCts;
        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();

        public GameController(GameState state, ISaveService save, IAudioSynthesizer audio = null)
        {
            _state = state;
            _save = save;
            _audio = audio;
        }

        /// <summary>Wires the audio layer in once it exists (mirrors <c>setSynthesizer</c>).</summary>
        public void SetSynthesizer(IAudioSynthesizer synth)
        {
            _audio = synth;
            _state.IsMuted.Value = synth.IsMuted();
            synth.SetNatureSound(_state.NatureSound.Value);
            synth.SetTimeOfDay(_state.TimeOfDay.Value);
            synth.SetWeather(_state.Weather.Value);
        }

        // ---------------------------------------------------------------------
        //  Bait
        // ---------------------------------------------------------------------

        public void SelectBait(BaitType bait)
        {
            _state.ActiveBait.Value = bait;
            _save.Save();
        }

        public bool BuyBait(BaitType bait, int quantity = 1)
        {
            if (bait == BaitType.Basic) return false;
            int cost = bait.Price * quantity;
            if (_state.Coins.Value < cost) return false;

            _state.Coins.Value -= cost;
            if (bait == BaitType.Worm) _state.BaitWormCount.Value += quantity;
            else if (bait == BaitType.Shrimp) _state.BaitShrimpCount.Value += quantity;
            else if (bait == BaitType.Golden) _state.BaitGoldenCount.Value += quantity;

            // Auto-select if we were on basic bait
            if (_state.ActiveBait.Value == BaitType.Basic) SelectBait(bait);

            _save.Save();
            _ = PlayChimeSequenceAsync(_lifetimeCts.Token, (0, 587.33f, 0.15f), (80, 880.00f, 0.25f));
            return true;
        }

        // ---------------------------------------------------------------------
        //  Progression
        // ---------------------------------------------------------------------

        /// <summary>XP required to clear a given level. Static + pure for testability.</summary>
        public static int XpNeededForLevel(int level) => level * 100;

        public int GetXpNeeded(int level) => XpNeededForLevel(level);

        public int GetUpgradeCost() => _state.RodLevel.Value * 150;

        public bool UpgradeRod()
        {
            int cost = GetUpgradeCost();
            if (_state.Coins.Value < cost) return false;

            _state.Coins.Value -= cost;
            _state.RodLevel.Value += 1;
            _save.Save();

            // Rising pentatonic celebration: C5 E5 G5 B5 C6
            _ = PlayChimeSequenceAsync(_lifetimeCts.Token,
                (0, 523.25f, 0.15f), (100, 659.25f, 0.15f), (100, 783.99f, 0.15f),
                (100, 987.77f, 0.15f), (100, 1046.50f, 0.35f));
            return true;
        }

        public void DismissLevelUpDialog() => _state.ShowLevelUpDialog.Value = null;

        private void AddExperienceForFish(FishSpecies fish)
        {
            int xpGain = fish.Rarity switch
            {
                "일반" => 20,
                "희귀" => 40,
                "전설" => 150,
                "신화" => 500,
                _ => 15,
            };
            AddXp(xpGain);

            int baseCoins = fish.Rarity switch
            {
                "일반" => 15,
                "희귀" => 40,
                "전설" => 120,
                "신화" => 450,
                _ => 10,
            };
            float rodMultiplier = 1.0f + 0.15f * (_state.RodLevel.Value - 1);
            int coinsEarned = (int)(baseCoins * rodMultiplier);

            _state.Coins.Value += coinsEarned;
            _save.Save();
        }

        /// <summary>
        /// Pure XP→level resolution (extracted for testability): adds <paramref name="amount"/>,
        /// then repeatedly subtracts the per-level requirement (<see cref="XpNeededForLevel"/>),
        /// rolling the level up. Returns the resulting level, carried-over XP, and whether any
        /// level-up occurred. No state, no side effects.
        /// </summary>
        public static (int level, int xp, bool leveledUp) ApplyXp(int level, int xp, int amount)
        {
            int newXp = xp + amount;
            int newLevel = level;
            bool leveledUp = false;
            while (newXp >= XpNeededForLevel(newLevel))
            {
                newXp -= XpNeededForLevel(newLevel);
                newLevel++;
                leveledUp = true;
            }
            return (newLevel, newXp, leveledUp);
        }

        private void AddXp(int amount)
        {
            var (level, xp, leveledUp) = ApplyXp(_state.FishingLevel.Value, _state.FishingXp.Value, amount);
            _state.FishingXp.Value = xp;
            _state.FishingLevel.Value = level;
            _save.Save();

            if (leveledUp)
            {
                _ = PlayChimeSequenceAsync(_lifetimeCts.Token,
                    (0, 523.25f, 0.15f), (120, 659.25f, 0.15f), (120, 783.99f, 0.15f), (120, 1046.50f, 0.30f));
                _state.ShowLevelUpDialog.Value = level;
            }
        }

        // ---------------------------------------------------------------------
        //  Environment cycles (auto + manual)
        // ---------------------------------------------------------------------

        /// <summary>Starts the three background cycles. Call once after load (boot).</summary>
        public void StartEnvironmentCycles()
        {
            _ = AutoTimeCycleAsync(_lifetimeCts.Token);
            _ = AutoWeatherCycleAsync(_lifetimeCts.Token);
            _ = AutoNatureSoundCycleAsync(_lifetimeCts.Token);
        }

        private async Awaitable AutoTimeCycleAsync(CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    await DelayMs(90000, ct); // 90 s per time phase
                    ProgressTimeOfDay();
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Awaitable AutoWeatherCycleAsync(CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    await DelayMs(50000, ct); // evaluate every 50 s
                    float roll = UnityEngine.Random.value;
                    Weather next = roll < 0.45f ? Weather.Clear
                                 : roll < 0.75f ? Weather.Mist
                                 : Weather.Rain;
                    if (_state.Weather.Value != next)
                    {
                        _state.Weather.Value = next;
                        _audio?.SetWeather(next);
                        PlayWeatherChangeSound(next);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Awaitable AutoNatureSoundCycleAsync(CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    await DelayMs(75000, ct); // cycle every 75 s
                    ProgressNatureSound();
                }
            }
            catch (OperationCanceledException) { }
        }

        public void ProgressNatureSound()
        {
            NatureSound next = _state.NatureSound.Value switch
            {
                NatureSound.WaterLap => NatureSound.Wind,
                NatureSound.Wind => NatureSound.Crickets,
                NatureSound.Crickets => NatureSound.WaterLap,
                _ => NatureSound.WaterLap,
            };
            SetNatureSound(next);
        }

        public void SetNatureSound(NatureSound sound)
        {
            _state.NatureSound.Value = sound;
            _audio?.SetNatureSound(sound);

            // Cozy transition chime
            if (sound == NatureSound.WaterLap) _audio?.TriggerChime(493.88f, 0.08f);      // B4
            else if (sound == NatureSound.Wind) _audio?.TriggerChime(587.33f, 0.08f);     // D5
            else if (sound == NatureSound.Crickets) _audio?.TriggerChime(659.25f, 0.08f); // E5
        }

        public void ProgressWeather()
        {
            Weather next = _state.Weather.Value switch
            {
                Weather.Clear => Weather.Mist,
                Weather.Mist => Weather.Rain,
                Weather.Rain => Weather.Clear,
                _ => Weather.Clear,
            };
            _state.Weather.Value = next;
            _audio?.SetWeather(next);
            PlayWeatherChangeSound(next);
        }

        private void PlayWeatherChangeSound(Weather w)
        {
            if (w == Weather.Clear) _audio?.TriggerChime(659.25f, 0.08f);     // E5
            else if (w == Weather.Mist) _audio?.TriggerChime(523.25f, 0.08f); // C5
            else if (w == Weather.Rain) _audio?.TriggerChime(349.23f, 0.08f); // F4
        }

        public void ProgressTimeOfDay()
        {
            TimeOfDay next = _state.TimeOfDay.Value switch
            {
                TimeOfDay.Day => TimeOfDay.Sunset,
                TimeOfDay.Sunset => TimeOfDay.Night,
                TimeOfDay.Night => TimeOfDay.Day,
                _ => TimeOfDay.Day,
            };
            _state.TimeOfDay.Value = next;
            _audio?.SetTimeOfDay(next);
            PlayTimeShiftSound(next);
        }

        public void SetTimeOfDay(TimeOfDay time)
        {
            _state.TimeOfDay.Value = time;
            _audio?.SetTimeOfDay(time);
            PlayTimeShiftSound(time);
        }

        private void PlayTimeShiftSound(TimeOfDay time)
        {
            switch (time)
            {
                case TimeOfDay.Day:
                    _audio?.TriggerChime(587.33f, 0.15f); // D5
                    _audio?.TriggerChime(783.99f, 0.10f); // G5
                    break;
                case TimeOfDay.Sunset:
                    _audio?.TriggerChime(440.00f, 0.15f); // A4
                    _audio?.TriggerChime(659.25f, 0.10f); // E5
                    break;
                case TimeOfDay.Night:
                    _audio?.TriggerChime(329.63f, 0.15f); // E4
                    _audio?.TriggerChime(493.88f, 0.12f); // B4
                    break;
            }
        }

        // ---------------------------------------------------------------------
        //  Settings / locale
        // ---------------------------------------------------------------------

        public void ToggleMute()
        {
            if (_audio == null) return;
            _audio.ToggleMute();
            _state.IsMuted.Value = _audio.IsMuted();
        }

        public void ToggleLanguage()
        {
            AppLanguage next = _state.Language.Value switch
            {
                AppLanguage.Ko => AppLanguage.En,
                AppLanguage.En => AppLanguage.Ja,
                AppLanguage.Ja => AppLanguage.Ko,
                _ => AppLanguage.Ko,
            };
            _state.Language.Value = next;
            _state.IsEnglish.Value = (next == AppLanguage.En);
            _save.Save();
        }

        public void ToggleGpgSignIn()
        {
            _state.IsGpgSignedIn.Value = !_state.IsGpgSignedIn.Value;
            PlayRewardChime();
        }

        public void TriggerSoundChime(float freq, float volume = 0.15f) => _audio?.TriggerChime(freq, volume);

        // ---------------------------------------------------------------------
        //  Core interaction — single tap drives the whole state machine
        // ---------------------------------------------------------------------

        public void HandleScreenTap(float tapX = 0.5f, float tapY = 0.65f)
        {
            switch (_state.FishingState.Value)
            {
                case FishingState.Idle:
                    CastLine(tapX, tapY);
                    break;
                case FishingState.Casting:
                    break; // ignore taps mid-cast
                case FishingState.Waiting:
                    CancelFishing(lost: false); // too early — line retrieved empty
                    break;
                case FishingState.Nibble:
                    CancelFishing(lost: true);  // jerked on a nibble — fish escapes
                    break;
                case FishingState.Bite:
                    ReelIn();                   // perfect hook!
                    break;
                case FishingState.Reeling:
                    HandleRhythmTap();
                    break;
                case FishingState.Splashing:
                    ShowCaughtDialog();         // skip straight to stats
                    break;
                case FishingState.Caught:
                case FishingState.Lost:
                    _state.FishingState.Value = FishingState.Idle;
                    break;
            }
        }

        private void CastLine(float targetX, float targetY)
        {
            if (!_state.HasCastBefore.Value)
            {
                _state.HasCastBefore.Value = true;
                _save.Save();
            }

            _state.FishingState.Value = FishingState.Casting;
            _state.BobberPositionX.Value = Mathf.Clamp(targetX, 0.15f, 0.85f);
            _state.BobberPositionY.Value = Mathf.Clamp(targetY, 0.55f, 0.85f);

            _audio?.TriggerReelClick();

            // Deduct one unit of premium bait, or fall back to basic if exhausted
            BaitType active = _state.ActiveBait.Value;
            if (active != BaitType.Basic)
            {
                int count = _state.GetBaitCount(active);
                if (count > 0)
                {
                    if (active == BaitType.Worm) _state.BaitWormCount.Value--;
                    else if (active == BaitType.Shrimp) _state.BaitShrimpCount.Value--;
                    else if (active == BaitType.Golden) _state.BaitGoldenCount.Value--;
                    _save.Save();
                }
                else
                {
                    SelectBait(BaitType.Basic);
                }
            }

            CancellationToken ct = StartFishingJob();
            _ = CastFlowAsync(ct);
        }

        private async Awaitable CastFlowAsync(CancellationToken ct)
        {
            try
            {
                await DelayMs(1200, ct); // cast animation

                _audio?.TriggerSplashChime();
                _state.FishingState.Value = FishingState.Waiting;

                // Wait 3–7 s for the first nibble; rod level shortens it, worm bait speeds it up
                long waitReduction = (_state.RodLevel.Value - 1) * 350L;
                long rawTime = RandomLong(3000, 7000) - waitReduction;
                long patientWaitTime = _state.ActiveBait.Value == BaitType.Worm
                    ? Math.Max((long)(rawTime * 0.65f), 1000L)
                    : Math.Max(rawTime, 1500L);
                await DelayMs(patientWaitTime, ct);

                if (_state.FishingState.Value != FishingState.Waiting) return;

                // 1–3 teasing nibbles
                int nibbleCount = UnityEngine.Random.Range(1, 4);
                for (int i = 0; i < nibbleCount; i++)
                {
                    if (_state.FishingState.Value != FishingState.Waiting) return;

                    _state.FishingState.Value = FishingState.Nibble;
                    _audio?.TriggerChime(392.00f, 0.05f);
                    await DelayMs(RandomLong(400, 900), ct);

                    if (_state.FishingState.Value != FishingState.Nibble) return;
                    _state.FishingState.Value = FishingState.Waiting;
                    await DelayMs(RandomLong(1000, 2500), ct);
                }

                // The real bite
                if (_state.FishingState.Value != FishingState.Waiting) return;
                _state.FishingState.Value = FishingState.Bite;
                _audio?.TriggerSplashChime();

                long biteWindow = RandomLong(1500, 2200);
                await DelayMs(biteWindow, ct);

                // Missed the window — fish swims away
                if (_state.FishingState.Value == FishingState.Bite)
                {
                    _state.FishingState.Value = FishingState.Lost;
                    _audio?.TriggerChime(220f, 0.15f);
                    await DelayMs(3000, ct);
                    if (_state.FishingState.Value == FishingState.Lost)
                        _state.FishingState.Value = FishingState.Idle;
                }
            }
            catch (OperationCanceledException) { /* superseded by a new cast/reel */ }
        }

        // ---------------------------------------------------------------------
        //  Rhythm reeling mini-game
        // ---------------------------------------------------------------------

        private void ReelIn()
        {
            CancellationToken ct = StartFishingJob();

            _state.FishingState.Value = FishingState.Reeling;
            _state.RhythmHitCount.Value = 0;
            _state.RhythmMissCount.Value = 0;
            _state.RhythmFeedbackText.Value = null;
            _state.RhythmBeatActive.Value = false;
            _state.RhythmRingScale.Value = 1.0f;

            _audio?.TriggerReelClick();

            _ = ReelFlowAsync(ct);
        }

        private async Awaitable ReelFlowAsync(CancellationToken ct)
        {
            try
            {
                // 3 hits land the fish; 3 misses lose it
                while (_state.RhythmHitCount.Value < 3 && _state.RhythmMissCount.Value < 3)
                {
                    await DelayMs(400, ct);
                    if (_state.FishingState.Value != FishingState.Reeling) break;
                    await RunRhythmBeatAsync(ct);
                }

                if (_state.FishingState.Value != FishingState.Reeling) return;

                if (_state.RhythmHitCount.Value >= 3)
                {
                    FishSpecies fish = RollFishSpecies(_state.TimeOfDay.Value, _state.Weather.Value);
                    float length = UnityEngine.Random.Range(fish.BaseLengthMin, fish.BaseLengthMax);
                    float weight = UnityEngine.Random.Range(fish.BaseWeightMin, fish.BaseWeightMax);
                    string mappedTime = MapTimeOfDayId(_state.TimeOfDay.Value);

                    var entity = new CaughtFish(fish.Id, weight, length, mappedTime, NowUnixMs());
                    _state.LastCaughtFish.Value = entity;

                    _audio?.TriggerSplashChime();
                    _state.FishingState.Value = FishingState.Splashing;

                    await DelayMs(2200, ct);
                    ShowCaughtDialog();
                }
                else
                {
                    _state.FishingState.Value = FishingState.Lost;
                    _audio?.TriggerChime(220f, 0.15f);
                    await DelayMs(3000, ct);
                    if (_state.FishingState.Value == FishingState.Lost)
                        _state.FishingState.Value = FishingState.Idle;
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Awaitable RunRhythmBeatAsync(CancellationToken ct)
        {
            _state.RhythmRingScale.Value = 1.0f;
            _state.RhythmBeatActive.Value = true;
            _state.RhythmFeedbackText.Value = null;

            const long durationMs = 1400L;
            const int steps = 35;
            long stepDelay = durationMs / steps; // 40 ms

            bool hasFeedbackOccurred = false;

            for (int i = 0; i <= steps; i++)
            {
                if (_state.FishingState.Value != FishingState.Reeling) break;
                if (_state.RhythmFeedbackText.Value != null) hasFeedbackOccurred = true;

                _state.RhythmRingScale.Value = 1.0f - ((float)i / steps);

                if (i == 10 || i == 20) _audio?.TriggerReelClick();

                await DelayMs(stepDelay, ct);
            }

            _state.RhythmBeatActive.Value = false;

            // No tap at all this beat = a miss
            if (!hasFeedbackOccurred && _state.FishingState.Value == FishingState.Reeling)
            {
                _state.RhythmFeedbackText.Value = "MISS";
                _state.RhythmMissCount.Value++;
                _audio?.TriggerChime(220f, 0.12f);
                await DelayMs(400, ct);
            }
            else
            {
                await DelayMs(300, ct);
            }
        }

        private void HandleRhythmTap()
        {
            if (!_state.RhythmBeatActive.Value || _state.RhythmFeedbackText.Value != null) return;

            float scale = _state.RhythmRingScale.Value;
            // Upgraded rod widens the sweet spot
            float tolerance = Mathf.Min((_state.RodLevel.Value - 1) * 0.012f, 0.08f);
            float perfectMin = 0.23f - tolerance;
            float perfectMax = 0.43f + tolerance;
            float goodMin = 0.11f - tolerance * 1.5f;
            float goodMax = 0.52f + tolerance * 1.5f;

            if (scale >= perfectMin && scale <= perfectMax)
            {
                _state.RhythmFeedbackText.Value = "PERFECT!";
                _state.RhythmHitCount.Value++;
                _audio?.TriggerChime(880.00f, 0.1f); // high A
                _ = PlayDelayedChimeAsync(80, 1046.50f, 0.1f, _lifetimeCts.Token); // high C
            }
            else if (scale >= goodMin && scale <= goodMax)
            {
                _state.RhythmFeedbackText.Value = "GOOD!";
                _state.RhythmHitCount.Value++;
                _audio?.TriggerChime(659.25f, 0.1f); // E5
            }
            else
            {
                _state.RhythmFeedbackText.Value = "MISS";
                _state.RhythmMissCount.Value++;
                _audio?.TriggerChime(220.00f, 0.12f); // low A
            }
        }

        // ---------------------------------------------------------------------
        //  Catch resolution
        // ---------------------------------------------------------------------

        private void ShowCaughtDialog()
        {
            CancelFishingJob();

            CaughtFish entity = _state.LastCaughtFish.Value;
            if (entity == null) return;

            _state.FishingState.Value = FishingState.Caught;

            // Persist + award (the Android version did this in a coroutine because the
            // DB insert was suspend; here the insert is synchronous in-memory + Save()).
            InsertCaughtFish(entity);

            FishSpecies fish = FishSpecies.Find(entity.SpeciesId);
            if (fish != null)
            {
                AddExperienceForFish(fish);
                CheckAndUpdateCatchStats(fish, entity.Length, entity.Weight);
            }
        }

        /// <summary>Adds a catch to the collection (newest first, by caught time), assigns its id, persists.</summary>
        private void InsertCaughtFish(CaughtFish entity)
        {
            var list = new List<CaughtFish>(_state.CaughtFishList.Value);

            int nextId = 1;
            foreach (var f in list)
                if (f.Id >= nextId) nextId = f.Id + 1;
            entity.Id = nextId;

            list.Add(entity);
            list.Sort((a, b) => b.CaughtTime.CompareTo(a.CaughtTime)); // ORDER BY caughtTime DESC
            _state.CaughtFishList.Value = list;
            _save.Save();
        }

        private void CheckAndUpdateCatchStats(FishSpecies fish, float length, float weight)
        {
            _state.TotalFishCaught.Value += 1;
            _save.Save();

            if (_state.DailyQuestCatchCount.Value < 3)
            {
                _state.DailyQuestCatchCount.Value += 1;
                _save.Save();
            }

            bool isRareOrHigher = fish.Rarity == "희귀" || fish.Rarity == "전설" || fish.Rarity == "신화";
            if (isRareOrHigher && _state.DailyQuestRareCount.Value < 1)
            {
                _state.DailyQuestRareCount.Value += 1;
                _save.Save();
            }

            if (fish.Rarity == "전설" || fish.Rarity == "신화")
            {
                _state.HasCaughtLegendaryOrMythic.Value = true;
                _save.Save();
            }

            AppLanguage lang = _state.Language.Value;
            string nameToDisplay = lang == AppLanguage.En ? fish.NameEn : fish.Name;
            SendLocalNotification(nameToDisplay, length, weight);

            string lenStr = length.ToString("F1", CultureInfo.InvariantCulture);
            string title = lang == AppLanguage.Ko ? "🎣 대어 획득 편지!"
                         : lang == AppLanguage.Ja ? "🎣 釣り上げました！"
                         : "🎣 Big Catch!";
            // NOTE: the Japanese branch intentionally uses the Korean name, matching the original.
            string message = lang == AppLanguage.Ko ? $"{fish.Name} ({lenStr}cm) 획득 성공!"
                            : lang == AppLanguage.Ja ? $"{fish.Name} ({lenStr}cm) 獲得成功！"
                            : $"{fish.NameEn} ({lenStr}cm) caught successfully!";

            var alert = new NotificationAlert(title, message, "🐠", fish.Color);
            _state.InAppAlert.Value = alert;
            _ = AutoDismissAlertAsync(alert, _lifetimeCts.Token);
        }

        private async Awaitable AutoDismissAlertAsync(NotificationAlert alert, CancellationToken ct)
        {
            try
            {
                await DelayMs(4000, ct);
                if (ReferenceEquals(_state.InAppAlert.Value, alert))
                    _state.InAppAlert.Value = null;
            }
            catch (OperationCanceledException) { }
        }

        public void DismissInAppAlert() => _state.InAppAlert.Value = null;

        /// <summary>
        /// OS-level local notification. Android-specific in the original
        /// (NotificationManager). Deferred to the platform-integration phase
        /// (Phase 7, via Unity's Mobile Notifications package); the in-app alert
        /// above already gives in-game feedback, so this is a no-op for now.
        /// </summary>
        private void SendLocalNotification(string fishName, float length, float weight)
        {
            // TODO (Phase 7): Unity Mobile Notifications on Android.
        }

        private void CancelFishing(bool lost)
        {
            CancelFishingJob();
            if (lost)
            {
                _state.FishingState.Value = FishingState.Lost;
                _audio?.TriggerChime(220f, 0.15f);
                _ = LostResetAsync(2500, _lifetimeCts.Token);
            }
            else
            {
                _state.FishingState.Value = FishingState.Idle;
            }
        }

        private async Awaitable LostResetAsync(long ms, CancellationToken ct)
        {
            try
            {
                await DelayMs(ms, ct);
                if (_state.FishingState.Value == FishingState.Lost)
                    _state.FishingState.Value = FishingState.Idle;
            }
            catch (OperationCanceledException) { }
        }

        // ---------------------------------------------------------------------
        //  Species roll (probability)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Weighted species draw based on time of day, weather, rarity (boosted by
        /// active bait), and the current fishing spot. The per-species scoring is
        /// factored into <see cref="ComputeSpeciesWeight"/> so it can be verified
        /// deterministically in tests, independent of the RNG draw.
        /// </summary>
        private FishSpecies RollFishSpecies(TimeOfDay time, Weather w)
        {
            IReadOnlyList<FishSpecies> candidates = FishSpecies.List;
            string timeString = time switch
            {
                TimeOfDay.Day => "낮",
                TimeOfDay.Sunset => "노을",
                TimeOfDay.Night => "밤",
                _ => "낮",
            };
            BaitType bait = _state.ActiveBait.Value;
            string spotId = _state.CurrentSpot.Value.Id;

            double total = 0.0;
            var scores = new double[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                scores[i] = ComputeSpeciesWeight(candidates[i], timeString, w, bait, spotId);
                total += scores[i];
            }

            double randomPos = (double)UnityEngine.Random.value * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                randomPos -= scores[i];
                if (randomPos <= 0.0) return candidates[i];
            }
            return candidates[0];
        }

        /// <summary>
        /// Relative selection weight for one species under the given conditions.
        /// Pure function of its inputs (no RNG, no state) — directly unit-testable.
        /// <paramref name="timeString"/> is the Korean optimal-time token
        /// ("낮"/"노을"/"밤"); <paramref name="spotId"/> is a FishingSpot id.
        /// </summary>
        public static double ComputeSpeciesWeight(FishSpecies fish, string timeString, Weather w, BaitType bait, string spotId)
        {
            double score = 1.0;

            // Time of day
            if (fish.OptimalTime == timeString) score *= 3.0;
            else if (fish.OptimalTime == "언제나") score *= 1.5;
            else score *= 0.3;

            // Weather
            bool isOptimalWeather = w switch
            {
                Weather.Clear => IsIn(fish.Id, "gold_crucian", "shiri", "rainbow_trout", "koi"),
                Weather.Mist => IsIn(fish.Id, "mandarin_fish", "gobs", "cherry_salmon", "sunset_butterfly"),
                Weather.Rain => IsIn(fish.Id, "catfish", "sweetfish", "moonlight_catfish", "star_whale"),
                _ => false,
            };
            score *= isOptimalWeather ? 4.0 : 0.4;

            // Rarity, boosted by specialized bait
            double rarityWeight;
            switch (fish.Rarity)
            {
                case "일반":
                    rarityWeight = 1.0;
                    break;
                case "희귀":
                    rarityWeight = 0.4 * (bait == BaitType.Shrimp ? 2.2 : 1.0);
                    break;
                case "전설":
                {
                    double m = bait == BaitType.Shrimp ? 1.8 : bait == BaitType.Golden ? 3.6 : 1.0;
                    rarityWeight = 0.1 * m;
                    break;
                }
                case "신화":
                {
                    double m = bait == BaitType.Shrimp ? 1.5 : bait == BaitType.Golden ? 5.5 : 1.0;
                    rarityWeight = 0.02 * m;
                    break;
                }
                default:
                    rarityWeight = 1.0;
                    break;
            }
            score *= rarityWeight;

            // Current spot bonus
            double spotMultiplier = 1.0;
            switch (spotId)
            {
                case "windy_valley":
                    if (IsIn(fish.Id, "gold_crucian", "rainbow_trout", "sweetfish", "shiri")) spotMultiplier = 2.5;
                    break;
                case "galaxy_lake":
                    if (IsIn(fish.Id, "mandarin_fish", "koi", "gobs", "moonlight_catfish")) spotMultiplier = 2.5;
                    break;
                case "wave_beach":
                    if (IsIn(fish.Id, "sunset_butterfly", "star_whale", "catfish", "cherry_salmon")) spotMultiplier = 2.5;
                    break;
            }
            score *= spotMultiplier;

            return score;
        }

        // ---------------------------------------------------------------------
        //  Collection: selling / releasing
        // ---------------------------------------------------------------------

        public void ReleaseAllCaughtFish()
        {
            _state.CaughtFishList.Value = new List<CaughtFish>();
            _state.LastCaughtFish.Value = null;
            _state.FishingLevel.Value = 1;
            _state.FishingXp.Value = 0;
            _state.HasCastBefore.Value = false;
            _state.Coins.Value = 0;
            _state.RodLevel.Value = 1;
            _save.Save();
        }

        public int GetFishValue(CaughtFish entity)
        {
            FishSpecies fish = FishSpecies.Find(entity.SpeciesId);
            if (fish == null) return 10;

            int basePrice = fish.Rarity switch
            {
                "일반" => 20,
                "희귀" => 50,
                "전설" => 200,
                "신화" => 600,
                _ => 10,
            };
            float multiplier = fish.BaseLengthMin > 0 ? entity.Length / fish.BaseLengthMin : 1.0f;
            return Math.Max((int)(basePrice * multiplier), basePrice);
        }

        public void SellFish(CaughtFish entity)
        {
            int value = GetFishValue(entity);
            _state.Coins.Value += value;
            TrackSaleStats(value, 1);

            var list = new List<CaughtFish>(_state.CaughtFishList.Value);
            list.RemoveAll(f => f.Id == entity.Id);
            _state.CaughtFishList.Value = list;
            _save.Save();

            _ = PlayChimeSequenceAsync(_lifetimeCts.Token, (0, 783.99f, 0.12f), (100, 1046.50f, 0.15f));
        }

        public void SellAllFish(IReadOnlyList<CaughtFish> list)
        {
            if (list == null || list.Count == 0) return;

            int totalGain = 0;
            foreach (var e in list) totalGain += GetFishValue(e);
            _state.Coins.Value += totalGain;
            TrackSaleStats(totalGain, list.Count);

            _state.CaughtFishList.Value = new List<CaughtFish>();
            _save.Save();

            _ = PlayChimeSequenceAsync(_lifetimeCts.Token,
                (0, 523.25f, 0.1f), (80, 659.25f, 0.1f), (80, 783.99f, 0.1f), (80, 1046.50f, 0.25f));
        }

        private void TrackSaleStats(int value, int fishCount)
        {
            _state.TotalFishSold.Value += fishCount;
            _save.Save();

            if (_state.DailyQuestGoldEarned.Value < 120)
            {
                _state.DailyQuestGoldEarned.Value = Math.Min(_state.DailyQuestGoldEarned.Value + value, 120);
                _save.Save();
            }
        }

        // ---------------------------------------------------------------------
        //  Fishing spots
        // ---------------------------------------------------------------------

        public void SelectSpot(FishingSpot spot)
        {
            _state.CurrentSpot.Value = spot;
            _save.Save();

            _ = PlayChimeSequenceAsync(_lifetimeCts.Token,
                (0, 293.66f, 0.15f), (150, 392.00f, 0.20f), (150, 587.33f, 0.12f));
        }

        // ---------------------------------------------------------------------
        //  Celebrations
        // ---------------------------------------------------------------------

        public void DismissCelebration() => _state.CurrentCelebration.Value = null;

        public void TriggerCelebration(CelebrationData data)
        {
            _state.CurrentCelebration.Value = data;
            PlayRewardChime();
        }

        // ---------------------------------------------------------------------
        //  Daily quests
        // ---------------------------------------------------------------------

        /// <summary>
        /// Resets daily-quest progress + claim flags on the first play of a new day,
        /// mirroring the date check in <c>loadDailyQuestsAndAchievements()</c>. Call
        /// at boot, after the save layer has loaded state.
        /// </summary>
        public void ResetDailyQuestsIfNewDay()
        {
            string today = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            if (_state.LastDailyQuestDate == today) return;

            _state.LastDailyQuestDate = today;
            _state.DailyQuestCatchCount.Value = 0;
            _state.DailyQuestRareCount.Value = 0;
            _state.DailyQuestGoldEarned.Value = 0;
            _state.DailyQuestCatchClaimed.Value = false;
            _state.DailyQuestRareClaimed.Value = false;
            _state.DailyQuestGoldClaimed.Value = false;
            _save.Save();
        }

        public void ClaimDailyQuest(string questType)
        {
            switch (questType)
            {
                case "catch":
                    if (_state.DailyQuestCatchCount.Value >= 3 && !_state.DailyQuestCatchClaimed.Value)
                    {
                        _state.DailyQuestCatchClaimed.Value = true;
                        _state.Coins.Value += 50;
                        AddXp(50);
                        _state.BaitWormCount.Value += 2;
                        _save.Save();

                        TriggerCelebration(new CelebrationData(
                            "quest_catch",
                            "일일 미션 완료: 유유자적 물놀이",
                            "Daily Quest Claimed: Leisurely Catch",
                            "오늘 아무 물고기나 3마리 포획하기 목표를 정성스레 달성했습니다.",
                            "You have successfully caught 3 fish of any kind today.",
                            "DAILY_QUEST",
                            new List<CelebrationReward>
                            {
                                new CelebrationReward("coins", 50, "50 골드", "50 Gold"),
                                new CelebrationReward("xp", 50, "경험치 50 XP", "50 XP"),
                                new CelebrationReward("bait_worm", 2, "갯지렁이 미끼 2개", "2 Worm Baits"),
                            },
                            "🎯"));
                    }
                    break;

                case "rare":
                    if (_state.DailyQuestRareCount.Value >= 1 && !_state.DailyQuestRareClaimed.Value)
                    {
                        _state.DailyQuestRareClaimed.Value = true;
                        _state.Coins.Value += 80;
                        AddXp(80);
                        _state.BaitShrimpCount.Value += 1;
                        _save.Save();

                        TriggerCelebration(new CelebrationData(
                            "quest_rare",
                            "일일 미션 완료: 희귀종 발견!",
                            "Daily Quest Claimed: Rare Sighting",
                            "오늘 희귀 등급 이상의 신선한 물고기를 1마리 이상 건져 올렸습니다.",
                            "You have successfully caught at least 1 Rare or higher tier fish today.",
                            "DAILY_QUEST",
                            new List<CelebrationReward>
                            {
                                new CelebrationReward("coins", 80, "80 골드", "80 Gold"),
                                new CelebrationReward("xp", 80, "경험치 80 XP", "80 XP"),
                                new CelebrationReward("bait_shrimp", 1, "크릴새우 미끼 1개", "1 Shrimp Bait"),
                            },
                            "🌟"));
                    }
                    break;

                case "gold":
                    if (_state.DailyQuestGoldEarned.Value >= 120 && !_state.DailyQuestGoldClaimed.Value)
                    {
                        _state.DailyQuestGoldClaimed.Value = true;
                        _state.Coins.Value += 100;
                        AddXp(100);
                        _state.BaitGoldenCount.Value += 1;
                        _save.Save();

                        TriggerCelebration(new CelebrationData(
                            "quest_gold",
                            "일일 미션 완료: 물결 위의 부귀",
                            "Daily Quest Claimed: Wealth on the Waves",
                            "오늘 물고기 골드 매각 수입 합계가 120골드 이상을 돌파하였습니다.",
                            "You have earned 120 or more gold through selling fish today.",
                            "DAILY_QUEST",
                            new List<CelebrationReward>
                            {
                                new CelebrationReward("coins", 100, "100 골드", "100 Gold"),
                                new CelebrationReward("xp", 100, "경험치 100 XP", "100 XP"),
                                new CelebrationReward("bait_golden", 1, "황금 고농축 떡밥 1개", "1 Golden Bait"),
                            },
                            "💰"));
                    }
                    break;
            }
            _save.Save();
        }

        // ---------------------------------------------------------------------
        //  Collection milestones
        // ---------------------------------------------------------------------

        public void ClaimCollectionMilestone(int milestone)
        {
            int uniqueCount = _state.CaughtFishList.Value.Select(f => f.SpeciesId).Distinct().Count();
            switch (milestone)
            {
                case 3:
                    if (uniqueCount >= 3 && !_state.ColMilestone3Claimed.Value)
                    {
                        _state.ColMilestone3Claimed.Value = true;
                        _state.Coins.Value += 200;
                        _state.BaitWormCount.Value += 2;
                        _save.Save();

                        TriggerCelebration(new CelebrationData(
                            "col_milestone_3",
                            "도감 마일스톤 달성: 입문 소집가",
                            "Collection Goal: Novice Collector",
                            "물고기 도감에 3종 이상의 서로 다른 어류를 아름답게 기록하셨습니다.",
                            "You have recorded 3 or more distinct species in your log book.",
                            "COLLECTION",
                            new List<CelebrationReward>
                            {
                                new CelebrationReward("coins", 200, "200 골드", "200 Gold"),
                                new CelebrationReward("bait_worm", 2, "갯지렁이 미끼 2개", "2 Worm Baits"),
                            },
                            "📜"));
                    }
                    break;

                case 6:
                    if (uniqueCount >= 6 && !_state.ColMilestone6Claimed.Value)
                    {
                        _state.ColMilestone6Claimed.Value = true;
                        _state.Coins.Value += 500;
                        _state.BaitShrimpCount.Value += 2;
                        _save.Save();

                        TriggerCelebration(new CelebrationData(
                            "col_milestone_6",
                            "도감 마일스톤 달성: 노련한 학자",
                            "Collection Goal: Adept Angler",
                            "물고기 도감에 6종 이상의 서로 다른 어류를 훌륭히 등재하셨습니다.",
                            "You have recorded 6 or more distinct species in your log book.",
                            "COLLECTION",
                            new List<CelebrationReward>
                            {
                                new CelebrationReward("coins", 500, "500 골드", "500 Gold"),
                                new CelebrationReward("bait_shrimp", 2, "크릴새우 미끼 2개", "2 Shrimp Baits"),
                            },
                            "🐠"));
                    }
                    break;

                case 9:
                    if (uniqueCount >= 9 && !_state.ColMilestone9Claimed.Value)
                    {
                        _state.ColMilestone9Claimed.Value = true;
                        _state.Coins.Value += 1000;
                        _state.BaitGoldenCount.Value += 2;
                        _save.Save();

                        TriggerCelebration(new CelebrationData(
                            "col_milestone_9",
                            "도감 마일스톤 달성: 생태 탐구자",
                            "Collection Goal: Freshwater Ecologist",
                            "물고기 도감에 9종 이상의 서로 다른 어류를 찾아내어 생존을 보존했습니다.",
                            "You have recorded 9 or more distinct species in your log book.",
                            "COLLECTION",
                            new List<CelebrationReward>
                            {
                                new CelebrationReward("coins", 1000, "1000 골드", "1000 Gold"),
                                new CelebrationReward("bait_golden", 2, "황금 고농축 떡밥 2개", "2 Golden Baits"),
                            },
                            "🔬"));
                    }
                    break;

                case 12:
                    if (uniqueCount >= 12 && !_state.ColMilestone12Claimed.Value)
                    {
                        _state.ColMilestone12Claimed.Value = true;
                        _state.Coins.Value += 2500;
                        _state.BaitGoldenCount.Value += 5;
                        _save.Save();

                        TriggerCelebration(new CelebrationData(
                            "col_milestone_12",
                            "도감 마일스톤 달성: 대완성 마스터!",
                            "Collection Goal: Grand Dictionary Master!",
                            "모든 12종의 다양한 신형 생태 물고기를 도감에 완벽히 정복하셨습니다!",
                            "You have successfully completed your log book with all 12 unique species!",
                            "COLLECTION",
                            new List<CelebrationReward>
                            {
                                new CelebrationReward("coins", 2500, "2500 골드", "2500 Gold"),
                                new CelebrationReward("bait_golden", 5, "황금 고농축 떡밥 5개", "5 Golden Baits"),
                            },
                            "👑"));
                    }
                    break;
            }
            _save.Save();
        }

        // ---------------------------------------------------------------------
        //  Achievements
        // ---------------------------------------------------------------------

        public void ClaimAchievement(string achId)
        {
            switch (achId)
            {
                case "catch_10":
                    if (_state.TotalFishCaught.Value >= 10 && !_state.AchCatch10Claimed.Value)
                    {
                        _state.AchCatch10Claimed.Value = true;
                        _state.Coins.Value += 100;

                        TriggerCelebration(new CelebrationData(
                            "ach_catch_10",
                            "업적 달성: 초보 강태공",
                            "Achievement: Rookie Angler",
                            "물의 흐름을 파악하는 법을 배우며 누적 10마리 이상 낚시에 정성껏 성공하였습니다.",
                            "You have successfully caught a lifetime total of 10 or more fish.",
                            "ACHIEVEMENT",
                            new List<CelebrationReward> { new CelebrationReward("coins", 100, "100 골드", "100 Gold") },
                            "🎣"));
                    }
                    break;

                case "mythic":
                    if (_state.HasCaughtLegendaryOrMythic.Value && !_state.AchMythicClaimed.Value)
                    {
                        _state.AchMythicClaimed.Value = true;
                        _state.Coins.Value += 250;

                        TriggerCelebration(new CelebrationData(
                            "ach_mythic",
                            "업적 달성: 전설을 마주하다",
                            "Achievement: Legend Met",
                            "수심 깊은 은하의 고요 아래 숨쉬던 전설 또는 신화 등급의 귀족 어종을 생포했습니다.",
                            "You have fought and caught an elusive Legendary or Mythic grade specimen.",
                            "ACHIEVEMENT",
                            new List<CelebrationReward> { new CelebrationReward("coins", 250, "250 골드", "250 Gold") },
                            "🦄"));
                    }
                    break;

                case "rod_3":
                    if (_state.RodLevel.Value >= 3 && !_state.AchRod3Claimed.Value)
                    {
                        _state.AchRod3Claimed.Value = true;
                        _state.Coins.Value += 400;

                        TriggerCelebration(new CelebrationData(
                            "ach_rod_3",
                            "업적 달성: 정밀한 마스터",
                            "Achievement: Precision Hand",
                            "낚싯대 개조 레벨을 3개성 수준으로 정교히 가꾸어 한 단계 높였습니다.",
                            "You have customized and upgraded your fishing rod to level 3 or higher.",
                            "ACHIEVEMENT",
                            new List<CelebrationReward> { new CelebrationReward("coins", 400, "400 골드", "400 Gold") },
                            "🛠️"));
                    }
                    break;

                case "sell_20":
                    if (_state.TotalFishSold.Value >= 20 && !_state.AchSell20Claimed.Value)
                    {
                        _state.AchSell20Claimed.Value = true;
                        _state.Coins.Value += 500;

                        TriggerCelebration(new CelebrationData(
                            "ach_sell_20",
                            "업적 달성: 노련한 지배자",
                            "Achievement: Shrewd Merchant",
                            "누적 20마리 이상 물고기를 맑은 자연으로 방생하여 호수의 조화를 지켰습니다.",
                            "You have successfully sold or released a lifetime total of 20 or more fish.",
                            "ACHIEVEMENT",
                            new List<CelebrationReward> { new CelebrationReward("coins", 500, "500 골드", "500 Gold") },
                            "⚖️"));
                    }
                    break;

                case "coins_2000":
                    if (_state.Coins.Value >= 2000 && !_state.AchCoins2000Claimed.Value)
                    {
                        _state.AchCoins2000Claimed.Value = true;
                        _state.Coins.Value += 1000;
                        _state.BaitWormCount.Value += 3;
                        _state.BaitShrimpCount.Value += 3;
                        _state.BaitGoldenCount.Value += 3;
                        _save.Save();

                        TriggerCelebration(new CelebrationData(
                            "ach_coins_2000",
                            "업적 달성: 황금빛 만선",
                            "Achievement: Golden Harvest",
                            "수중에 차곡차곡 모아낸 고유 자금이 드디어 2000골드를 시원하게 돌파했습니다.",
                            "You have accumulated a personal budget of 2,000 or more gold.",
                            "ACHIEVEMENT",
                            new List<CelebrationReward>
                            {
                                new CelebrationReward("coins", 1000, "1000 골드", "1000 Gold"),
                                new CelebrationReward("bait_worm", 3, "갯지렁이 미끼 3개", "3 Worm Baits"),
                                new CelebrationReward("bait_shrimp", 3, "크릴새우 미끼 3개", "3 Shrimp Baits"),
                                new CelebrationReward("bait_golden", 3, "황금 고농축 떡밥 3개", "3 Golden Baits"),
                            },
                            "💫"));
                    }
                    break;
            }
            _save.Save();
        }

        // ---------------------------------------------------------------------
        //  Audio sequencing helpers (fire-and-forget, lifetime-scoped)
        // ---------------------------------------------------------------------

        private void PlayRewardChime()
        {
            _ = PlayChimeSequenceAsync(_lifetimeCts.Token,
                (0, 523.25f, 0.15f), (120, 659.25f, 0.15f), (120, 783.99f, 0.15f), (120, 1046.50f, 0.25f));
        }

        private async Awaitable PlayChimeSequenceAsync(CancellationToken ct, params (int delayMs, float freq, float vol)[] notes)
        {
            if (_audio == null) return; // nothing to play — also keeps head-less callers off the player loop
            try
            {
                foreach (var n in notes)
                {
                    if (n.delayMs > 0) await DelayMs(n.delayMs, ct);
                    _audio?.TriggerChime(n.freq, n.vol);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Awaitable PlayDelayedChimeAsync(long delayMs, float freq, float vol, CancellationToken ct)
        {
            if (_audio == null) return;
            try
            {
                await DelayMs(delayMs, ct);
                _audio?.TriggerChime(freq, vol);
            }
            catch (OperationCanceledException) { }
        }

        // ---------------------------------------------------------------------
        //  Lifecycle + small helpers
        // ---------------------------------------------------------------------

        /// <summary>Cancels all running flows. Mirrors the Android <c>onCleared()</c>.</summary>
        public void Dispose()
        {
            CancelFishingJob();
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();
        }

        /// <summary>Cancels the current fishing flow and starts a fresh cancellation scope (≈ reassigning fishingJob).</summary>
        private CancellationToken StartFishingJob()
        {
            CancelFishingJob();
            _fishingCts = new CancellationTokenSource();
            return _fishingCts.Token;
        }

        private void CancelFishingJob()
        {
            if (_fishingCts == null) return;
            _fishingCts.Cancel();
            _fishingCts.Dispose();
            _fishingCts = null;
        }

        private static async Awaitable DelayMs(long ms, CancellationToken ct)
        {
            await Awaitable.WaitForSecondsAsync(ms / 1000f, ct);
        }

        /// <summary>Kotlin <c>Random.nextLong(min, max)</c> — uniform in [min, max).</summary>
        private static long RandomLong(long minInclusive, long maxExclusive)
            => UnityEngine.Random.Range((int)minInclusive, (int)maxExclusive);

        private static string MapTimeOfDayId(TimeOfDay t) => t switch
        {
            TimeOfDay.Day => "day",
            TimeOfDay.Sunset => "sunset",
            TimeOfDay.Night => "night",
            _ => "day",
        };

        private static long NowUnixMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private static bool IsIn(string id, params string[] ids) => Array.IndexOf(ids, id) >= 0;
    }
}
