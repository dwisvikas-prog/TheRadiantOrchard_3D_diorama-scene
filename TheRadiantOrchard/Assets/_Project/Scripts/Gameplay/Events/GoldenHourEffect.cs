using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Golden Hour (CLAUDE.md 5.11): double seeds for a while, reusing the Golden Harvest boost, so the gold glow and
    /// timer come with it. A Golden Harvest already running restarts with the event's time rather than stacking.
    /// </summary>
    public sealed class GoldenHourEffect : IMiniEventEffect
    {
        private readonly BoostService _boosts;

        public GoldenHourEffect(BoostService boosts) => _boosts = boosts ?? throw new ArgumentNullException(nameof(boosts));

        public MiniEventKind Kind => MiniEventKind.GoldenHour;

        public bool CanStart(MiniEventDefinition definition) => definition.DurationSeconds > 0f;

        public void Start(MiniEventDefinition definition) =>
            _boosts.Start(BoostService.GoldenHarvest, definition.Multiplier, definition.DurationSeconds);
    }
}
