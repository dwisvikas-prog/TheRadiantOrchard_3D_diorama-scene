using NUnit.Framework;
using RadiantOrchard.UI;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>Safe-area anchors (ENGINEERING_GUIDE 5.8): UI stays clear of notches and camera cutouts.</summary>
    public class SafeAreaTests
    {
        private const float Eps = 1e-5f;

        [Test]
        public void FullScreenSafeAreaUsesTheWholeScreen()
        {
            SafeAreaMath.Anchors(new Rect(0f, 0f, 2400f, 1080f), 2400f, 1080f, out Vector2 min, out Vector2 max);
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);
        }

        [Test]
        public void LandscapeCutoutOnTheLeftPushesTheUiRight()
        {
            // Pixel 7 emulator in landscape: a 136 px camera cutout on the left.
            SafeAreaMath.Anchors(new Rect(136f, 0f, 2264f, 1080f), 2400f, 1080f, out Vector2 min, out Vector2 max);
            Assert.AreEqual(136f / 2400f, min.x, Eps);
            Assert.AreEqual(0f, min.y, Eps);
            Assert.AreEqual(1f, max.x, Eps);
            Assert.AreEqual(1f, max.y, Eps);
        }

        [Test]
        public void InsetsOnEverySideAreRespected()
        {
            SafeAreaMath.Anchors(new Rect(100f, 50f, 1800f, 960f), 2000f, 1080f, out Vector2 min, out Vector2 max);
            Assert.AreEqual(0.05f, min.x, Eps);
            Assert.AreEqual(50f / 1080f, min.y, Eps);
            Assert.AreEqual(0.95f, max.x, Eps);
            Assert.AreEqual(1010f / 1080f, max.y, Eps);
        }

        [Test]
        public void MissingScreenSizeFallsBackToTheFullScreen()
        {
            SafeAreaMath.Anchors(new Rect(0f, 0f, 0f, 0f), 0f, 0f, out Vector2 min, out Vector2 max);
            Assert.AreEqual(Vector2.zero, min);
            Assert.AreEqual(Vector2.one, max);
        }
    }
}
