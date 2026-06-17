using System;
using System.Collections.Generic;
using YoonseulFishing.Core;

namespace YoonseulFishing.Data
{
    /// <summary>
    /// Flat, <see cref="JsonUtility"/>-friendly snapshot of the persisted parts of
    /// <see cref="GameState"/>. Mirrors what the Android build kept across
    /// SharedPreferences (progress / settings / quests) and the Room table
    /// (caught fish), unified here into a single JSON document.
    ///
    /// Only persisted fields are included — transient runtime state
    /// (FishingState, bobber position, rhythm counters, current celebration/alert,
    /// time/weather/nature cycle, level-up dialog, GPG sign-in) is intentionally
    /// left out, matching the original's persistence.
    ///
    /// <see cref="BaitType"/> / <see cref="FishingSpot"/> are reference singletons,
    /// so they are stored by their stable <c>Id</c> string and resolved back on load.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Save-format version, for future migrations.</summary>
        public int version = 1;

        // --- Progression ---
        public int fishingLevel = 1;
        public int fishingXp = 0;
        public int coins = 0;
        public int rodLevel = 1;
        public bool hasCastBefore = false;

        // --- Settings ---
        public AppLanguage language = AppLanguage.Ko; // serialized as its int ordinal
        public bool isMuted = false;

        // --- Bait ---
        public string activeBaitId = "basic";
        public int baitWormCount = 0;
        public int baitShrimpCount = 0;
        public int baitGoldenCount = 0;

        // --- Spot ---
        public string currentSpotId = "windy_valley";

        // --- Lifetime stats ---
        public int totalFishCaught = 0;
        public int totalFishSold = 0;
        public bool hasCaughtLegendaryOrMythic = false;

        // --- Daily quests ---
        public string lastDailyQuestDate = "";
        public int dailyQuestCatchCount = 0;
        public int dailyQuestRareCount = 0;
        public int dailyQuestGoldEarned = 0;
        public bool dailyQuestCatchClaimed = false;
        public bool dailyQuestRareClaimed = false;
        public bool dailyQuestGoldClaimed = false;

        // --- Achievements ---
        public bool achCatch10Claimed = false;
        public bool achMythicClaimed = false;
        public bool achRod3Claimed = false;
        public bool achSell20Claimed = false;
        public bool achCoins2000Claimed = false;

        // --- Collection milestones ---
        public bool colMilestone3Claimed = false;
        public bool colMilestone6Claimed = false;
        public bool colMilestone9Claimed = false;
        public bool colMilestone12Claimed = false;

        // --- Collection ---
        public List<CaughtFish> caughtFish = new List<CaughtFish>();

        /// <summary>Captures the persisted fields of a live <see cref="GameState"/>.</summary>
        public static SaveData FromState(GameState s)
        {
            return new SaveData
            {
                version = 1,

                fishingLevel = s.FishingLevel.Value,
                fishingXp = s.FishingXp.Value,
                coins = s.Coins.Value,
                rodLevel = s.RodLevel.Value,
                hasCastBefore = s.HasCastBefore.Value,

                language = s.Language.Value,
                isMuted = s.IsMuted.Value,

                activeBaitId = s.ActiveBait.Value.Id,
                baitWormCount = s.BaitWormCount.Value,
                baitShrimpCount = s.BaitShrimpCount.Value,
                baitGoldenCount = s.BaitGoldenCount.Value,

                currentSpotId = s.CurrentSpot.Value.Id,

                totalFishCaught = s.TotalFishCaught.Value,
                totalFishSold = s.TotalFishSold.Value,
                hasCaughtLegendaryOrMythic = s.HasCaughtLegendaryOrMythic.Value,

                lastDailyQuestDate = s.LastDailyQuestDate,
                dailyQuestCatchCount = s.DailyQuestCatchCount.Value,
                dailyQuestRareCount = s.DailyQuestRareCount.Value,
                dailyQuestGoldEarned = s.DailyQuestGoldEarned.Value,
                dailyQuestCatchClaimed = s.DailyQuestCatchClaimed.Value,
                dailyQuestRareClaimed = s.DailyQuestRareClaimed.Value,
                dailyQuestGoldClaimed = s.DailyQuestGoldClaimed.Value,

                achCatch10Claimed = s.AchCatch10Claimed.Value,
                achMythicClaimed = s.AchMythicClaimed.Value,
                achRod3Claimed = s.AchRod3Claimed.Value,
                achSell20Claimed = s.AchSell20Claimed.Value,
                achCoins2000Claimed = s.AchCoins2000Claimed.Value,

                colMilestone3Claimed = s.ColMilestone3Claimed.Value,
                colMilestone6Claimed = s.ColMilestone6Claimed.Value,
                colMilestone9Claimed = s.ColMilestone9Claimed.Value,
                colMilestone12Claimed = s.ColMilestone12Claimed.Value,

                caughtFish = new List<CaughtFish>(s.CaughtFishList.Value),
            };
        }

        /// <summary>Writes these persisted fields back onto a <see cref="GameState"/> (boot load).</summary>
        public void ApplyTo(GameState s)
        {
            s.FishingLevel.Value = fishingLevel;
            s.FishingXp.Value = fishingXp;
            s.Coins.Value = coins;
            s.RodLevel.Value = rodLevel;
            s.HasCastBefore.Value = hasCastBefore;

            s.Language.Value = language;
            s.IsEnglish.Value = (language == AppLanguage.En);
            s.IsMuted.Value = isMuted;

            s.ActiveBait.Value = BaitType.FromId(activeBaitId);
            s.BaitWormCount.Value = baitWormCount;
            s.BaitShrimpCount.Value = baitShrimpCount;
            s.BaitGoldenCount.Value = baitGoldenCount;

            s.CurrentSpot.Value = FishingSpot.FromId(currentSpotId);

            s.TotalFishCaught.Value = totalFishCaught;
            s.TotalFishSold.Value = totalFishSold;
            s.HasCaughtLegendaryOrMythic.Value = hasCaughtLegendaryOrMythic;

            s.LastDailyQuestDate = lastDailyQuestDate ?? "";
            s.DailyQuestCatchCount.Value = dailyQuestCatchCount;
            s.DailyQuestRareCount.Value = dailyQuestRareCount;
            s.DailyQuestGoldEarned.Value = dailyQuestGoldEarned;
            s.DailyQuestCatchClaimed.Value = dailyQuestCatchClaimed;
            s.DailyQuestRareClaimed.Value = dailyQuestRareClaimed;
            s.DailyQuestGoldClaimed.Value = dailyQuestGoldClaimed;

            s.AchCatch10Claimed.Value = achCatch10Claimed;
            s.AchMythicClaimed.Value = achMythicClaimed;
            s.AchRod3Claimed.Value = achRod3Claimed;
            s.AchSell20Claimed.Value = achSell20Claimed;
            s.AchCoins2000Claimed.Value = achCoins2000Claimed;

            s.ColMilestone3Claimed.Value = colMilestone3Claimed;
            s.ColMilestone6Claimed.Value = colMilestone6Claimed;
            s.ColMilestone9Claimed.Value = colMilestone9Claimed;
            s.ColMilestone12Claimed.Value = colMilestone12Claimed;

            s.CaughtFishList.Value = caughtFish ?? new List<CaughtFish>();
        }
    }
}
