using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using YoonseulFishing.Core;
using YoonseulFishing.Data;

namespace YoonseulFishing.Tests
{
    /// <summary>
    /// Serialization round-trips for the save layer: GameState → SaveData → JSON →
    /// SaveData → GameState must preserve every persisted field. Exercises the real
    /// Unity <see cref="JsonUtility"/> (so it only runs in the editor / a player).
    /// </summary>
    public class SaveDataTests
    {
        private static GameState RoundTrip(GameState source)
        {
            string json = JsonUtility.ToJson(SaveData.FromState(source));
            var restored = new GameState();
            JsonUtility.FromJson<SaveData>(json).ApplyTo(restored);
            return restored;
        }

        [Test]
        public void RoundTrip_PreservesProgressionAndSettings()
        {
            var s1 = new GameState();
            s1.FishingLevel.Value = 7;
            s1.FishingXp.Value = 250;
            s1.Coins.Value = 1234;
            s1.RodLevel.Value = 4;
            s1.HasCastBefore.Value = true;
            s1.Language.Value = AppLanguage.Ja;
            s1.IsMuted.Value = true;
            s1.ActiveBait.Value = BaitType.Golden;
            s1.CurrentSpot.Value = FishingSpot.WaveBeach;
            s1.BaitWormCount.Value = 3;
            s1.BaitShrimpCount.Value = 1;
            s1.BaitGoldenCount.Value = 5;
            s1.TotalFishCaught.Value = 42;
            s1.TotalFishSold.Value = 20;
            s1.HasCaughtLegendaryOrMythic.Value = true;

            var s2 = RoundTrip(s1);

            Assert.AreEqual(7, s2.FishingLevel.Value);
            Assert.AreEqual(250, s2.FishingXp.Value);
            Assert.AreEqual(1234, s2.Coins.Value);
            Assert.AreEqual(4, s2.RodLevel.Value);
            Assert.IsTrue(s2.HasCastBefore.Value);
            Assert.AreEqual(AppLanguage.Ja, s2.Language.Value);
            Assert.IsTrue(s2.IsMuted.Value);
            Assert.AreSame(BaitType.Golden, s2.ActiveBait.Value);
            Assert.AreSame(FishingSpot.WaveBeach, s2.CurrentSpot.Value);
            Assert.AreEqual(3, s2.BaitWormCount.Value);
            Assert.AreEqual(1, s2.BaitShrimpCount.Value);
            Assert.AreEqual(5, s2.BaitGoldenCount.Value);
            Assert.AreEqual(42, s2.TotalFishCaught.Value);
            Assert.AreEqual(20, s2.TotalFishSold.Value);
            Assert.IsTrue(s2.HasCaughtLegendaryOrMythic.Value);
        }

        [Test]
        public void RoundTrip_PreservesQuestsAchievementsAndMilestones()
        {
            var s1 = new GameState();
            s1.LastDailyQuestDate = "20260617";
            s1.DailyQuestCatchCount.Value = 3;
            s1.DailyQuestRareCount.Value = 1;
            s1.DailyQuestGoldEarned.Value = 120;
            s1.DailyQuestCatchClaimed.Value = true;
            s1.AchMythicClaimed.Value = true;
            s1.AchCoins2000Claimed.Value = true;
            s1.ColMilestone6Claimed.Value = true;
            s1.ColMilestone12Claimed.Value = true;

            var s2 = RoundTrip(s1);

            Assert.AreEqual("20260617", s2.LastDailyQuestDate);
            Assert.AreEqual(3, s2.DailyQuestCatchCount.Value);
            Assert.AreEqual(1, s2.DailyQuestRareCount.Value);
            Assert.AreEqual(120, s2.DailyQuestGoldEarned.Value);
            Assert.IsTrue(s2.DailyQuestCatchClaimed.Value);
            Assert.IsTrue(s2.AchMythicClaimed.Value);
            Assert.IsTrue(s2.AchCoins2000Claimed.Value);
            Assert.IsTrue(s2.ColMilestone6Claimed.Value);
            Assert.IsTrue(s2.ColMilestone12Claimed.Value);
            // untouched flags stay false
            Assert.IsFalse(s2.AchCatch10Claimed.Value);
            Assert.IsFalse(s2.ColMilestone3Claimed.Value);
        }

        [Test]
        public void RoundTrip_PreservesCaughtFishList()
        {
            var s1 = new GameState();
            s1.CaughtFishList.Value = new List<CaughtFish>
            {
                new CaughtFish("koi", 5.5f, 60f, "day", 111L, 1),
                new CaughtFish("star_whale", 40f, 150f, "night", 222L, 2),
            };

            var s2 = RoundTrip(s1);

            Assert.AreEqual(2, s2.CaughtFishList.Value.Count);
            Assert.AreEqual("koi", s2.CaughtFishList.Value[0].SpeciesId);
            Assert.AreEqual(1, s2.CaughtFishList.Value[0].Id);
            Assert.AreEqual(60f, s2.CaughtFishList.Value[0].Length);
            Assert.AreEqual("star_whale", s2.CaughtFishList.Value[1].SpeciesId);
            Assert.AreEqual(222L, s2.CaughtFishList.Value[1].CaughtTime);
            Assert.AreEqual("night", s2.CaughtFishList.Value[1].TimeOfDay);
        }

        [Test]
        public void DefaultState_RoundTripsToDefaults()
        {
            var s2 = RoundTrip(new GameState());
            Assert.AreEqual(1, s2.FishingLevel.Value);
            Assert.AreEqual(0, s2.Coins.Value);
            Assert.AreSame(BaitType.Basic, s2.ActiveBait.Value);
            Assert.AreSame(FishingSpot.WindyValley, s2.CurrentSpot.Value);
            Assert.AreEqual(0, s2.CaughtFishList.Value.Count);
        }

        [Test]
        public void EnglishLanguage_RestoresIsEnglishFlag()
        {
            var s1 = new GameState();
            s1.Language.Value = AppLanguage.En;
            var s2 = RoundTrip(s1);
            Assert.AreEqual(AppLanguage.En, s2.Language.Value);
            Assert.IsTrue(s2.IsEnglish.Value, "IsEnglish must be derived from the restored language");
        }
    }
}
