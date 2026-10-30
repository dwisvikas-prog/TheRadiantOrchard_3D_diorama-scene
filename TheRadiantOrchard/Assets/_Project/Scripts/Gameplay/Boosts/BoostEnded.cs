namespace RadiantOrchard.Gameplay
{
    /// <summary>A seed boost ran out.</summary>
    public readonly struct BoostEnded
    {
        public readonly string Id;

        public BoostEnded(string id) => Id = id;
    }
}
