using NUnit.Framework;
using RadiantOrchard.Input;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>Camera clamping and zooming maths used by CameraController.</summary>
    public class CameraMathTests
    {
        private const float Aspect = 16f / 9f;
        private static readonly Rect Farm = new Rect(-24f, -14f, 48f, 28f);

        [Test]
        public void ZoomStaysInsideMinAndMax()
        {
            Assert.AreEqual(5.4f, CameraMath.ClampOrthoSize(2f, 5.4f, 13.5f, Farm, Aspect), 1e-4f);
            Assert.AreEqual(10.8f, CameraMath.ClampOrthoSize(10.8f, 5.4f, 13.5f, Farm, Aspect), 1e-4f);
            Assert.AreEqual(13.5f, CameraMath.ClampOrthoSize(30f, 5.4f, 13.5f, Farm, Aspect), 1e-4f);
        }

        [Test]
        public void ZoomOutNeverShowsPastTheFarm()
        {
            // A 30-wide farm at 16:9 can be filled up to size 30 / (2 * 1.777) = 8.4375.
            var narrow = new Rect(-15f, -14f, 30f, 28f);
            Assert.AreEqual(8.4375f, CameraMath.ClampOrthoSize(13.5f, 5.4f, 13.5f, narrow, Aspect), 1e-4f);
        }

        [Test]
        public void PositionIsKeptInsideTheBounds()
        {
            // View at size 10.8: half width 19.2, half height 10.8.
            Vector2 clamped = CameraMath.ClampPosition(new Vector2(100f, -100f), 10.8f, Aspect, Farm);
            Assert.AreEqual(24f - 19.2f, clamped.x, 1e-4f);
            Assert.AreEqual(-14f + 10.8f, clamped.y, 1e-4f);

            Vector2 inside = CameraMath.ClampPosition(new Vector2(1f, 2f), 10.8f, Aspect, Farm);
            Assert.AreEqual(new Vector2(1f, 2f), inside);
        }

        [Test]
        public void ViewLargerThanBoundsIsCentred()
        {
            var small = new Rect(10f, 10f, 4f, 4f);
            Vector2 clamped = CameraMath.ClampPosition(new Vector2(-50f, 50f), 10.8f, Aspect, small);
            Assert.AreEqual(new Vector2(12f, 12f), clamped);
        }

        [Test]
        public void ZoomingKeepsTheFocusPointInPlace()
        {
            Vector2 cam = new Vector2(0f, 0f);
            Vector2 focus = new Vector2(6f, 3f);
            Vector2 after = CameraMath.ZoomAround(cam, focus, 10f, 5f);

            // Halving the size halves the distance from the focus to the camera centre.
            Assert.AreEqual(new Vector2(3f, 1.5f), after);
            Assert.AreEqual(cam, CameraMath.ZoomAround(cam, focus, 0f, 5f), "A zero old size must not move the camera.");
        }

        [Test]
        public void ScreenToWorldMatchesTheOrthographicView()
        {
            // 1920x1080 screen, size 10.8: 1 px = 0.02 world units; screen centre = camera position.
            Vector2 cam = new Vector2(2f, -1f);
            Assert.AreEqual(0.02f, CameraMath.WorldPerPixel(10.8f, 1080f), 1e-6f);
            Assert.AreEqual(cam, CameraMath.ScreenToWorld(new Vector2(960f, 540f), cam, 10.8f, 1920f, 1080f));
            Vector2 corner = CameraMath.ScreenToWorld(new Vector2(1920f, 1080f), cam, 10.8f, 1920f, 1080f);
            Assert.AreEqual(2f + 19.2f, corner.x, 1e-4f);
            Assert.AreEqual(-1f + 10.8f, corner.y, 1e-4f);
        }
    }
}
