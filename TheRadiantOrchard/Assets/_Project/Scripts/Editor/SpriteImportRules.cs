using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Applies the right import preset to art dropped into the sprite folders (ART_TRACKER 3): ground objects and
    /// characters get a bottom-centre pivot, icons/UI/VFX a centred one; all PPU 100, no mipmaps, uncompressed
    /// source (the atlases apply ASTC 6×6 or 4×4).
    /// Only on the first import, so later hand-tweaks (9-slice borders, a custom pivot) survive re-imports;
    /// Radiant Orchard > Art > Reapply Import Preset resets on purpose. Placeholders are left alone.
    /// </summary>
    public sealed class SpriteImportRules : AssetPostprocessor
    {
        public const string PresetFolder = "Assets/_Project/Settings/Presets";
        public const string GroundPreset = PresetFolder + "/TI_Sprite_Ground.preset";
        public const string CharacterPreset = PresetFolder + "/TI_Sprite_Character.preset";
        public const string CenteredPreset = PresetFolder + "/TI_Sprite_Centered.preset";

        private const string SpritesRoot = "Assets/_Project/Art/Sprites/";
        private const string VfxRoot = "Assets/_Project/Art/VFX/";

        /// <summary>The preset for a texture at this path, or null when the folder has no rule.</summary>
        public static string PresetPathFor(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            string path = assetPath.Replace('\\', '/');
            if (path.StartsWith(VfxRoot)) return CenteredPreset;
            if (!path.StartsWith(SpritesRoot)) return null;

            string rest = path.Substring(SpritesRoot.Length);
            int slash = rest.IndexOf('/');
            if (slash < 0) return null;
            switch (rest.Substring(0, slash))
            {
                case "Environment":
                case "Landmarks":
                case "Plots":
                case "Trees":
                    return GroundPreset;
                case "Characters":
                    return CharacterPreset;
                case "Icons":
                case "UI":
                    return CenteredPreset;
                default:
                    return null; // Placeholders and anything new stay as imported.
            }
        }

        /// <summary>Applies the folder's preset to this importer. False when there is no rule or the preset is missing.</summary>
        public static bool Apply(AssetImporter importer)
        {
            string presetPath = PresetPathFor(importer.assetPath);
            if (presetPath == null) return false;
            var preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);
            if (preset == null)
            {
                Debug.LogWarning($"SpriteImportRules: {presetPath} is missing; run Radiant Orchard > Setup > Build Sprite Presets and Atlases.");
                return false;
            }
            return preset.ApplyTo(importer);
        }

        private void OnPreprocessTexture()
        {
            if (assetImporter.importSettingsMissing) Apply(assetImporter);
        }

        [MenuItem("Radiant Orchard/Art/Reapply Import Preset")]
        private static void ReapplyToSelection()
        {
            int count = 0;
            foreach (Object o in Selection.objects)
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(o)) as TextureImporter;
                if (importer == null || !Apply(importer)) continue;
                importer.SaveAndReimport();
                count++;
            }
            Debug.Log($"SpriteImportRules: preset reapplied to {count} texture(s).");
        }

        [MenuItem("Radiant Orchard/Art/Reapply Import Preset", true)]
        private static bool HasTextureSelected()
        {
            foreach (Object o in Selection.objects)
                if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(o)) is TextureImporter) return true;
            return false;
        }
    }
}
