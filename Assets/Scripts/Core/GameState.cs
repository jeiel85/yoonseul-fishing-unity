using System.Collections.Generic;
using YoonseulFishing.Data;

namespace YoonseulFishing.Core
{
    /// <summary>
    /// Central observable game state — the C# port of every <c>StateFlow</c> that
    /// lived in FishingViewModel.kt. Game logic (GameController, ported next)
    /// writes to these; the UI layer subscribes to each one's <c>Changed</c> event.
    ///
    /// Initial values mirror the Kotlin defaults; persisted values are loaded over
    /// the top of these by the save layer during boot.
    /// </summary>
    public class GameState
    {
        // --- Environment cycle ---
        public readonly Observable<TimeOfDay> TimeOfDay = new Observable<TimeOfDay>(Data.TimeOfDay.Day);
        public readonly Observable<Weather> Weather = new Observable<Weather>(Data.Weather.Clear);
        public readonly Observable<NatureSound> NatureSound = new Observable<NatureSound>(Data.NatureSound.WaterLap);

        // --- Fishing flow ---
        public readonly Observable<FishingState> FishingState = new Observable<FishingState>(Data.FishingState.Idle);
        public readonly Observable<bool> HasCastBefore = new Observable<bool>(false);

        // Bobber position (0..1 relative to the water canvas)
        public readonly Observable<float> BobberPositionX = new Observable<float>(0.5f);
        public readonly Observable<float> BobberPositionY = new Observable<float>(0.7f);

        // --- Rhythm mini-game ---
        public readonly Observable<float> RhythmRingScale = new Observable<float>(1.0f);
        public readonly Observable<int> RhythmHitCount = new Observable<int>(0);
        public readonly Observable<int> RhythmMissCount = new Observable<int>(0);
        public readonly Observable<string> RhythmFeedbackText = new Observable<string>(null);
        public readonly Observable<bool> RhythmBeatActive = new Observable<bool>(false);

        // --- Collection ---
        public readonly Observable<IReadOnlyList<CaughtFish>> CaughtFishList =
            new Observable<IReadOnlyList<CaughtFish>>(new List<CaughtFish>());
        public readonly Observable<CaughtFish> LastCaughtFish = new Observable<CaughtFish>(null);

        // --- Settings / locale ---
        public readonly Observable<bool> IsMuted = new Observable<bool>(false);
        public readonly Observable<bool> IsEnglish = new Observable<bool>(false);
        public readonly Observable<AppLanguage> Language = new Observable<AppLanguage>(AppLanguage.Ko);

        // --- Progression ---
        public readonly Observable<int> FishingLevel = new Observable<int>(1);
        public readonly Observable<int> FishingXp = new Observable<int>(0);
        public readonly Observable<int> Coins = new Observable<int>(0);
        public readonly Observable<int> RodLevel = new Observable<int>(1);
        public readonly Observable<int?> ShowLevelUpDialog = new Observable<int?>(null);

        // --- Spot ---
        public readonly Observable<FishingSpot> CurrentSpot = new Observable<FishingSpot>(FishingSpot.WindyValley);

        // --- Daily quests ---
        /// <summary>
        /// <c>yyyyMMdd</c> of the day the current daily-quest progress belongs to.
        /// Mirrors the Android <c>"daily_quest_date"</c> pref. Not surfaced to the UI
        /// (no observers), but it is part of the persisted save model: GameController
        /// compares it against today to reset quests on the first play of a new day.
        /// </summary>
        public string LastDailyQuestDate = "";

        public readonly Observable<int> DailyQuestCatchCount = new Observable<int>(0);
        public readonly Observable<int> DailyQuestRareCount = new Observable<int>(0);
        public readonly Observable<int> DailyQuestGoldEarned = new Observable<int>(0);
        public readonly Observable<bool> DailyQuestCatchClaimed = new Observable<bool>(false);
        public readonly Observable<bool> DailyQuestRareClaimed = new Observable<bool>(false);
        public readonly Observable<bool> DailyQuestGoldClaimed = new Observable<bool>(false);

        // --- Achievements ---
        public readonly Observable<bool> AchCatch10Claimed = new Observable<bool>(false);
        public readonly Observable<bool> AchMythicClaimed = new Observable<bool>(false);
        public readonly Observable<bool> AchRod3Claimed = new Observable<bool>(false);
        public readonly Observable<bool> AchSell20Claimed = new Observable<bool>(false);
        public readonly Observable<bool> AchCoins2000Claimed = new Observable<bool>(false);

        // --- Collection milestones ---
        public readonly Observable<bool> ColMilestone3Claimed = new Observable<bool>(false);
        public readonly Observable<bool> ColMilestone6Claimed = new Observable<bool>(false);
        public readonly Observable<bool> ColMilestone9Claimed = new Observable<bool>(false);
        public readonly Observable<bool> ColMilestone12Claimed = new Observable<bool>(false);

        // --- Celebration / alerts ---
        public readonly Observable<CelebrationData> CurrentCelebration = new Observable<CelebrationData>(null);
        public readonly Observable<NotificationAlert> InAppAlert = new Observable<NotificationAlert>(null);

        // --- Lifetime stats ---
        public readonly Observable<int> TotalFishCaught = new Observable<int>(0);
        public readonly Observable<int> TotalFishSold = new Observable<int>(0);
        public readonly Observable<bool> HasCaughtLegendaryOrMythic = new Observable<bool>(false);

        // --- Misc ---
        public readonly Observable<bool> IsGpgSignedIn = new Observable<bool>(false);

        // --- Bait ---
        public readonly Observable<BaitType> ActiveBait = new Observable<BaitType>(BaitType.Basic);
        public readonly Observable<int> BaitWormCount = new Observable<int>(0);
        public readonly Observable<int> BaitShrimpCount = new Observable<int>(0);
        public readonly Observable<int> BaitGoldenCount = new Observable<int>(0);

        public int GetBaitCount(BaitType bait)
        {
            if (bait == BaitType.Basic) return 99999;
            if (bait == BaitType.Worm) return BaitWormCount.Value;
            if (bait == BaitType.Shrimp) return BaitShrimpCount.Value;
            if (bait == BaitType.Golden) return BaitGoldenCount.Value;
            return 0;
        }
    }
}
