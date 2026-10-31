using System;
using System.Collections.Generic;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Pure Wisdom Tree quiz rules (CLAUDE.md 5.8): a quiz is ready every QuizEveryHeals heals; questions come in random
    /// order without repeats until all have been seen, then a new round starts that never opens with the last question.
    /// </summary>
    public static class QuizRules
    {
        public static bool IsReady(BalanceConfig balance, GameState state) => state.HealedSinceQuiz >= balance.QuizEveryHeals;

        /// <summary>
        /// The next question: a random unseen one; when every question has been seen, newRound is true and the pick is
        /// any question except the last one seen. Null when there are no questions.
        /// </summary>
        public static QuizScenario PickNext(IReadOnlyList<QuizScenario> scenarios, IReadOnlyList<string> seen, Random random, out bool newRound)
        {
            newRound = false;
            if (scenarios == null || scenarios.Count == 0) return null;

            var candidates = new List<QuizScenario>(scenarios.Count);
            foreach (QuizScenario s in scenarios)
                if (s != null && !Contains(seen, s.Id)) candidates.Add(s);

            if (candidates.Count == 0)
            {
                newRound = true;
                string last = seen != null && seen.Count > 0 ? seen[seen.Count - 1] : null;
                foreach (QuizScenario s in scenarios)
                    if (s != null && (s.Id != last || scenarios.Count == 1)) candidates.Add(s);
            }
            return candidates.Count > 0 ? candidates[random.Next(candidates.Count)] : null;
        }

        public static bool IsCorrect(QuizScenario scenario, int optionIndex) =>
            scenario != null && optionIndex == scenario.CorrectIndex;

        private static bool Contains(IReadOnlyList<string> list, string id)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == id) return true;
            return false;
        }
    }
}
