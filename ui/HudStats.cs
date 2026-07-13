public readonly struct HudStats
{
    public int Size { get; init; }
    public int FoodEaten { get; init; }
    public int FoodTowardsNextSize { get; init; }
    public int FoodNeededForNextSize { get; init; }
    public int ComboCount { get; init; }
    public float ComboMultiplier { get; init; }
    public float ComboTimeRemaining { get; init; }
    public float ComboTimeRatio { get; init; }
}
