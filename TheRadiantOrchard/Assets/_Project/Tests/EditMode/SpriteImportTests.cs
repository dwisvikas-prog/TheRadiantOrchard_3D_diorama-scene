using System.IO;
using NUnit.Framework;
using RadiantOrchard.Editor;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>Sprite import presets and atlases (ART_TRACKER 3): art dropped into a folder imports correctly.</summary>
    public class SpriteImportTests
    {
        private const string Sprites = "Assets/_Project/Art/Sprites/";
        private string _tempAsset;

        [TearDown]
        public void TearDown()
        {
            if (_tempAsset != null) AssetDatabase.DeleteAsset(_tempAsset);
            _tempAsset = null;
        }

        [TestCase("Environment/env_ground_tile_01.png", SpriteImportRules.GroundPreset)]
        [TestCase("Landmarks/lm_well.png", SpriteImportRules.GroundPreset)]
        [TestCase("Plots/plot_empty.png", SpriteImportRules.GroundPreset)]
        [TestCase("Trees/tree_lemon_ripe.png", SpriteImportRules.GroundPreset)]
        [TestCase("Characters/char_visitor_head.png", SpriteImportRules.CharacterPreset)]
        [TestCase("Icons/icon_fruit_lemon.png", SpriteImportRules.CenteredPreset)]
        [TestCase("UI/ui_panel_frame.png", SpriteImportRules.CenteredPreset)]
        public void EachArtFolderHasItsPreset(string relative, string expected)
        {
            Assert.AreEqual(expected, SpriteImportRules.PresetPathFor(Sprites + relative));
        }

        [Test]
        public void VfxAndOtherFoldersAreHandledAsAgreed()
        {
            Assert.AreEqual(SpriteImportRules.CenteredPreset, SpriteImportRules.PresetPathFor("Assets/_Project/Art/VFX/fx_burst_heart.png"));
            Assert.IsNull(SpriteImportRules.PresetPathFor(Sprites + "Placeholders/ph_block.png"), "Placeholders keep their own settings.");
            Assert.IsNull(SpriteImportRules.PresetPathFor(Sprites + "loose_file.png"));
            Assert.IsNull(SpriteImportRules.PresetPathFor("Assets/_Project/Art/Fonts/x.png"));
            Assert.IsNull(SpriteImportRules.PresetPathFor(null));
        }

        [Test]
        public void ThePresetsExist()
        {
            foreach (string path in new[] { SpriteImportRules.GroundPreset, SpriteImportRules.CharacterPreset, SpriteImportRules.CenteredPreset })
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Preset>(path), $"{path} (Radiant Orchard > Setup > Build Sprite Presets and Atlases).");
        }

        [Test]
        public void ATreeDroppedIntoTreesImportsAsAGroundSprite()
        {
            TextureImporter importer = ImportTempPng(Sprites + "Trees/zz_test_tree.png");
            AssertCommon(importer);
            Assert.AreEqual((int)SpriteAlignment.BottomCenter, Settings(importer).spriteAlignment, "Bottom-centre pivot for things on the ground.");
            Assert.AreEqual(SpriteMeshType.Tight, Settings(importer).spriteMeshType);
            AssertUncompressedSource(importer);
        }

        [Test]
        public void AUiImageDroppedIntoUiImportsCentred()
        {
            TextureImporter importer = ImportTempPng(Sprites + "UI/zz_test_ui.png");
            AssertCommon(importer);
            Assert.AreEqual((int)SpriteAlignment.Center, Settings(importer).spriteAlignment);
            Assert.AreEqual(SpriteMeshType.FullRect, Settings(importer).spriteMeshType, "Full rect keeps 9-slice borders working.");
            AssertUncompressedSource(importer);
        }

        [Test]
        public void TheFiveAtlasesExistWithFourPixelPadding()
        {
            foreach (string name in SpriteAssetsBuilder.AtlasNames)
            {
                string path = $"{SpriteAssetsBuilder.AtlasFolder}/{name}.spriteatlasv2";
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path), $"Missing {path}.");
                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
                Assert.IsTrue(importer.includeInBuild, name);
                Assert.AreEqual(4, importer.packingSettings.padding, name);
                Assert.IsFalse(importer.packingSettings.enableRotation, name);
                Assert.IsFalse(importer.textureSettings.generateMipMaps, name);
                TextureImporterPlatformSettings android = importer.GetPlatformSettings("Android");
                Assert.IsTrue(android.overridden, name);
                Assert.AreEqual(name == "ATL_Environment" || name == "ATL_Trees" ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4, android.format, name);
            }
        }

        private TextureImporter ImportTempPng(string path)
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Preset>(SpriteImportRules.PresetPathFor(path)), "Build the presets first.");
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            _tempAsset = path;
            AssetDatabase.ImportAsset(path);
            return (TextureImporter)AssetImporter.GetAtPath(path);
        }

        private static TextureImporterSettings Settings(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            return settings;
        }

        private static void AssertCommon(TextureImporter importer)
        {
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(100f, importer.spritePixelsPerUnit);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.AreEqual(2048, importer.maxTextureSize);
        }

        // The atlas compresses; a compressed source would be compressed twice.
        private static void AssertUncompressedSource(TextureImporter importer)
        {
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.IsFalse(importer.GetPlatformTextureSettings("Android").overridden);
        }
    }
}
