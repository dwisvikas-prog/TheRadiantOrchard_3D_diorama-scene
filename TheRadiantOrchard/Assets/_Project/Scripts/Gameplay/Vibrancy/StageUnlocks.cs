using System;
using System.Collections.Generic;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// What a vibrancy stage opens (CLAUDE.md 5.7), for the unlock banner: the fruits whose UnlockStage is this stage
    /// (in GameConfig order) and the plots added since the stage before. Read from data, so a new fruit needs no code.
    /// </summary>
    public sealed class StageUnlocks
    {
        private StageUnlocks(int stage, IReadOnlyList<FruitDefinition> fruits, int newPlots, bool isFinal)
        {
            Stage = stage;
            Fruits = fruits;
            NewPlots = newPlots;
            IsFinal = isFinal;
        }

        public int Stage { get; }
        public IReadOnlyList<FruitDefinition> Fruits { get; }
        public int NewPlots { get; }
        public bool IsFinal { get; }

        public static StageUnlocks For(GameConfig config, int stage)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            BalanceConfig balance = config.Balance;
            var fruits = new List<FruitDefinition>();
            foreach (FruitDefinition fruit in config.Fruits)
                if (fruit != null && fruit.UnlockStage == stage && stage > 0) fruits.Add(fruit);
            int newPlots = stage > 0 ? balance.PlotsOpenAt(stage) - balance.PlotsOpenAt(stage - 1) : 0;
            return new StageUnlocks(stage, fruits, newPlots, stage >= balance.StageThresholds.Count);
        }
    }
}
