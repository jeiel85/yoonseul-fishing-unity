using NUnit.Framework;
using YoonseulFishing.Core;
using YoonseulFishing.Data;

namespace YoonseulFishing.Tests
{
    /// <summary>
    /// Level / XP / coin-value math — the deterministic progression logic ported
    /// from FishingViewModel.kt. No timing or RNG involved.
    /// </summary>
    public class ProgressionTests
    {
        private sealed class FakeSave : ISaveService { public int SaveCount; public void Save() => SaveCount++; }

        private static GameController NewController(out GameState state)
        {
            state = new GameState();
            return new GameController(state, new FakeSave(), null);
        }

        [Test]
        public void XpNeeded_IsLevelTimes100()
        {
            Assert.AreEqual(100, GameController.XpNeededForLevel(1));
            Assert.AreEqual(500, GameController.XpNeededForLevel(5));
        }

        [Test]
        public void ApplyXp_NoLevelUp_BelowThreshold()
        {
            var (level, xp, up) = GameController.ApplyXp(1, 0, 50);
            Assert.AreEqual(1, level);
            Assert.AreEqual(50, xp);
            Assert.IsFalse(up);
        }

        [Test]
        public void ApplyXp_LevelsUp_OnExactThreshold()
        {
            var (level, xp, up) = GameController.ApplyXp(1, 0, 100);
            Assert.AreEqual(2, level);
            Assert.AreEqual(0, xp);
            Assert.IsTrue(up);
        }

        [Test]
        public void ApplyXp_CarriesRemainder()
        {
            // 50 + 60 = 110, need(1) = 100 -> level 2 with 10 carried
            var (level, xp, up) = GameController.ApplyXp(1, 50, 60);
            Assert.AreEqual(2, level);
            Assert.AreEqual(10, xp);
            Assert.IsTrue(up);
        }

        [Test]
        public void ApplyXp_CrossesMultipleLevelBoundaries()
        {
            // need(1)=100, need(2)=200: 350 -> L2 (250 left) -> L3 (50 left) -> stop
            var (level, xp, up) = GameController.ApplyXp(1, 0, 350);
            Assert.AreEqual(3, level);
            Assert.AreEqual(50, xp);
            Assert.IsTrue(up);
        }

        [Test]
        public void UpgradeCost_ScalesWithRodLevel()
        {
            var ctrl = NewController(out var state);
            Assert.AreEqual(150, ctrl.GetUpgradeCost()); // rod level 1
            state.RodLevel.Value = 3;
            Assert.AreEqual(450, ctrl.GetUpgradeCost());
        }

        [Test]
        public void FishValue_ScalesWithLength_FloorsAtBasePrice()
        {
            var ctrl = NewController(out _);

            // gold_crucian: common (base 20), baseLengthMin 15
            var atMin = new CaughtFish("gold_crucian", 0.3f, 15f, "day", 0L); // 15/15 = 1.0x -> 20
            Assert.AreEqual(20, ctrl.GetFishValue(atMin));

            var doubled = new CaughtFish("gold_crucian", 0.5f, 30f, "day", 0L); // 30/15 = 2.0x -> 40
            Assert.AreEqual(40, ctrl.GetFishValue(doubled));

            // unknown species -> fallback 10
            Assert.AreEqual(10, ctrl.GetFishValue(new CaughtFish("???", 1f, 1f, "day", 0L)));
        }
    }
}
