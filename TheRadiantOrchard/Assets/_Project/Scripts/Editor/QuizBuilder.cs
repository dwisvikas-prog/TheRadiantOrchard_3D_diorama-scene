using System.IO;
using RadiantOrchard.Core;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Builds the Wisdom Tree quiz set (CLAUDE.md 5.8) in Data/Quiz: one question per virtue (GameConfig order), its
    /// scenario and hint keys, and three fruit choices: the virtue's own fruit plus two others (placeholders until the
    /// client sends the real questions; then edit the asset or this table). Lists it on GameConfig. Re-running rebuilds
    /// the questions in place, so the asset GUID stays.
    /// </summary>
    public static class QuizBuilder
    {
        private const string GameConfigPath = "Assets/_Project/Data/GameConfig.asset";
        private const string Folder = "Assets/_Project/Data/Quiz";
        private const string AssetPath = Folder + "/QuizSet.asset";
        // The two wrong choices are the fruits this many places further along the list (mixes gestures and stages).
        private static readonly int[] WrongOffsets = { 3, 6 };

        [MenuItem("Radiant Orchard/Setup/Build Quiz")]
        public static void Build()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null || config.Virtues.Count == 0)
            {
                Debug.LogError($"QuizBuilder: GameConfig at {GameConfigPath} needs its virtues (run Build Virtues); nothing was changed.");
                return;
            }

            var set = AssetDatabase.LoadAssetAtPath<QuizSet>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<QuizSet>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }

            int count = config.Virtues.Count;
            var so = new SerializedObject(set);
            SerializedProperty list = so.FindProperty("_scenarios");
            list.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                VirtueDefinition virtue = config.Virtues[i];
                FruitDefinition own = FruitOf(config, virtue);
                if (own == null)
                {
                    Debug.LogError($"QuizBuilder: no fruit links to virtue '{virtue.Id}'; its question has no right answer.");
                    continue;
                }
                int ownIndex = IndexOf(config, own);
                // Rotate where the right answer sits in the data (the panel shuffles the shown order too).
                int correct = i % 3;
                var options = new FruitDefinition[3];
                options[correct] = own;
                for (int n = 0, slot = 0; n < WrongOffsets.Length; n++, slot++)
                {
                    if (slot == correct) slot++;
                    options[slot] = config.Fruits[(ownIndex + WrongOffsets[n]) % config.Fruits.Count];
                }

                SerializedProperty q = list.GetArrayElementAtIndex(i);
                q.FindPropertyRelative("_id").stringValue = virtue.Id;
                q.FindPropertyRelative("_virtue").objectReferenceValue = virtue;
                q.FindPropertyRelative("_scenarioKey").stringValue = $"quiz.{virtue.Id}.scenario";
                q.FindPropertyRelative("_hintKey").stringValue = $"quiz.{virtue.Id}.hint";
                SerializedProperty opts = q.FindPropertyRelative("_options");
                opts.arraySize = options.Length;
                for (int n = 0; n < options.Length; n++) opts.GetArrayElementAtIndex(n).objectReferenceValue = options[n];
                q.FindPropertyRelative("_correctIndex").intValue = correct;

                foreach (string key in new[] { $"quiz.{virtue.Id}.scenario", $"quiz.{virtue.Id}.hint" })
                    if (config.Strings != null && !config.Strings.Contains(key))
                        Debug.LogWarning($"QuizBuilder: StringTable has no '{key}'; add it so the question can show.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);

            var configSo = new SerializedObject(config);
            configSo.FindProperty("_quiz").objectReferenceValue = set;
            configSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            // The folder has real content now (ENGINEERING_GUIDE 3).
            string keep = Folder + "/.gitkeep";
            if (File.Exists(keep)) File.Delete(keep);

            AssetDatabase.SaveAssets();
            Debug.Log($"QuizBuilder: {count} questions built in {AssetPath} and set on GameConfig.");
        }

        private static FruitDefinition FruitOf(GameConfig config, VirtueDefinition virtue)
        {
            foreach (FruitDefinition f in config.Fruits)
                if (f != null && f.Virtue == virtue) return f;
            return null;
        }

        private static int IndexOf(GameConfig config, FruitDefinition fruit)
        {
            for (int i = 0; i < config.Fruits.Count; i++)
                if (config.Fruits[i] == fruit) return i;
            return 0;
        }
    }
}
