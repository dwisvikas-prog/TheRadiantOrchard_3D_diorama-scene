using NUnit.Framework;
using RadiantOrchard.Presentation;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>The tonic's flight curve: exact ends, the arc's height, the sideways nudge, any direction.</summary>
    public class TonicArcTests
    {
        private const float Eps = 1e-4f;

        private static void AreClose(Vector2 expected, Vector2 actual, string message = null) =>
            Assert.Less(Vector2.Distance(expected, actual), Eps, $"{message} expected {expected}, was {actual}");

        private static readonly Vector2[] Ends =
        {
            new Vector2(6f, 0f),   // right
            new Vector2(-6f, 0f),  // left
            new Vector2(0f, 5f),   // up
            new Vector2(0f, -5f),  // down
            new Vector2(-4f, 3f),  // diagonal
        };

        [Test]
        public void StartsAndEndsExactlyOnItsPoints()
        {
            var start = new Vector2(1f, 2f);
            foreach (Vector2 offset in Ends)
            {
                Vector2 end = start + offset;
                Vector2 c = TonicArc.ControlPoint(start, end, 2f, 0.3f);
                AreClose(start, TonicArc.Evaluate(start, c, end, 0f), "t = 0");
                AreClose(end, TonicArc.Evaluate(start, c, end, 1f), "t = 1");
                AreClose(end, TonicArc.Evaluate(start, c, end, 1.5f), "t past the end clamps");
            }
        }

        [Test]
        public void HalfwayPointIsArcHeightAboveTheMidpoint()
        {
            var start = new Vector2(-3f, -1f);
            foreach (Vector2 offset in Ends)
            {
                Vector2 end = start + offset;
                Vector2 c = TonicArc.ControlPoint(start, end, 2f, 0f);
                AreClose((start + end) * 0.5f + new Vector2(0f, 2f), TonicArc.Evaluate(start, c, end, 0.5f), $"towards {offset}");
            }
        }

        [Test]
        public void SidewaysNudgeIsAcrossTheFlightLineAndExactlyThatFar()
        {
            var start = Vector2.zero;
            var end = new Vector2(8f, 0f);
            Vector2 c = TonicArc.ControlPoint(start, end, 0f, 0.5f);
            Vector2 half = TonicArc.Evaluate(start, c, end, 0.5f);
            AreClose(new Vector2(4f, 0.5f), half, "a flight to the right is nudged straight up or down");

            Vector2 straightUp = TonicArc.Evaluate(Vector2.zero, TonicArc.ControlPoint(Vector2.zero, new Vector2(0f, 8f), 0f, 0.5f), new Vector2(0f, 8f), 0.5f);
            Assert.AreEqual(0.5f, Mathf.Abs(straightUp.x), Eps, "a flight straight up is nudged sideways");
        }

        [Test]
        public void ArcRisesAboveTheStraightLineAllTheWay()
        {
            var start = new Vector2(0f, 0f);
            var end = new Vector2(10f, 0f);
            Vector2 c = TonicArc.ControlPoint(start, end, 2f, 0f);
            for (int i = 1; i < 10; i++)
                Assert.Greater(TonicArc.Evaluate(start, c, end, i / 10f).y, 0f, $"t = {i / 10f}");
        }

        [Test]
        public void SamePointFlightDoesNotBreak()
        {
            var p = new Vector2(2f, 2f);
            Vector2 c = TonicArc.ControlPoint(p, p, 1f, 0.5f);
            Assert.IsFalse(float.IsNaN(c.x) || float.IsNaN(c.y));
            AreClose(p, TonicArc.Evaluate(p, c, p, 1f));
        }
    }
}
