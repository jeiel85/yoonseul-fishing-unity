using NUnit.Framework;
using YoonseulFishing.Core;
using YoonseulFishing.Data;

namespace YoonseulFishing.Tests
{
    /// <summary>
    /// Probability-weight verification for the species roll. Each test isolates one
    /// factor (time / weather / bait / spot) by holding the others constant and
    /// asserting the exact multiplier ratio from FishingViewModel.kt's scoring.
    /// </summary>
    public class SpeciesWeightTests
    {
        private const double Eps = 1e-9;
        private static FishSpecies Fish(string id) => FishSpecies.Find(id);

        [Test]
        public void OptimalTime_Triples_VsOffTime()
        {
            // rainbow_trout optimal time = "낮" (Day). Off-time -> *0.3, on-time -> *3.0 => ratio 10.
            // Hold weather/spot/bait neutral (rainbow_trout not boosted by Rain or galaxy_lake).
            var f = Fish("rainbow_trout");
            double day = GameController.ComputeSpeciesWeight(f, "낮", Weather.Rain, BaitType.Basic, "galaxy_lake");
            double night = GameController.ComputeSpeciesWeight(f, "밤", Weather.Rain, BaitType.Basic, "galaxy_lake");
            Assert.That(day / night, Is.EqualTo(10.0).Within(Eps));
        }

        [Test]
        public void AnytimeFish_Gets1_5xTimeFactor()
        {
            // gold_crucian optimal time = "언제나" -> always *1.5 regardless of time.
            var f = Fish("gold_crucian");
            double a = GameController.ComputeSpeciesWeight(f, "밤", Weather.Mist, BaitType.Basic, "galaxy_lake");
            double b = GameController.ComputeSpeciesWeight(f, "낮", Weather.Mist, BaitType.Basic, "galaxy_lake");
            Assert.That(a, Is.EqualTo(b).Within(Eps), "anytime fish should not depend on time of day");
        }

        [Test]
        public void OptimalWeather_Quadruples_VsOffWeather()
        {
            // gold_crucian is in the CLEAR optimal-weather list. Clear -> *4.0, Mist -> *0.4 => ratio 10.
            var f = Fish("gold_crucian");
            double clear = GameController.ComputeSpeciesWeight(f, "낮", Weather.Clear, BaitType.Basic, "galaxy_lake");
            double mist = GameController.ComputeSpeciesWeight(f, "낮", Weather.Mist, BaitType.Basic, "galaxy_lake");
            Assert.That(clear / mist, Is.EqualTo(10.0).Within(Eps));
        }

        [Test]
        public void ShrimpBait_Boosts_Rare_By2_2x()
        {
            var f = Fish("rainbow_trout"); // rare
            double basic = GameController.ComputeSpeciesWeight(f, "낮", Weather.Rain, BaitType.Basic, "galaxy_lake");
            double shrimp = GameController.ComputeSpeciesWeight(f, "낮", Weather.Rain, BaitType.Shrimp, "galaxy_lake");
            Assert.That(shrimp / basic, Is.EqualTo(2.2).Within(Eps));
        }

        [Test]
        public void GoldenBait_Boosts_Mythic_By5_5x()
        {
            var f = Fish("star_whale"); // mythic
            double basic = GameController.ComputeSpeciesWeight(f, "밤", Weather.Clear, BaitType.Basic, "galaxy_lake");
            double golden = GameController.ComputeSpeciesWeight(f, "밤", Weather.Clear, BaitType.Golden, "galaxy_lake");
            Assert.That(golden / basic, Is.EqualTo(5.5).Within(Eps));
        }

        [Test]
        public void Spot_Boosts_MatchingFish_By2_5x()
        {
            // windy_valley boosts gold_crucian; galaxy_lake does not.
            var f = Fish("gold_crucian");
            double off = GameController.ComputeSpeciesWeight(f, "낮", Weather.Clear, BaitType.Basic, "galaxy_lake");
            double on = GameController.ComputeSpeciesWeight(f, "낮", Weather.Clear, BaitType.Basic, "windy_valley");
            Assert.That(on / off, Is.EqualTo(2.5).Within(Eps));
        }

        [Test]
        public void AllWeightsPositive_ForEverySpecies()
        {
            foreach (var f in FishSpecies.List)
            {
                double w = GameController.ComputeSpeciesWeight(f, "낮", Weather.Clear, BaitType.Basic, "windy_valley");
                Assert.Greater(w, 0.0, $"{f.Id} produced a non-positive weight");
            }
        }
    }
}
