using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEditor;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>Growth from timestamps only (ENGINEERING_GUIDE 9.1: ripe on time, offline growth, clock rollback).</summary>
    public class GrowthCalculatorTests
    {
        private const long T0 = 1_761_700_000_000;
        private const float Sapling = 0.25f;
        private FruitDefinition _strawberry; // 300 s

        [SetUp]
        public void SetUp()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            _strawberry = config.FindFruit("strawberry");
            Assert.AreEqual(300, _strawberry.GrowSeconds);
        }

        private static PlotState Planted(long plantedUtc, long lastHarvestUtc = 0) =>
            new PlotState { Unlocked = true, FruitId = "strawberry", PlantedUtc = plantedUtc, LastHarvestUtc = lastHarvestUtc };

        private static long Sec(double s) => (long)(s * 1000);

        [Test]
        public void EmptyPlotIsEmpty()
        {
            var plot = new PlotState { Unlocked = true };
            Assert.AreEqual(TreeStage.Empty, GrowthCalculator.StageAt(plot, null, T0, Sapling));
            Assert.AreEqual(0, GrowthCalculator.SecondsUntilRipe(plot, null, T0));
        }

        [Test]
        public void FirstGrowthGoesSaplingGrowingRipe()
        {
            var plot = Planted(T0);
            Assert.AreEqual(TreeStage.Sapling, GrowthCalculator.StageAt(plot, _strawberry, T0, Sapling));
            Assert.AreEqual(TreeStage.Sapling, GrowthCalculator.StageAt(plot, _strawberry, T0 + Sec(74), Sapling));
            Assert.AreEqual(TreeStage.Growing, GrowthCalculator.StageAt(plot, _strawberry, T0 + Sec(75), Sapling));
            Assert.AreEqual(TreeStage.Growing, GrowthCalculator.StageAt(plot, _strawberry, T0 + Sec(299.9), Sapling));
            Assert.AreEqual(TreeStage.Ripe, GrowthCalculator.StageAt(plot, _strawberry, T0 + Sec(300), Sapling));
        }

        [Test]
        public void TimeLeftCountsDownAndRoundsUp()
        {
            var plot = Planted(T0);
            Assert.AreEqual(300, GrowthCalculator.SecondsUntilRipe(plot, _strawberry, T0));
            Assert.AreEqual(1, GrowthCalculator.SecondsUntilRipe(plot, _strawberry, T0 + Sec(299.5)));
            Assert.AreEqual(0, GrowthCalculator.SecondsUntilRipe(plot, _strawberry, T0 + Sec(300)));
            Assert.AreEqual(0.5, GrowthCalculator.Progress(plot, _strawberry, T0 + Sec(150)), 1e-9);
        }

        [Test]
        public void HoursAwayStillRipe()
        {
            var plot = Planted(T0);
            Assert.AreEqual(TreeStage.Ripe, GrowthCalculator.StageAt(plot, _strawberry, T0 + Sec(8 * 3600), Sapling));
            Assert.AreEqual(1.0, GrowthCalculator.Progress(plot, _strawberry, T0 + Sec(8 * 3600)), 1e-9);
        }

        [Test]
        public void RegrowthAfterHarvestNeverShrinksToSapling()
        {
            long harvested = T0 + Sec(400);
            var plot = Planted(T0, harvested);
            Assert.AreEqual(TreeStage.Growing, GrowthCalculator.StageAt(plot, _strawberry, harvested, Sapling));
            Assert.AreEqual(300, GrowthCalculator.SecondsUntilRipe(plot, _strawberry, harvested), "Same timer again.");
            Assert.AreEqual(TreeStage.Ripe, GrowthCalculator.StageAt(plot, _strawberry, harvested + Sec(300), Sapling));
        }

        [Test]
        public void ClockTurnedBackCountsAsNoGrowth()
        {
            var plot = Planted(T0);
            long earlier = T0 - Sec(3600);
            Assert.AreEqual(TreeStage.Sapling, GrowthCalculator.StageAt(plot, _strawberry, earlier, Sapling));
            Assert.AreEqual(300, GrowthCalculator.SecondsUntilRipe(plot, _strawberry, earlier));
            Assert.AreEqual(0.0, GrowthCalculator.Progress(plot, _strawberry, earlier), 1e-9);
        }
    }
}
