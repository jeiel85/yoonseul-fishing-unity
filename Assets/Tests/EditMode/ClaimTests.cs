using System.Collections.Generic;
using NUnit.Framework;
using YoonseulFishing.Core;
using YoonseulFishing.Data;

namespace YoonseulFishing.Tests
{
    /// <summary>
    /// Claim-condition logic for daily quests, achievements, and collection
    /// milestones — plus the bait-shop guard. Audio is null (cosmetic chime
    /// sequences short-circuit), so only the deterministic state changes are checked.
    /// </summary>
    public class ClaimTests
    {
        private sealed class FakeSave : ISaveService { public void Save() { } }

        private static GameController New(out GameState s)
        {
            s = new GameState();
            return new GameController(s, new FakeSave(), null);
        }

        [Test]
        public void DailyCatchQuest_RequiresThreeCatches()
        {
            var c = New(out var s);

            s.DailyQuestCatchCount.Value = 2;
            c.ClaimDailyQuest("catch");
            Assert.IsFalse(s.DailyQuestCatchClaimed.Value, "must not claim below the goal");
            Assert.AreEqual(0, s.Coins.Value);

            s.DailyQuestCatchCount.Value = 3;
            c.ClaimDailyQuest("catch");
            Assert.IsTrue(s.DailyQuestCatchClaimed.Value);
            Assert.AreEqual(50, s.Coins.Value);          // +50 gold
            Assert.AreEqual(2, s.BaitWormCount.Value);   // +2 worm bait
            Assert.IsNotNull(s.CurrentCelebration.Value); // celebration popup fired
        }

        [Test]
        public void DailyCatchQuest_NotClaimableTwice()
        {
            var c = New(out var s);
            s.DailyQuestCatchCount.Value = 3;
            c.ClaimDailyQuest("catch");
            int coinsAfterFirst = s.Coins.Value;

            c.ClaimDailyQuest("catch");
            Assert.AreEqual(coinsAfterFirst, s.Coins.Value, "second claim must be a no-op");
        }

        [Test]
        public void Achievement_Catch10_RequiresLifetimeTen()
        {
            var c = New(out var s);

            s.TotalFishCaught.Value = 9;
            c.ClaimAchievement("catch_10");
            Assert.IsFalse(s.AchCatch10Claimed.Value);

            s.TotalFishCaught.Value = 10;
            c.ClaimAchievement("catch_10");
            Assert.IsTrue(s.AchCatch10Claimed.Value);
            Assert.AreEqual(100, s.Coins.Value);
        }

        [Test]
        public void CollectionMilestone_CountsDistinctSpecies()
        {
            var c = New(out var s);

            // 3 records but only 2 distinct species -> milestone 3 not reached
            s.CaughtFishList.Value = new List<CaughtFish>
            {
                new CaughtFish("gold_crucian", 1f, 20f, "day", 0L),
                new CaughtFish("gold_crucian", 1f, 21f, "day", 0L),
                new CaughtFish("koi", 1f, 60f, "day", 0L),
            };
            c.ClaimCollectionMilestone(3);
            Assert.IsFalse(s.ColMilestone3Claimed.Value, "2 distinct species < 3");

            // 3 distinct species -> claimable
            s.CaughtFishList.Value = new List<CaughtFish>
            {
                new CaughtFish("gold_crucian", 1f, 20f, "day", 0L),
                new CaughtFish("koi", 1f, 60f, "day", 0L),
                new CaughtFish("catfish", 1f, 40f, "night", 0L),
            };
            c.ClaimCollectionMilestone(3);
            Assert.IsTrue(s.ColMilestone3Claimed.Value);
            Assert.AreEqual(200, s.Coins.Value);
        }

        [Test]
        public void BuyBait_DeductsCoins_AndAddsCount()
        {
            var c = New(out var s);
            s.Coins.Value = 100;

            bool ok = c.BuyBait(BaitType.Worm, 2); // 10 * 2 = 20
            Assert.IsTrue(ok);
            Assert.AreEqual(80, s.Coins.Value);
            Assert.AreEqual(2, s.BaitWormCount.Value);
        }

        [Test]
        public void BuyBait_FailsWhenInsufficientCoins()
        {
            var c = New(out var s);
            s.Coins.Value = 5;

            bool ok = c.BuyBait(BaitType.Worm, 1); // costs 10
            Assert.IsFalse(ok);
            Assert.AreEqual(5, s.Coins.Value);
            Assert.AreEqual(0, s.BaitWormCount.Value);
        }

        [Test]
        public void BuyBasicBait_AlwaysFails()
        {
            var c = New(out var s);
            s.Coins.Value = 1000;
            Assert.IsFalse(c.BuyBait(BaitType.Basic), "basic bait is free/infinite, not purchasable");
            Assert.AreEqual(1000, s.Coins.Value);
        }
    }
}
