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
    public int FoodTowardsNextSize => _foodTowardsNextSize;
    public int FoodNeededForNextSize => GetFoodRequiredForNextSize();

    private int _foodTowardsNextSize = 0;
    private World _world;
    private CollisionShape2D _collisionShape;
    private AnimatedSprite2D _sprite;

    public override void _Ready()
    {
        _world = GetParentOrNull<World>();
        _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite");
        ApplySizeScale();
    }

    public override void _Process(double delta)
    {
        // Player Movement
        var input = Input.GetVector("move_left", "move_right", "move_up", "move_down").Normalized();
        Velocity = input * Speed;
        MoveAndSlide();
        UpdateFacingDirection(input);
        ClampToPlayableArea();
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
            _world?.ShowGrowthPopup("<GROWN>", GlobalPosition + new Vector2(0.0f, -32.0f));
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

    private void ClampToPlayableArea()
    {
        if (_world == null)
        {
            return;
        }

        var playableArea = _world.GetPlayableArea();
        var minPosition = playableArea.Position;
        var maxPosition = playableArea.End;

        if (_collisionShape?.Shape is RectangleShape2D rectangleShape)
        {
            var scaledOffset = _collisionShape.Position * Scale;
            var scaledExtents = rectangleShape.Size * Scale / 2.0f;

            minPosition += scaledExtents - scaledOffset;
            maxPosition -= scaledExtents + scaledOffset;
        }

        Position = new Vector2(
            Mathf.Clamp(Position.X, minPosition.X, maxPosition.X),
            Mathf.Clamp(Position.Y, minPosition.Y, maxPosition.Y)
        );
    }

    private void UpdateFacingDirection(Vector2 input)
    {
        if (_sprite == null || Mathf.IsZeroApprox(input.X))
        {
            return;
        }

        _sprite.FlipH = input.X < 0.0f;
    }
}
