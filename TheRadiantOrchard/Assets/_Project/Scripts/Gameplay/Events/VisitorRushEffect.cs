using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Visitor Rush (CLAUDE.md 5.11): visitors fill every free path spot at once (up to the event's count), walking in
    /// together as the daily wave does, with normal patience and no bonus. Needs a free spot, something planted, and no
    /// daily wave waiting for the path.
    /// </summary>
    public sealed class VisitorRushEffect : IMiniEventEffect
    {
        private readonly VisitorManager _visitors;
        private readonly int[] _ids = new int[8];

        public VisitorRushEffect(VisitorManager visitors) => _visitors = visitors ?? throw new ArgumentNullException(nameof(visitors));

        public MiniEventKind Kind => MiniEventKind.VisitorRush;

        public bool CanStart(MiniEventDefinition definition) =>
            definition.VisitorCount > 0 && _visitors.FreeSpots > 0 && _visitors.HasPlantedFruit && !_visitors.HoldSpawning;

        public void Start(MiniEventDefinition definition)
        {
            int count = Math.Min(Math.Min(definition.VisitorCount, _visitors.FreeSpots), _ids.Length);
            _visitors.SpawnGroup(count, 0f, _ids);
        }
    }
}
