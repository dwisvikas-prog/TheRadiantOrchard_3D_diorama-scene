using System.IO;
using RadiantOrchard.Core;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Builds the three mini-event assets (CLAUDE.md 5.11) in Data/Events and lists them on GameConfig: Golden Hour
    /// (90 s of double seeds), Visitor Rush (up to 3 visitors at once) and Blessing Rain (every tree ripe). Text lives in
    /// the StringTable. Re-running updates the assets in place, so their GUIDs stay.
    /// </summary>
    public static class MiniEventsBuilder
    {
        private const string GameConfigPath = "Assets/_Project/Data/GameConfig.asset";
        private const string Folder = "Assets/_Project/Data/Events";

        [MenuItem("Radiant Orchard/Setup/Build Mini Events")]
        public static void Build()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null)
            {
                Debug.LogError($"MiniEventsBuilder: no GameConfig at {GameConfigPath}; nothing was changed.");
                return;
            }

            MiniEventDefinition[] events =
            {
                Build(config, "MiniEvent_GoldenHour", "golden_hour", MiniEventKind.GoldenHour, 90f, 2f, 0),
                Build(config, "MiniEvent_VisitorRush", "visitor_rush", MiniEventKind.VisitorRush, 0f, 1f, 3),
                Build(config, "MiniEvent_BlessingRain", "blessing_rain", MiniEventKind.BlessingRain, 0f, 1f, 0),
            };

            var so = new SerializedObject(config);
            SerializedProperty list = so.FindProperty("_miniEvents");
            list.arraySize = events.Length;
            for (int i = 0; i < events.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = events[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            // The folder has real content now (ENGINEERING_GUIDE 3).
            string keep = Folder + "/.gitkeep";
            if (File.Exists(keep)) File.Delete(keep);

            AssetDatabase.SaveAssets();
            Debug.Log($"MiniEventsBuilder: {events.Length} mini-events built in {Folder} and listed on GameConfig.");
        }

        private static MiniEventDefinition Build(GameConfig config, string assetName, string id, MiniEventKind kind,
            float durationSeconds, float multiplier, int visitorCount)
        {
            string path = $"{Folder}/{assetName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<MiniEventDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MiniEventDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            var so = new SerializedObject(definition);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_kind").enumValueIndex = (int)kind;
            so.FindProperty("_titleKey").stringValue = $"event.{id}.title";
            so.FindProperty("_bodyKey").stringValue = $"event.{id}.body";
            so.FindProperty("_weight").floatValue = 1f;
            so.FindProperty("_durationSeconds").floatValue = durationSeconds;
            so.FindProperty("_multiplier").floatValue = multiplier;
            so.FindProperty("_visitorCount").intValue = visitorCount;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            foreach (string key in new[] { $"event.{id}.title", $"event.{id}.body" })
                if (config.Strings != null && !config.Strings.Contains(key))
                    Debug.LogWarning($"MiniEventsBuilder: StringTable has no '{key}'; add it so the banner can show.");
            return definition;
        }
    }
}
