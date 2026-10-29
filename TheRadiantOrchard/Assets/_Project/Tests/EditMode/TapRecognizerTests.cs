using NUnit.Framework;
using RadiantOrchard.Input;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>Tap rules behind TapDetector: what counts as a tap on the farm.</summary>
    public class TapRecognizerTests
    {
        private static readonly Vector2 At = new Vector2(500f, 300f);

        private static TapRecognizer Recognizer() => new TapRecognizer { MaxSeconds = 0.3f, MaxMovePixels = 10f };

        [Test]
        public void PressAndReleaseInTheSameFrameIsATap()
        {
            // Touchpad tap-to-click delivers down and up in one input update.
            Assert.IsTrue(Recognizer().Step(true, true, false, At, 1f, false));
        }

        [Test]
        public void QuickPressThenReleaseIsATap()
        {
            var r = Recognizer();
            Assert.IsFalse(r.Step(true, false, true, At, 1f, false));
            Assert.IsFalse(r.Step(false, false, true, At + new Vector2(3f, 0f), 1.1f, false));
            Assert.IsTrue(r.Step(false, true, false, At + new Vector2(3f, 0f), 1.2f, false));
        }

        [Test]
        public void HeldTooLongIsNotATap()
        {
            var r = Recognizer();
            r.Step(true, false, true, At, 1f, false);
            Assert.IsFalse(r.Step(false, true, false, At, 1.31f, false));
        }

        [Test]
        public void DraggingPastTheThresholdIsNotATapEvenIfItComesBack()
        {
            var r = Recognizer();
            r.Step(true, false, true, At, 1f, false);
            r.Step(false, false, true, At + new Vector2(0f, 12f), 1.05f, false);
            Assert.IsFalse(r.Step(false, true, false, At, 1.1f, false), "A camera drag that returns to its start must not plant.");
        }

        [Test]
        public void PressStartedOnTheUiIsNotATap()
        {
            var r = Recognizer();
            r.Step(true, false, true, At, 1f, true);
            Assert.IsFalse(r.Step(false, true, false, At, 1.1f, false));
        }

        [Test]
        public void CancelledPressIsNotATapAndTheNextOneIs()
        {
            var r = Recognizer();
            r.Step(true, false, true, At, 1f, false);
            r.Cancel();
            Assert.IsFalse(r.Step(false, true, false, At, 1.1f, false), "A pinch that lifts one finger is not a tap.");
            Assert.IsTrue(r.Step(true, true, false, At, 2f, false), "The next clean press is unaffected.");
        }

        [Test]
        public void ReleaseWithoutPressIsIgnored()
        {
            Assert.IsFalse(Recognizer().Step(false, true, false, At, 1f, false));
        }
    }
}
