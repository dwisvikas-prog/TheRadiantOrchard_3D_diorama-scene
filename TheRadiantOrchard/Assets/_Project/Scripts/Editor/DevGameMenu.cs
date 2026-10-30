using RadiantOrchard.App;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Play-mode cheats (ENGINEERING_GUIDE 9.2) until the in-game dev panel exists: seeds, stage and ripening, so
    /// every fruit and gesture can be tried without playing for days. Editor only; changes are saved like any other.
    /// </summary>
    public static class DevGameMenu
    {
        private const int SeedsToAdd = 2000;
        private const int StageWithAllFruits = 4;

        [MenuItem("Radiant Orchard/Dev/Add 2000 Seeds")]
        public static void AddSeeds()
        {
            if (!TryGetGame(out AppServices services, out _)) return;
            services.State.Seeds += SeedsToAdd;
            services.Events.Publish(new SeedsChanged(services.State.Seeds, SeedsToAdd));
            services.Save.MarkDirty();
            Debug.Log($"Dev: +{SeedsToAdd} seeds, now {services.State.Seeds}.");
        }

        [MenuItem("Radiant Orchard/Dev/Set Stage 4 (all fruits, 9 plots)")]
        public static void SetStageFour()
        {
            if (!TryGetGame(out AppServices services, out PlotManager plots)) return;
            services.State.VibrancyStage = Mathf.Max(services.State.VibrancyStage, StageWithAllFruits);
            plots.ApplyStageUnlocks();
            services.Save.MarkDirty();
            Debug.Log($"Dev: vibrancy stage {services.State.VibrancyStage}.");
        }

        [MenuItem("Radiant Orchard/Dev/Ripen All Trees")]
        public static void RipenAll()
        {
            if (!TryGetGame(out _, out PlotManager plots)) return;
            Debug.Log($"Dev: ripened {plots.RipenAll()} trees.");
        }

        [MenuItem("Radiant Orchard/Dev/Add 2000 Seeds", true)]
        [MenuItem("Radiant Orchard/Dev/Set Stage 4 (all fruits, 9 plots)", true)]
        [MenuItem("Radiant Orchard/Dev/Ripen All Trees", true)]
        private static bool InPlayMode() => EditorApplication.isPlaying;

        private static bool TryGetGame(out AppServices services, out PlotManager plots)
        {
            services = AppServices.Current;
            // Editor tooling may search the scene; the no-Find rule is for runtime code.
            var installer = Object.FindFirstObjectByType<GameInstaller>();
            plots = installer != null ? installer.DevPlots : null;
            if (services != null && plots != null) return true;
            Debug.LogWarning("Dev: start Play mode from Boot first.");
            return false;
        }
    }
}
