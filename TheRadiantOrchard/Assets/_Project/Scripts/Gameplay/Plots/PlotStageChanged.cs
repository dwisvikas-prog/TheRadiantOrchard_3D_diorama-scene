namespace RadiantOrchard.Gameplay
{
    /// <summary>A plot's visible stage changed (planted, grew, ripened, harvested).</summary>
    public readonly struct PlotStageChanged
    {
        public readonly int Plot;
        public readonly TreeStage Stage;

        public PlotStageChanged(int plot, TreeStage stage)
        {
            Plot = plot;
            Stage = stage;
        }
    }
}
