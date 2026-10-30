using NUnit.Framework;
using RadiantOrchard.Input;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>
    /// Gesture rules behind GestureRecognizer (ENGINEERING_GUIDE 5.7): tap, double tap, long press, swipe, and the
    /// pan / swipe split that keeps camera drags from ever harvesting. Distances in pixels, times in seconds.
    /// </summary>
    public class GestureRulesTests
    {
        private const float Frame = 1f / 60f;
        private static readonly Vector2 At = new Vector2(500f, 300f);
        private GestureRules _r;

        [SetUp]
        public void SetUp()
        {
            _r = new GestureRules
            {
                TapMaxSeconds = 0.3f,
                LongPressSeconds = 0.55f,
                DoubleTapWindowSeconds = 0.3f,
                SwipeMaxSeconds = 0.4f,
                DragThresholdPixels = 10f,
                DoubleTapMaxDistancePixels = 50f,
                SwipeMinSpeedPixels = 1500f,
                SwipeMinDistancePixels = 80f,
            };
        }

        private GestureEvent Down(Vector2 p, float t, bool overUi = false) => _r.Step(true, false, true, p, t, overUi);
        private GestureEvent Hold(Vector2 p, float t) => _r.Step(false, false, true, p, t, false);
        private GestureEvent Up(Vector2 p, float t) => _r.Step(false, true, false, p, t, false);
        private GestureEvent Click(Vector2 p, float down, float up)
        {
            Down(p, down);
            return Up(p, up);
        }

        // ---- Taps ----

        [Test]
        public void PressAndReleaseInTheSameFrameIsATap()
        {
            // Touchpad tap-to-click delivers down and up in one input update.
            Assert.AreEqual(GestureEvent.Tap, _r.Step(true, true, false, At, 1f, false));
            Assert.AreEqual(At, _r.EventPosition);
        }

        [Test]
        public void QuickPressThenReleaseWithJitterIsATap()
        {
            Down(At, 1f);
            Assert.AreEqual(GestureEvent.None, Hold(At + new Vector2(3f, 0f), 1.1f));
            Assert.AreEqual(GestureEvent.Tap, Up(At + new Vector2(3f, 0f), 1.2f));
        }

        [Test]
        public void PressBetweenTapAndLongPressIsNothing()
        {
            Down(At, 1f);
            Hold(At, 1.4f);
            Assert.AreEqual(GestureEvent.None, Up(At, 1.45f));
        }

        [Test]
        public void PressStartedOnTheUiIsNothing()
        {
            Down(At, 1f, overUi: true);
            Assert.AreEqual(GestureEvent.None, Hold(At, 2f), "No long press either.");
            Assert.AreEqual(GestureEvent.None, Up(At, 2.1f));
        }

        [Test]
        public void ReleaseWithoutPressIsIgnored()
        {
            Assert.AreEqual(GestureEvent.None, Up(At, 1f));
        }

        [Test]
        public void ALostReleaseDropsThePress()
        {
            Down(At, 1f);
            Assert.AreEqual(GestureEvent.None, _r.Step(false, false, false, At, 1.1f, false));
            Assert.AreEqual(PointerIntent.None, _r.Intent);
        }

        [Test]
        public void CancelledPressIsNothingAndTheNextOneWorks()
        {
            Down(At, 1f);
            _r.Cancel();
            Assert.AreEqual(GestureEvent.None, Up(At, 1.1f), "A pinch that lifts one finger is not a tap.");
            Assert.AreEqual(GestureEvent.Tap, Click(At, 2f, 2.05f));
        }

        // ---- Double tap ----

        [Test]
        public void TwoQuickTapsMakeADoubleTapAndAThirdStartsOver()
        {
            Assert.AreEqual(GestureEvent.Tap, Click(At, 1f, 1.08f));
            Assert.AreEqual(GestureEvent.DoubleTap, Click(At + new Vector2(10f, 5f), 1.25f, 1.3f));
            Assert.AreEqual(GestureEvent.Tap, Click(At, 1.45f, 1.5f), "A third tap does not chain into another double tap.");
        }

        [Test]
        public void SecondTapTooLateIsJustATap()
        {
            Click(At, 1f, 1.08f);
            Assert.AreEqual(GestureEvent.Tap, Click(At, 1.08f + 0.35f, 1.5f));
        }

        [Test]
        public void SecondTapTooFarAwayIsJustATap()
        {
            Click(At, 1f, 1.08f);
            Assert.AreEqual(GestureEvent.Tap, Click(At + new Vector2(100f, 0f), 1.2f, 1.25f));
        }

        [Test]
        public void SameFrameClicksStillMakeADoubleTap()
        {
            Assert.AreEqual(GestureEvent.Tap, _r.Step(true, true, false, At, 1f, false));
            Assert.AreEqual(GestureEvent.DoubleTap, _r.Step(true, true, false, At, 1.15f, false));
        }

        // ---- Long press ----

        [Test]
        public void LongPressFiresWhileHeldOnlyOnceAtThePressPoint()
        {
            Down(At, 1f);
            Assert.AreEqual(GestureEvent.None, Hold(At + new Vector2(2f, 0f), 1.3f));
            Assert.AreEqual(GestureEvent.LongPress, Hold(At + new Vector2(2f, 0f), 1.56f));
            Assert.AreEqual(At, _r.EventPosition);
            Assert.AreEqual(GestureEvent.None, Hold(At, 1.8f));
            Assert.AreEqual(GestureEvent.None, Up(At, 1.9f), "Letting go after a long press is not also a tap.");
        }

        // ---- Pan versus swipe ----

        [Test]
        public void SlowDragIsAPanAndNeverAGesture()
        {
            Down(At, 1f);
            float t = 1f;
            Vector2 p = At;
            for (int i = 0; i < 6; i++)
            {
                t += 0.05f;
                p += new Vector2(5f, 0f); // 100 px/s
                Assert.AreEqual(GestureEvent.None, Hold(p, t));
            }
            Assert.IsTrue(_r.IsPanning);
            Assert.AreEqual(GestureEvent.None, Hold(p + new Vector2(200f, 0f), t + Frame), "Speeding up mid-pan stays a pan.");
            Assert.AreEqual(GestureEvent.None, Hold(At, 2f), "No long press while panning.");
            Assert.AreEqual(GestureEvent.None, Up(At, 2.05f), "A drag that comes back to its start is not a tap.");
        }

        [Test]
        public void FastDragIsASwipeAndTheCameraStaysStill()
        {
            Down(new Vector2(100f, 300f), 1f);
            Hold(new Vector2(160f, 300f), 1f + Frame);
            Assert.AreEqual(PointerIntent.Swipe, _r.Intent);
            Assert.IsFalse(_r.IsPanning);
            Hold(new Vector2(260f, 300f), 1f + 2 * Frame);
            Assert.AreEqual(GestureEvent.Swipe, Up(new Vector2(320f, 300f), 1f + 3 * Frame));

            SwipePath path = _r.Path;
            Assert.AreEqual(4, path.Count);
            Assert.AreEqual(new Vector2(100f, 300f), path[0]);
            Assert.AreEqual(new Vector2(320f, 300f), path[path.Count - 1]);
        }

        [Test]
        public void RestingThenFlickingIsStillASwipe()
        {
            Down(At, 1f);
            Hold(At, 1.2f);
            Hold(At + new Vector2(60f, 0f), 1.2f + Frame);
            Assert.AreEqual(PointerIntent.Swipe, _r.Intent);
            Assert.AreEqual(GestureEvent.Swipe, Up(At + new Vector2(150f, 0f), 1.2f + 2 * Frame));
        }

        [Test]
        public void ShortFlickIsNotASwipe()
        {
            Down(At, 1f);
            Hold(At + new Vector2(30f, 0f), 1f + Frame);
            Assert.AreEqual(GestureEvent.None, Up(At + new Vector2(40f, 0f), 1f + 2 * Frame));
        }

        [Test]
        public void FlickThatKeepsDraggingBecomesAPan()
        {
            Down(At, 1f);
            Hold(At + new Vector2(60f, 0f), 1f + Frame);
            Assert.AreEqual(PointerIntent.Swipe, _r.Intent);
            Hold(At + new Vector2(150f, 0f), 1.45f);
            Assert.IsTrue(_r.IsPanning);
            Assert.AreEqual(GestureEvent.None, Up(At + new Vector2(160f, 0f), 1.5f));
        }

        [Test]
        public void FlickCrossingTheThresholdOnTheReleaseFrameIsASwipe()
        {
            Down(At, 1f);
            Assert.AreEqual(GestureEvent.Swipe, Up(At + new Vector2(100f, 0f), 1f + Frame));
        }

        // ---- Swipe path sampling ----

        [Test]
        public void SwipeSegmentsAreSplitIntoStepsNoLongerThanTheSpacing()
        {
            Assert.AreEqual(5, SwipePath.Steps(Vector2.zero, new Vector2(100f, 0f), 20f));
            Assert.AreEqual(6, SwipePath.Steps(Vector2.zero, new Vector2(101f, 0f), 20f));
            Assert.AreEqual(1, SwipePath.Steps(At, At, 20f), "A still point is still tested once.");
            Assert.AreEqual(1, SwipePath.Steps(Vector2.zero, new Vector2(100f, 0f), 0f));
        }
    }
}
