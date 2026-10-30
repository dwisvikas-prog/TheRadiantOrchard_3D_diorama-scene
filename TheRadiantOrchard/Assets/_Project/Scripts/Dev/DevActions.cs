using System;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEngine;

namespace RadiantOrchard.Dev
{
    /// <summary>
    /// What the dev panel's buttons do (and the Editor's Dev menu). Each goes through the real systems, so saving,
    /// events and views behave exactly as in play. Returns a one-line result for the panel's status line.
    /// Text here is for developers only, so it is not in the StringTable.
    /// </summary>
    public static class DevActions
    {
        public const string NotRunning = "Game not running (start Play from Boot).";

        /// <summary>Moves game time forward (the dev offset, kept across restarts in dev builds); trees grow as if time passed.</summary>
        public static string AddTime(DevGame game, TimeSpan amount)
        {
            if (game == null) return NotRunning;
            game.Time.AddDevOffset(amount);
            return $"Time +{amount} (offset now {game.Time.DevOffset}).";
        }

        public static string ResetTime(DevGame game)
        {
            if (game == null) return NotRunning;
            game.Time.ResetDevOffset();
            return "Back on real time.";
        }

        public static string AddSeeds(DevGame game, int amount)
        {
            if (game == null) return NotRunning;
            game.State.Seeds += amount;
            game.Events.Publish(new SeedsChanged(game.State.Seeds, amount));
            game.Save.MarkDirty();
            return $"+{amount} seeds, now {game.State.Seeds}.";
        }

        /// <summary>
        /// Sets the vibrancy stage (clamped to 0..last stage) and opens its plots and fruits. Lowering the stage does not
        /// close plots again. VibrancyManager (W2·10) takes this over with zones and the unlock sequence.
        /// </summary>
        public static string SetStage(DevGame game, int stage)
        {
            if (game == null) return NotRunning;
            game.State.VibrancyStage = Mathf.Clamp(stage, 0, game.Config.Balance.StageThresholds.Count);
            game.Plots.ApplyStageUnlocks();
            game.Save.MarkDirty();
            return $"Stage {game.State.VibrancyStage}.";
        }

        public static string RipenAll(DevGame game)
        {
            if (game == null) return NotRunning;
            return $"Ripened {game.Plots.RipenAll()} tree(s).";
        }

        /// <summary>Heals count visitors through the real heal path (seeds, counters, events, save).</summary>
        public static string Heal(DevGame game, int count)
        {
            if (game == null) return NotRunning;
            string fruitId = game.Config.Fruits[0].Id;
            int seeds = 0;
            for (int i = 0; i < count; i++) seeds += game.Heal.Heal(fruitId);
            return $"Healed {count}: +{seeds} seeds, {game.State.HealedTotal} healed in total.";
        }

        public static string SpawnVisitor(DevGame game)
        {
            if (game == null) return NotRunning;
            int id = game.Visitors.SpawnNow();
            return id >= 0 ? $"Visitor {id} is on the way." : "No room on the path, or nothing planted yet.";
        }

        public static string DumpSave(DevGame game)
        {
            if (game == null) return NotRunning;
            Debug.Log("[Dev] Save JSON:\n" + JsonUtility.ToJson(game.State, true));
            return "Save JSON written to the log.";
        }

        public static string DeleteSave(DevGame game)
        {
            if (game == null) return NotRunning;
            game.Save.DeleteAndStopSaving();
            return "Save deleted. Restart the app (or stop Play) for a new game.";
        }
    }
}
