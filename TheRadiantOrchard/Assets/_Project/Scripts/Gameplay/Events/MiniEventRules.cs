using System;
using System.Collections.Generic;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Pure mini-event choice (CLAUDE.md 5.11): weighted random among the events that can start now, never the one that
    /// ran last unless it is the only one possible. Null when none can start.
    /// </summary>
    public static class MiniEventRules
    {
        public static MiniEventDefinition Pick(IReadOnlyList<MiniEventDefinition> events, Func<MiniEventDefinition, bool> canStart,
            string lastId, Random random)
        {
            MiniEventDefinition picked = PickAmong(events, canStart, lastId, random);
            return picked ?? PickAmong(events, canStart, null, random);
        }

        private static MiniEventDefinition PickAmong(IReadOnlyList<MiniEventDefinition> events, Func<MiniEventDefinition, bool> canStart,
            string excludeId, Random random)
        {
            if (events == null) return null;
            float total = 0f;
            foreach (MiniEventDefinition e in events)
                if (Eligible(e, canStart, excludeId)) total += e.Weight;
            if (total <= 0f) return null;

            double roll = random.NextDouble() * total;
            MiniEventDefinition last = null;
            foreach (MiniEventDefinition e in events)
            {
                if (!Eligible(e, canStart, excludeId)) continue;
                last = e;
                roll -= e.Weight;
                if (roll < 0d) return e;
            }
            return last;
        }

        private static bool Eligible(MiniEventDefinition e, Func<MiniEventDefinition, bool> canStart, string excludeId) =>
            e != null && e.Weight > 0f && e.Id != excludeId && canStart(e);
    }
}
