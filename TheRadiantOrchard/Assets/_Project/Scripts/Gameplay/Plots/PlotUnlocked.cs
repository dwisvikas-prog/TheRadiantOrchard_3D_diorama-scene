namespace RadiantOrchard.Gameplay
{
    /// <summary>A locked plot opened (vibrancy stage reached).</summary>
    public readonly struct PlotUnlocked
    {
        public readonly int Plot;

        public PlotUnlocked(int plot)
        {
            Plot = plot;
        }
    }
}
