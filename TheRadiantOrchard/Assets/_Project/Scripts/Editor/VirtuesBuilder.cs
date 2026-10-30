using System.IO;
using RadiantOrchard.Core;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Builds the nine VirtueDefinition assets (CLAUDE.md 4) in Data/Virtues, each with its name key and its three
    /// situation line keys (5.4), links every fruit to its virtue and lists them on GameConfig. The text itself lives
    /// in the StringTable. Re-running updates the assets in place, so their GUIDs and links stay.
    /// </summary>
    public static class VirtuesBuilder
    {
        private const string GameConfigPath = "Assets/_Project/Data/GameConfig.asset";
        private const string Folder = "Assets/_Project/Data/Virtues";
        private const int LinesPerVirtue = 3;

        // CLAUDE.md section 4, in order: fruit id, virtue id, asset name.
        private static readonly (string fruit, string virtue, string asset)[] Matrix =
        {
            ("strawberry", "love", "Virtue_01_Love"),
            ("pineapple", "joy", "Virtue_02_Joy"),
            ("watermelon", "peace", "Virtue_03_Peace"),
            ("lemon", "patience", "Virtue_04_Patience"),
            ("grapes", "meekness", "Virtue_05_Meekness"),
            ("apple", "self_control", "Virtue_06_SelfControl"),
            ("peach", "kindness", "Virtue_07_Kindness"),
            ("banana", "goodness", "Virtue_08_Goodness"),
            ("cherry", "faithfulness", "Virtue_09_Faithfulness"),
        };

        [MenuItem("Radiant Orchard/Setup/Build Virtues")]
        public static void Build()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null)
            {
                Debug.LogError($"VirtuesBuilder: no GameConfig at {GameConfigPath}; nothing was changed.");
                return;
            }

            var virtues = new VirtueDefinition[Matrix.Length];
            for (int i = 0; i < Matrix.Length; i++)
            {
                (string fruitId, string virtueId, string assetName) = Matrix[i];
                virtues[i] = BuildVirtue(virtueId, assetName, config.Strings);

                FruitDefinition fruit = config.FindFruit(fruitId);
                if (fruit == null)
                {
                    Debug.LogError($"VirtuesBuilder: GameConfig has no fruit '{fruitId}'; it was not linked.");
                    continue;
                }
                var fruitSo = new SerializedObject(fruit);
                fruitSo.FindProperty("_virtue").objectReferenceValue = virtues[i];
                fruitSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fruit);
            }

            var configSo = new SerializedObject(config);
            SerializedProperty list = configSo.FindProperty("_virtues");
            list.arraySize = virtues.Length;
            for (int i = 0; i < virtues.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = virtues[i];
            configSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            // The folder has real content now (ENGINEERING_GUIDE 3).
            string keep = Folder + "/.gitkeep";
            if (File.Exists(keep)) File.Delete(keep);

            AssetDatabase.SaveAssets();
            Debug.Log($"VirtuesBuilder: {virtues.Length} virtues built in {Folder}, linked to their fruits and listed on GameConfig.");
        }

        private static VirtueDefinition BuildVirtue(string id, string assetName, StringTable strings)
        {
            string path = $"{Folder}/{assetName}.asset";
            var virtue = AssetDatabase.LoadAssetAtPath<VirtueDefinition>(path);
            if (virtue == null)
            {
                virtue = ScriptableObject.CreateInstance<VirtueDefinition>();
                AssetDatabase.CreateAsset(virtue, path);
            }

            var so = new SerializedObject(virtue);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_nameKey").stringValue = $"virtue.{id}";
            SerializedProperty lines = so.FindProperty("_situationLineKeys");
            lines.arraySize = LinesPerVirtue;
            for (int n = 0; n < LinesPerVirtue; n++)
            {
                string key = $"situation.{id}.{n + 1}";
                if (strings != null && !strings.Contains(key))
                    Debug.LogWarning($"VirtuesBuilder: StringTable has no '{key}'; add it so the line can show.");
                lines.GetArrayElementAtIndex(n).stringValue = key;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(virtue);
            return virtue;
        }
    }
}
