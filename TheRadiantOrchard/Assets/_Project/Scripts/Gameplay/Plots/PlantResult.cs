namespace RadiantOrchard.Gameplay
{
    /// <summary>Outcome of PlotManager.TryPlant; the plant menu uses it to grey out options.</summary>
    public enum PlantResult
    {
        Planted = 0,
        InvalidPlot = 1,
        PlotLocked = 2,
        PlotOccupied = 3,
        UnknownFruit = 4,
        FruitLocked = 5,
        NotEnoughSeeds = 6,
    }
}
