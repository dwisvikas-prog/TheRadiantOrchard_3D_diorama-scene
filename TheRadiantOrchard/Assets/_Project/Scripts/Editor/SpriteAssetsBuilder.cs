using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Creates the sprite import presets used by SpriteImportRules and the five Sprite Atlases (ART_TRACKER 3,
    /// ENGINEERING_GUIDE 7). Atlases pack whole folders, so new art is picked up without editing them. Source sprites
    /// import uncompressed and the atlases carry the ASTC compression, so art is compressed once, not twice; builds
    /// ship the atlas textures. Re-running rebuilds everything from the values here.
    /// </summary>
    public static class SpriteAssetsBuilder
    {
        public const string AtlasFolder = "Assets/_Project/Art/Atlases";
        private const string SpritesRoot = "Assets/_Project/Art/Sprites/";
        private const string VfxFolder = "Assets/_Project/Art/VFX";
        private const string TempTexture = "Assets/_Project/Settings/Presets/__preset_source.png";
        private const int MaxSize = 2048;
        // ART_TRACKER 3: 4 px transparent margin around each sprite.
        private const int AtlasPadding = 4;

        private static readonly string[] Platforms = { "Android", "iPhone" };

        private struct AtlasSpec
        {
            public string Name;
            public string[] Folders;
            public TextureImporterFormat Format;
            public bool TightPacking;
        }

        public static readonly string[] AtlasNames = { "ATL_Environment", "ATL_Trees", "ATL_Characters", "ATL_UI", "ATL_VFX" };

        private static readonly AtlasSpec[] Atlases =
        {
            new AtlasSpec { Name = "ATL_Environment", Folders = new[] { SpritesRoot + "Environment", SpritesRoot + "Landmarks", SpritesRoot + "Plots" }, Format = TextureImporterFormat.ASTC_6x6, TightPacking = true },
            new AtlasSpec { Name = "ATL_Trees", Folders = new[] { SpritesRoot + "Trees" }, Format = TextureImporterFormat.ASTC_6x6, TightPacking = true },
            new AtlasSpec { Name = "ATL_Characters", Folders = new[] { SpritesRoot + "Characters" }, Format = TextureImporterFormat.ASTC_4x4, TightPacking = true },
            // Tight packing would cut into 9-slice panels and buttons.
            new AtlasSpec { Name = "ATL_UI", Folders = new[] { SpritesRoot + "UI", SpritesRoot + "Icons" }, Format = TextureImporterFormat.ASTC_4x4, TightPacking = false },
            new AtlasSpec { Name = "ATL_VFX", Folders = new[] { VfxFolder }, Format = TextureImporterFormat.ASTC_4x4, TightPacking = true },
        };

        [MenuItem("Radiant Orchard/Setup/Build Sprite Presets and Atlases")]
        public static void Build()
        {
            EnsureFolder(SpriteImportRules.PresetFolder);
            EnsureFolder(AtlasFolder);
            BuildPresets();
            foreach (AtlasSpec spec in Atlases) BuildAtlas(spec);
            AssetDatabase.SaveAssets();
            Debug.Log($"SpriteAssetsBuilder: 3 import presets in {SpriteImportRules.PresetFolder}, {Atlases.Length} atlases in {AtlasFolder}.");
        }

        private static void BuildPresets()
        {
            // A preset is captured from a real importer, so configure one on a throwaway texture.
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            File.WriteAllBytes(TempTexture, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(TempTexture);
            try
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(TempTexture);
                SavePreset(importer, SpriteAlignment.BottomCenter, SpriteMeshType.Tight, SpriteImportRules.GroundPreset);
                SavePreset(importer, SpriteAlignment.BottomCenter, SpriteMeshType.Tight, SpriteImportRules.CharacterPreset);
                SavePreset(importer, SpriteAlignment.Center, SpriteMeshType.FullRect, SpriteImportRules.CenteredPreset);
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempTexture);
            }
        }

        private static void SavePreset(TextureImporter importer, SpriteAlignment pivot, SpriteMeshType mesh, string path)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)pivot;
            settings.spriteMeshType = mesh;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            foreach (string platform in Platforms)
                importer.ClearPlatformTextureSettings(platform);

            var preset = new Preset(importer);
            if (AssetDatabase.LoadAssetAtPath<Preset>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(preset, path);
        }

        private static void BuildAtlas(AtlasSpec spec)
        {
            string path = $"{AtlasFolder}/{spec.Name}.spriteatlasv2";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

            var packables = new List<Object>();
            foreach (string folder in spec.Folders)
            {
                EnsureFolder(folder);
                packables.Add(AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder));
            }

            var atlas = new SpriteAtlasAsset();
            atlas.Add(packables.ToArray());
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path);

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            importer.includeInBuild = true;
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                blockOffset = 1,
                padding = AtlasPadding,
                enableRotation = false,
                enableTightPacking = spec.TightPacking,
                enableAlphaDilation = true,
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear,
            };
            foreach (string platform in Platforms)
            {
                importer.SetPlatformSettings(new TextureImporterPlatformSettings
                {
                    name = platform,
                    overridden = true,
                    maxTextureSize = MaxSize,
                    format = spec.Format,
                    compressionQuality = (int)TextureCompressionQuality.Normal,
                });
            }
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
