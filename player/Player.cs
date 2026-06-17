using Godot;
using System;

public partial class Player : CharacterBody2D
{
    [Export]
    public float Speed = 200.0f;
    
    [Export]
    public int Size = 1;

    [Export]
    public int FoodToGrow = 10;

    [Export]
    public Curve FoodToGrowCurve;

    [Export]
    public int FoodCurveMaxSize = 20;

    [Export]
    public float GrowthPerLevel = 0.1f;

    public int FoodEaten { get; private set; } = 0;

    private int _foodTowardsNextSize = 0;

    public override void _Ready()
    {
        ApplySizeScale();
    }

    public override void _Process(double delta)
    {
        // Player Movement
        var input = Input.GetVector("move_left", "move_right", "move_up", "move_down").Normalized();
        Velocity = input * Speed;
        MoveAndSlide();
    }

    public void EatFood(int amount = 1)
    {
        if (amount <= 0)
        {
            return;
        }

        FoodEaten += amount;
        _foodTowardsNextSize += amount;

        var grew = false;

        while (true)
        {
            var requiredFood = GetFoodRequiredForNextSize();
            if (_foodTowardsNextSize < requiredFood)
            {
                break;
            }

            _foodTowardsNextSize -= requiredFood;
            Size += 1;
            grew = true;
        }

        if (grew)
        {
            ApplySizeScale();
            GD.Print($"Player grew! Size: {Size} (Food eaten: {FoodEaten})");
        }

        var needed = GetFoodRequiredForNextSize();
        var remaining = needed - _foodTowardsNextSize;
        GD.Print($"Ate {amount} food. Progress: {_foodTowardsNextSize}/{needed} — {remaining} more needed to grow.");
    }

    private int GetFoodRequiredForNextSize()
    {
        if (FoodToGrowCurve != null)
        {
            var maxSize = Math.Max(2, FoodCurveMaxSize);
            var x = (float)(Size - 1) / (maxSize - 1);
            x = Mathf.Clamp(x, 0.0f, 1.0f);

            var curveValue = FoodToGrowCurve.SampleBaked(x);
            return Math.Max(1, Mathf.RoundToInt(curveValue));
        }

        return Math.Max(1, FoodToGrow);
    }

    private void ApplySizeScale()
    {
        var targetScale = 1.0f + (Size - 1) * GrowthPerLevel;
        Scale = new Vector2(targetScale, targetScale);
    }
}
