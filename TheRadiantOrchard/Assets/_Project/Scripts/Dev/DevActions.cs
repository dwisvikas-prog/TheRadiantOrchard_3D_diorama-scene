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
        /// Sets the vibrancy stage (clamped to 0..last stage) through VibrancyManager: the heal count moves to that
        /// stage's threshold so the meter matches; going up plays each stage reached (zones, plots, fruits); going down
        /// greys the zones above again and keeps plots open.
        /// </summary>
        public static string SetStage(DevGame game, int stage)
        {
            if (game == null) return NotRunning;
            game.Vibrancy.SetStage(stage);
            return $"Stage {game.State.VibrancyStage}, {game.State.HealedTotal} healed.";
        }

        /// <summary>Ripens every planted tree, and every fruit on the Wisdom Tree once it blooms.</summary>
        public static string RipenAll(DevGame game)
        {
            if (game == null) return NotRunning;
            int trees = game.Plots.RipenAll();
            return game.Bloom.IsActive ? $"Ripened {trees} tree(s) and {game.Bloom.RipenAll()} Wisdom Tree fruit." : $"Ripened {trees} tree(s).";
        }

        /// <summary>
        /// Brings the Wisdom Tree bloom (CLAUDE.md 5.9) the way play does: jumps to the final stage, so each stage still to
        /// come plays its zone, then the bloom celebration once.
        /// </summary>
        public static string BloomNow(DevGame game)
        {
            if (game == null) return NotRunning;
            if (game.Bloom.IsActive) return "The Wisdom Tree is already in bloom.";
            game.Vibrancy.SetStage(game.Vibrancy.LastStage);
            return game.Bloom.IsActive ? "Final stage reached: the Wisdom Tree blooms." : "The bloom did not start (see the console).";
        }

        /// <summary>Ripens every planted tree and makes each fruit golden (CLAUDE.md 5.12), to test golden harvests.</summary>
        public static string RipenGolden(DevGame game)
        {
            if (game == null) return NotRunning;
            game.Plots.RipenAll();
            int golden = 0;
            for (int i = 0; i < game.Plots.Count; i++)
                if (game.Plots.ForceGolden(i)) golden++;
            return golden > 0 ? $"{golden} golden fruit ripe." : "Nothing planted.";
        }

        /// <summary>Starts Golden Harvest (CLAUDE.md 5.8) as the quiz will: double seeds for GoldenHarvestSeconds.</summary>
        public static string GoldenHarvest(DevGame game)
        {
            if (game == null) return NotRunning;
            BalanceConfig b = game.Config.Balance;
            game.Boosts.Start(BoostService.GoldenHarvest, b.GoldenHarvestSeedMultiplier, b.GoldenHarvestSeconds);
            return $"Golden Harvest: x{b.GoldenHarvestSeedMultiplier} seeds for {b.GoldenHarvestSeconds:0} s.";
        }

        /// <summary>Starts this mini-event now (CLAUDE.md 5.11) if it can do something; the heal count is left alone.</summary>
        public static string MiniEvent(DevGame game, string id)
        {
            if (game == null) return NotRunning;
            return game.MiniEvents.StartNow(id) ? $"Event '{id}' started." : $"Event '{id}' cannot start now (nothing for it to do).";
        }

        /// <summary>Brings the daily visitor wave now (CLAUDE.md 5.10), whatever the date; it waits for free spots.</summary>
        public static string DailyWave(DevGame game)
        {
            if (game == null) return NotRunning;
            game.DailyWave.StartNow();
            return game.DailyWave.IsPending ? "Daily wave waiting for the path to empty (or something to be planted)." : "Daily wave on its way.";
        }

        /// <summary>Makes a Wisdom Tree quiz ready now (CLAUDE.md 5.8): the tree glows; tap it to answer.</summary>
        public static string QuizNow(DevGame game)
        {
            if (game == null) return NotRunning;
            if (!game.Quiz.HasQuestions) return "No quiz questions in GameConfig.";
            game.Quiz.MakeReady();
            return "Quiz ready: tap the Wisdom Tree.";
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

        /// <summary>As if the app had been closed for this long: visitors who came meanwhile are already waiting.</summary>
        public static string ArriveAfterAway(DevGame game, TimeSpan away)
        {
            if (game == null) return NotRunning;
            int count = game.Visitors.ArriveAfterAway(away);
            return count > 0 ? $"{count} visitor(s) were waiting." : "Nobody came: no free spot, or nothing planted.";
        }

        /// <summary>Everyone waiting on the path runs out of patience now and heads for the bench (or the jungle if it is full).</summary>
        public static string RunOutPatience(DevGame game)
        {
            if (game == null) return NotRunning;
            int count = game.Visitors.RunOutPatienceNow();
            return count > 0 ? $"{count} visitor(s) ran out of patience." : "Nobody is waiting on the path.";
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
