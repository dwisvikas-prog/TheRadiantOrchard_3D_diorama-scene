using System;
using RadiantOrchard.Dev;
using RadiantOrchard.Gameplay;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Editor menu shortcuts for the most used dev panel actions (ENGINEERING_GUIDE 9.2), in Play mode. Same actions as
    /// the in-game panel (DevActions on DevGame.Current), so both behave identically.
    /// </summary>
    public static class DevGameMenu
    {
        private const int SeedsToAdd = 2000;
        private const int StageWithAllFruits = 4;

        [MenuItem("Radiant Orchard/Dev/Add 2000 Seeds")]
        public static void AddSeeds() => Report(DevActions.AddSeeds(DevGame.Current, SeedsToAdd));

        [MenuItem("Radiant Orchard/Dev/Set Stage 4 (all fruits, 9 plots)")]
        public static void SetStageFour() => Report(DevActions.SetStage(DevGame.Current, StageWithAllFruits));

        [MenuItem("Radiant Orchard/Dev/Ripen All Trees")]
        public static void RipenAll() => Report(DevActions.RipenAll(DevGame.Current));

        [MenuItem("Radiant Orchard/Dev/Skip 1 Hour")]
        public static void SkipHour() => Report(DevActions.AddTime(DevGame.Current, TimeSpan.FromHours(1)));

        [MenuItem("Radiant Orchard/Dev/Add 2000 Seeds", true)]
        [MenuItem("Radiant Orchard/Dev/Set Stage 4 (all fruits, 9 plots)", true)]
        [MenuItem("Radiant Orchard/Dev/Ripen All Trees", true)]
        [MenuItem("Radiant Orchard/Dev/Skip 1 Hour", true)]
        private static bool InPlayMode() => EditorApplication.isPlaying;

        private static void Report(string result) => Debug.Log("[Dev] " + result);
    }
}
