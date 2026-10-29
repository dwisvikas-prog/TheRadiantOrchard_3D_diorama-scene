using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RadiantOrchard.Core;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>The farm layout data is consistent with the spec and with itself.</summary>
    public class FarmDefinitionTests
    {
        private static GameConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            Assert.IsNotNull(config, "No GameConfig asset.");
            Assert.IsNotNull(config.Farm, "GameConfig has no FarmDefinition.");
            return config;
        }

        private static bool Overlaps(RectInt a, RectInt b) =>
            a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;

        private static bool Inside(RectInt outer, RectInt inner) =>
            inner.xMin >= outer.xMin && inner.yMin >= outer.yMin && inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;

        [Test]
        public void CountsMatchTheSpec()
        {
            var config = Config();
            var farm = config.Farm;
            Assert.AreEqual(config.Balance.TotalPlots, farm.PlotCells.Count, "One plot position per plot.");
            Assert.AreEqual(config.Balance.MaxVisitorsOnPath, farm.WaitingSpotCells.Count, "One waiting spot per path visitor.");
            Assert.AreEqual(BalanceConfig.StageCount, farm.ZoneCells.Count, "One zone per vibrancy stage.");
        }

        [Test]
        public void PlotsAreInsideTheFarmAndDoNotOverlap()
        {
            var farm = Config().Farm;
            var rects = Enumerable.Range(0, farm.PlotCells.Count).Select(farm.PlotRect).ToList();
            for (int i = 0; i < rects.Count; i++)
            {
                Assert.IsTrue(Inside(farm.FarmCells, rects[i]), $"Plot {i + 1} is outside the farm.");
                for (int j = i + 1; j < rects.Count; j++)
                    Assert.IsFalse(Overlaps(rects[i], rects[j]), $"Plots {i + 1} and {j + 1} overlap.");
            }
        }

        [Test]
        public void LandmarksAndPathAreInPlace()
        {
            var farm = Config().Farm;
            var landmarks = new[] { farm.WisdomTreeCell, farm.WellCell, farm.BenchCell };
            var plots = Enumerable.Range(0, farm.PlotCells.Count).Select(farm.PlotRect).ToList();

            foreach (var cell in landmarks.Concat(farm.WaitingSpotCells))
            {
                Assert.IsTrue(farm.FarmCells.Contains(cell), $"{cell} is outside the farm.");
                Assert.IsFalse(plots.Any(p => p.Contains(cell)), $"{cell} sits on a plot.");
            }
            Assert.IsFalse(farm.FarmCells.Contains(farm.SpawnCell), "Visitors should arrive from the jungle, outside the farm.");
            CollectionAssert.AllItemsAreUnique(farm.WaitingSpotCells.ToList());
        }

        [Test]
        public void ZonesCoverTheFarmExactlyOnce()
        {
            var farm = Config().Farm;
            var seen = new HashSet<Vector2Int>();
            foreach (RectInt zone in farm.ZoneCells)
            {
                Assert.IsTrue(Inside(farm.FarmCells, zone), $"Zone {zone} is outside the farm.");
                foreach (Vector2Int cell in zone.allPositionsWithin)
                    Assert.IsTrue(seen.Add(cell), $"Cell {cell} is in two zones.");
            }
            Assert.AreEqual(farm.FarmCells.width * farm.FarmCells.height, seen.Count, "Zones leave part of the farm uncovered.");
        }

        [Test]
        public void StartingPlotsAreInZoneOne()
        {
            var config = Config();
            var farm = config.Farm;
            for (int i = 0; i < config.Balance.PlotsOpenAt(0); i++)
                Assert.IsTrue(Inside(farm.ZoneCells[0], farm.PlotRect(i)), $"Starting plot {i + 1} should be in zone 1, which colours first.");
        }
    }
}
