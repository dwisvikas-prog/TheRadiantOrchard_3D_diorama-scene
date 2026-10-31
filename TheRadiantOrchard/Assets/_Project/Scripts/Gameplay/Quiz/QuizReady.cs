namespace RadiantOrchard.Gameplay
{
    /// <summary>A Wisdom Tree quiz is waiting (CLAUDE.md 5.8): the tree glows until it is answered.</summary>
    public readonly struct QuizReady
    {
        public readonly int HealedSinceQuiz;

        public QuizReady(int healedSinceQuiz)
        {
            HealedSinceQuiz = healedSinceQuiz;
        }
    }
}
