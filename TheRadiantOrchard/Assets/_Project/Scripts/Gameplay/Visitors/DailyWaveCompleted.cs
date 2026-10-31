namespace RadiantOrchard.Gameplay
{
    /// <summary>Every visitor of the daily wave has been healed and the bonus is paid (CLAUDE.md 5.10).</summary>
    public readonly struct DailyWaveCompleted
    {
        public readonly int BonusSeeds;
        /// <summary>The last one healed, where the bonus pop-up shows.</summary>
        public readonly int LastVisitorId;

        public DailyWaveCompleted(int bonusSeeds, int lastVisitorId)
        {
            BonusSeeds = bonusSeeds;
            LastVisitorId = lastVisitorId;
        }
    }
}
