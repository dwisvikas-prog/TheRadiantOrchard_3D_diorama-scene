using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Rebuilds the game's TextMeshPro font assets as STATIC atlases with a fixed character set. Dynamic font assets
    /// write every newly shown character into the .asset file (even in Play mode), so they kept changing in git.
    /// Re-run after the final font is chosen or if a needed character is missing (it is then reported here).
    /// </summary>
    public static class FontAssetBuilder
    {
        private const string FontFolder = "Assets/_Project/Art/Fonts/";
        private const int SamplingPointSize = 48;
        private const int Padding = 5;
        private const int AtlasSize = 1024;

        private static readonly (string ttf, string asset)[] Fonts =
        {
            ("Fredoka-Regular.ttf", "Fredoka-Regular SDF.asset"),
            ("Fredoka-Bold.ttf", "Fredoka-Bold SDF.asset"),
        };

        [MenuItem("Radiant Orchard/Setup/Rebuild Font Assets (static)")]
        public static void Rebuild()
        {
            string characters = CharacterSet();
            TMP_FontAsset regular = null;

            foreach (var (ttf, assetName) in Fonts)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + ttf);
                if (font == null)
                {
                    Debug.LogError($"FontAssetBuilder: missing {FontFolder}{ttf}.");
                    return;
                }

                var fa = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
                fa.TryAddCharacters(characters, out string missing);
                fa.atlasPopulationMode = AtlasPopulationMode.Static;
                fa.name = System.IO.Path.GetFileNameWithoutExtension(assetName);

                string path = FontFolder + assetName;
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(fa, path);
                fa.material.name = fa.name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                foreach (Texture2D atlas in fa.atlasTextures)
                {
                    atlas.name = fa.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, fa);
                }
                EditorUtility.SetDirty(fa);

                string report = string.IsNullOrEmpty(missing) ? "none" : missing;
                Debug.Log($"FontAssetBuilder: {fa.name}: {fa.characterTable.Count} characters baked; not in the font: {report}.");
                if (regular == null) regular = fa;
            }

            var settings = TMP_Settings.instance;
            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = regular;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"FontAssetBuilder: TMP default font = {regular.name}.");
        }

        /// <summary>Printable ASCII, Latin-1 letters and punctuation, plus typographic quotes, dashes and ellipsis.</summary>
        private static string CharacterSet()
        {
            var chars = new List<char>();
            for (int c = 32; c <= 126; c++) chars.Add((char)c);
            for (int c = 160; c <= 255; c++) chars.Add((char)c);
            chars.AddRange("–—‘’“”…•€");
            return new string(chars.ToArray());
        }
    }
}
