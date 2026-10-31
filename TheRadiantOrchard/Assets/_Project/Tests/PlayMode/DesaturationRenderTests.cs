using System.Collections;
using NUnit.Framework;
using RadiantOrchard.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// The desaturation shader on the GPU (ENGINEERING_GUIDE 8): a red sprite is rendered into a texture and its
    /// pixels read back. Saturation 0 = grey at the colour's luminance, 1 = the colour itself; darken dims the grey;
    /// a half-done sweep leaves the bottom coloured and the top grey.
    /// </summary>
    public class DesaturationRenderTests
    {
        private const int Size = 32;
        // Far from anything else the test scene might hold.
        private static readonly Vector3 Centre = new Vector3(5000f, 5000f, 0f);
        // Linear-light luminance of pure red (Rec. 709), as in the shader.
        private const float RedLuminance = 0.2126f;

        private Camera _camera;
        private RenderTexture _target;
        private Texture2D _read;
        private Texture2D _white;
        private Material _material;
        private SpriteRenderer _sprite;
        private Desaturation _colour;

        [SetUp]
        public void SetUp()
        {
            Shader shader = Shader.Find("Radiant Orchard/Sprite Desaturate");
            Assert.IsNotNull(shader, "Shader 'Radiant Orchard/Sprite Desaturate' not found.");
            _material = new Material(shader);

            _white = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            _white.SetPixels32(pixels);
            _white.Apply();

            // A 2 × 2 world-unit square filling an orthographic camera of size 1.
            var go = new GameObject("DesaturateTestSprite");
            go.transform.position = Centre;
            _sprite = go.AddComponent<SpriteRenderer>();
            _sprite.sprite = Sprite.Create(_white, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 2f);
            _sprite.sharedMaterial = _material;
            _sprite.color = Color.red;
            _colour = go.AddComponent<Desaturation>();
            _colour.Bind(_sprite);

            // URP's render graph wants a depth buffer on a camera's target texture.
            _target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            _read = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            var camGo = new GameObject("DesaturateTestCamera");
            camGo.transform.position = Centre + new Vector3(0f, 0f, -10f);
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 1f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.targetTexture = _target;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_camera.gameObject);
            Object.Destroy(_sprite.gameObject);
            Object.Destroy(_material);
            Object.Destroy(_white);
            Object.Destroy(_read);
            _target.Release();
            Object.Destroy(_target);
        }

        // Lets the camera render a few frames into the texture (WaitForEndOfFrame never fires in batch mode), then reads it.
        private IEnumerator Capture()
        {
            for (int i = 0; i < 3; i++) yield return null;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;
            _read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            _read.Apply();
            RenderTexture.active = previous;
        }

        private Color At(float u, float v) => _read.GetPixel((int)(u * (Size - 1)), (int)(v * (Size - 1)));

        [UnityTest]
        public IEnumerator FullSaturationShowsTheSpriteColour()
        {
            _colour.SetSaturation(1f);
            yield return Capture();
            Color c = At(0.5f, 0.5f);
            Assert.AreEqual(1f, c.r, 0.02f, $"Red stays red: {c}");
            Assert.AreEqual(0f, c.g, 0.02f);
            Assert.AreEqual(0f, c.b, 0.02f);
        }

        [UnityTest]
        public IEnumerator ZeroSaturationShowsGreyAtTheColoursLuminance()
        {
            _colour.SetSaturation(0f);
            yield return Capture();
            Color c = At(0.5f, 0.5f);
            Assert.AreEqual(c.r, c.g, 0.02f, $"Grey: {c}");
            Assert.AreEqual(c.g, c.b, 0.02f);
            Assert.AreEqual(RedLuminance, c.r, 0.03f, "Red's linear luminance.");
        }

        [UnityTest]
        public IEnumerator DarkenDimsOnlyTheGrey()
        {
            _colour.SetSaturation(0f);
            _colour.SetDarken(0.5f);
            yield return Capture();
            Assert.AreEqual(RedLuminance * 0.5f, At(0.5f, 0.5f).r, 0.03f);

            _colour.SetSaturation(1f);
            yield return Capture();
            Assert.AreEqual(1f, At(0.5f, 0.5f).r, 0.02f, "Full colour is not darkened.");
        }

        [UnityTest]
        public IEnumerator AHalfDoneSweepColoursTheBottomAndLeavesTheTopGrey()
        {
            _colour.SetSaturation(0f);
            // From the sprite's bottom edge upwards, the front halfway up.
            _colour.SetSweep((Vector2)Centre + Vector2.down, Vector2.up, 1f);
            yield return Capture();

            Color bottom = At(0.5f, 0.15f), top = At(0.5f, 0.85f);
            Assert.AreEqual(1f, bottom.r, 0.02f, $"Bottom in colour: {bottom}");
            Assert.AreEqual(0f, bottom.g, 0.02f);
            Assert.AreEqual(top.r, top.g, 0.02f, $"Top still grey: {top}");
        }

        [UnityTest]
        public IEnumerator ASweepEndsInFullColour()
        {
            _colour.SetSaturation(0f);
            _colour.Sweep(_sprite.transform, Vector2.up, 2f, 0.2f);
            Assert.IsTrue(_colour.IsSweeping);
            yield return new WaitForSeconds(0.35f);
            Assert.IsFalse(_colour.IsSweeping);
            Assert.AreEqual(1f, _colour.Saturation);
            yield return Capture();
            Assert.AreEqual(1f, At(0.5f, 0.9f).r, 0.02f);
        }
    }
}
