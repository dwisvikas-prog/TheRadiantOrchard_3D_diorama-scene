using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Presentation;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>
    /// The farm layout data (layout v2: four areas around a centre square, CLAUDE.md 5.1, 5.3, 5.7) is consistent with
    /// the spec and with itself.
    /// </summary>
    public class FarmDefinitionTests
    {
        private const int Left = 0, Right = 1, Bottom = 2, Centre = 3, Tree = 4;

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

        private static Vector2Int CellAt(Vector2 point) => new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));

        [Test]
        public void CountsMatchTheSpec()
        {
            var config = Config();
            var farm = config.Farm;
            Assert.AreEqual(config.Balance.TotalPlots, farm.PlotCells.Count, "One plot position per plot.");
            Assert.AreEqual(config.Balance.MaxVisitorsOnPath, farm.WaitingSpotCells.Count, "One waiting spot per path visitor.");
            Assert.AreEqual(BalanceConfig.StageCount, farm.Zones.Count, "One zone per vibrancy stage.");
        }

        [Test]
        public void PlotsAreInsideTheFarmOffThePathsAndApart()
        {
            var farm = Config().Farm;
            var rects = Enumerable.Range(0, farm.PlotCells.Count).Select(farm.PlotRect).ToList();
            for (int i = 0; i < rects.Count; i++)
            {
                Assert.IsTrue(Inside(farm.FarmCells, rects[i]), $"Plot {i + 1} is outside the farm.");
                foreach (Vector2Int cell in rects[i].allPositionsWithin)
                    Assert.IsFalse(farm.IsPathCell(cell), $"Plot {i + 1} covers the path at {cell}.");
                // A one-cell gap all round, so neighbouring fences never touch.
                var grown = new RectInt(rects[i].xMin - 1, rects[i].yMin - 1, rects[i].width + 2, rects[i].height + 2);
                for (int j = i + 1; j < rects.Count; j++)
                    Assert.IsFalse(Overlaps(grown, rects[j]), $"Plots {i + 1} and {j + 1} touch or overlap.");
            }
        }

        [Test]
        public void ZonesCoverTheFarmExactlyOnce()
        {
            var farm = Config().Farm;
            var seen = new HashSet<Vector2Int>();
            foreach (ZoneArea zone in farm.Zones)
            {
                foreach (RectInt rect in zone.Rects)
                    Assert.IsTrue(Inside(farm.FarmCells, rect), $"{zone.Name}: {rect} is outside the farm.");
                foreach (Vector2Int cell in zone.Cells())
                    Assert.IsTrue(seen.Add(cell), $"Cell {cell} is in two zones.");
            }
            Assert.AreEqual(farm.FarmCells.width * farm.FarmCells.height, seen.Count, "Zones leave part of the farm uncovered.");
        }

        [Test]
        public void ThreePlotsPerAreaEachInTheZoneThatOpensIt()
        {
            var config = Config();
            var farm = config.Farm;
            // Agreed 31 Oct: plots 1-3 left (start), 4-5 right (stage 2), 6-7 bottom (stage 3), 8 right and 9 bottom (stage 4).
            int[] expected = { Left, Left, Left, Right, Right, Bottom, Bottom, Right, Bottom };
            for (int i = 0; i < farm.PlotCells.Count; i++)
            {
                foreach (Vector2Int cell in farm.PlotRect(i).allPositionsWithin)
                    Assert.AreEqual(expected[i], farm.ZoneIndexAtCell(cell), $"Plot {i + 1} at {cell}.");

                int opensAt = 0;
                while (config.Balance.PlotsOpenAt(opensAt) <= i) opensAt++;
                Assert.LessOrEqual(expected[i] + 1, Mathf.Max(1, opensAt), $"Plot {i + 1}'s area is in colour by the time it opens.");
            }
        }

        [Test]
        public void LandmarksSitInTheirAreasOffPlotsAndPaths()
        {
            var farm = Config().Farm;
            Assert.AreEqual(Tree, farm.ZoneIndexAtCell(farm.WisdomTreeCell), "Wisdom Tree in the top area (colours last: the bloom).");
            Assert.AreEqual(Left, farm.ZoneIndexAtCell(farm.WellCell), "Stone Well in the left area.");
            Assert.AreEqual(Centre, farm.ZoneIndexAtCell(farm.BenchCell), "Bench in the centre square.");
            foreach (Vector2Int spot in farm.WaitingSpotCells)
                Assert.AreEqual(Centre, farm.ZoneIndexAtCell(spot), $"Waiting spot {spot} in the centre square.");
            CollectionAssert.AllItemsAreUnique(farm.WaitingSpotCells.ToList());

            var cells = new List<Vector2Int> { farm.WisdomTreeCell, farm.WellCell, farm.BenchCell };
            cells.AddRange(farm.WaitingSpotCells);
            CollectionAssert.AllItemsAreUnique(cells);
            foreach (Vector2Int cell in cells)
            {
                Assert.AreEqual(-1, farm.PlotIndexAtCell(cell), $"{cell} sits on a plot.");
                Assert.IsFalse(farm.IsPathCell(cell), $"{cell} sits on a path.");
            }
        }

        [Test]
        public void VisitorsWalkFromTheJungleAlongThePathsAndBack()
        {
            var farm = Config().Farm;
            Assert.IsFalse(farm.FarmCells.Contains(CellAt(farm.SpawnPoint)), "Visitors arrive from the jungle, outside the farm.");
            Assert.IsFalse(farm.FarmCells.Contains(CellAt(farm.ExitPoint)), "Visitors leave into the jungle.");
            Assert.AreEqual(Centre, farm.ZoneIndexAtCell(CellAt(farm.EntryRoute[farm.EntryRoute.Count - 1])), "The way in ends at the centre square.");
            Assert.AreEqual(Centre, farm.ZoneIndexAtCell(CellAt(farm.ExitRoute[0])), "The way out starts at the centre square.");

            foreach (IReadOnlyList<Vector2> route in new[] { farm.EntryRoute, farm.ExitRoute })
            {
                for (int i = 1; i < route.Count; i++)
                {
                    for (float t = 0f; t <= 1f; t += 0.05f)
                    {
                        Vector2Int cell = CellAt(Vector2.Lerp(route[i - 1], route[i], t));
                        Assert.IsTrue(farm.IsPathCell(cell) || farm.ZoneIndexAtCell(cell) == Centre, $"Route leaves the path at {cell}.");
                    }
                }
            }
        }

        [Test]
        public void PlotIndexAtCellFindsEveryPlotCellAndNothingElse()
        {
            var farm = Config().Farm;
            for (int i = 0; i < farm.PlotCells.Count; i++)
            {
                RectInt r = farm.PlotRect(i);
                Assert.AreEqual(i, farm.PlotIndexAtCell(r.position), $"Plot {i + 1}: first cell.");
                Assert.AreEqual(i, farm.PlotIndexAtCell(r.max - Vector2Int.one), $"Plot {i + 1}: last cell.");
            }
            Assert.AreEqual(-1, farm.PlotIndexAtCell(farm.WellCell), "The well is not a plot.");
            Assert.AreEqual(-1, farm.PlotIndexAtCell(farm.FarmCells.max + Vector2Int.one), "Outside the farm is not a plot.");
        }

        [Test]
        public void RouteMathWalksEvenlyAlongTheWholeLength()
        {
            var points = new[] { new Vector2(0f, 0f), new Vector2(3f, 0f), new Vector2(3f, 1f) };
            Assert.AreEqual(new Vector2(0f, 0f), RouteMath.PointAt(points, 3, 0f));
            Assert.AreEqual(new Vector2(2f, 0f), RouteMath.PointAt(points, 3, 0.5f), "Halfway along 4 units is 2 along the first leg.");
            Assert.AreEqual(new Vector2(3f, 0.5f), RouteMath.PointAt(points, 3, 0.875f));
            Assert.AreEqual(new Vector2(3f, 1f), RouteMath.PointAt(points, 3, 1.5f), "Clamped at the end.");
            Assert.AreEqual(new Vector2(3f, 0f), RouteMath.PointAt(points, 2, 2f), "Only the first count points.");
            Assert.AreEqual(points[0], RouteMath.PointAt(points, 1, 0.7f));
            Assert.AreEqual(Vector2.zero, RouteMath.PointAt(null, 0, 0.5f));
        }
    }
}
