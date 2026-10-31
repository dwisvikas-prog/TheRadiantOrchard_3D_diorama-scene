using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>Blessing Rain (CLAUDE.md 5.11): every planted tree ripens at once. Needs a tree that is still growing.</summary>
    public sealed class BlessingRainEffect : IMiniEventEffect
    {
        private readonly PlotManager _plots;

        public BlessingRainEffect(PlotManager plots) => _plots = plots ?? throw new ArgumentNullException(nameof(plots));

        public MiniEventKind Kind => MiniEventKind.BlessingRain;

        public bool CanStart(MiniEventDefinition definition)
        {
            for (int i = 0; i < _plots.Count; i++)
                if (_plots.Get(i).IsGrowing) return true;
            return false;
        }

        public void Start(MiniEventDefinition definition) => _plots.RipenAll();
    }
}
