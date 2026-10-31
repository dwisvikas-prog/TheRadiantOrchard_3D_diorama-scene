namespace RadiantOrchard.Gameplay
{
    /// <summary>What answering the quiz did: whether it counted, whether it was right, and the hint for a wrong answer.</summary>
    public readonly struct QuizOutcome
    {
        public static readonly QuizOutcome None = new QuizOutcome(false, false, null);

        public readonly bool Answered;
        public readonly bool Correct;
        /// <summary>StringTable key of the gentle hint (wrong answers only).</summary>
        public readonly string HintKey;

        public QuizOutcome(bool answered, bool correct, string hintKey)
        {
            Answered = answered;
            Correct = correct;
            HintKey = hintKey;
        }
    }
}
