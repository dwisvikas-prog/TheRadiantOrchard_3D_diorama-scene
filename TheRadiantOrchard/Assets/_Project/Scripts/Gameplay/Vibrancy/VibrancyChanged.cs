namespace RadiantOrchard.Gameplay
{
    /// <summary>The heal count or stage changed: the Vibrancy Meter redraws, zones above the stage stay grey.</summary>
    public readonly struct VibrancyChanged
    {
        public readonly int HealedTotal;
        public readonly int Stage;

        public VibrancyChanged(int healedTotal, int stage)
        {
            HealedTotal = healedTotal;
            Stage = stage;
        }
    }
}
