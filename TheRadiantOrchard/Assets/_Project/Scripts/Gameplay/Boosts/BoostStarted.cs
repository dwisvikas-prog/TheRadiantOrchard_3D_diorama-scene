namespace RadiantOrchard.Gameplay
{
    /// <summary>A seed boost began (or restarted): Golden Harvest's warm glow starts here.</summary>
    public readonly struct BoostStarted
    {
        public readonly string Id;
        public readonly float Multiplier;
        public readonly float Seconds;

        public BoostStarted(string id, float multiplier, float seconds)
        {
            Id = id;
            Multiplier = multiplier;
            Seconds = seconds;
        }
    }
}
