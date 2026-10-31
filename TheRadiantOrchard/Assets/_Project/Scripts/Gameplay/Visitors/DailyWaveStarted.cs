namespace RadiantOrchard.Gameplay
{
    /// <summary>The daily visitor wave walked in (CLAUDE.md 5.10): the banner names the bonus for healing them all.</summary>
    public readonly struct DailyWaveStarted
    {
        public readonly int Visitors;
        public readonly int BonusSeeds;

        public DailyWaveStarted(int visitors, int bonusSeeds)
        {
            Visitors = visitors;
            BonusSeeds = bonusSeeds;
        }
    }
}
