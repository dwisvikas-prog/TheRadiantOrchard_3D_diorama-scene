namespace RadiantOrchard.Gameplay
{
    /// <summary>The quiz was answered (CLAUDE.md 5.8): right starts Golden Harvest, wrong shows a hint; either way it is used up.</summary>
    public readonly struct QuizAnswered
    {
        public readonly string ScenarioId;
        public readonly bool Correct;

        public QuizAnswered(string scenarioId, bool correct)
        {
            ScenarioId = scenarioId;
            Correct = correct;
        }
    }
}
