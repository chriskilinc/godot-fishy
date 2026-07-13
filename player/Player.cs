using Godot;
using System;

public partial class Player : CharacterBody2D
{
    [Signal]
    public delegate void StatsChangedEventHandler();

    [Signal]
    public delegate void FoodGainedEventHandler(int amount, Vector2 worldPosition);

    [Signal]
    public delegate void GrewEventHandler(string text, Vector2 worldPosition);

    [Signal]
    public delegate void ComboTriggeredEventHandler(string text, Vector2 worldPosition);

    [Export]
    public float Speed = 200.0f;

    [Export]
    public float Acceleration = 900.0f;

    [Export]
    public float Deceleration = 700.0f;

    [Export]
    public float CoastDecelerationMultiplier = 0.35f;

    [Export]
    public float WaterDrag = 1.6f;

    [Export]
    public float InputSmoothing = 10.0f;

    [Export]
    public float ReleaseInputSmoothingMultiplier = 0.3f;

    [Export]
    public float TurnResponsiveness = 8.0f;

    [Export]
    public float MinTurnSpeed = 20.0f;

    [Export]
    public float BodySwayAmount = 0.06f;

    [Export]
    public float BodySwaySpeed = 12.0f;

    [Export]
    public float MaxTiltDegrees = 16.0f;

    [Export]
    public float TiltResponsiveness = 10.0f;
    
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

    [Export]
    public float ComboWindowSeconds = 0.75f;

    [Export]
    public float ComboBonusPerChain = 0.5f;

    [Export]
    public float ComboExtraWindowPerChain = 0.15f;

    [Export]
    public float MaxComboWindowSeconds = 1.5f;

    [Export]
    public bool ComboEnabled = false;

    public int FoodEaten { get; private set; } = 0;
    public int FoodTowardsNextSize => _foodTowardsNextSize;
    public int FoodNeededForNextSize => GetFoodRequiredForNextSize();
    public int ComboCount => ComboEnabled ? _comboCount : 0;
    public float ComboMultiplier => ComboEnabled ? 1.0f + Math.Max(0, _comboCount - 1) * ComboBonusPerChain : 1.0f;
    public float ComboTimeRemaining => ComboEnabled ? (float)_comboTimeRemaining : 0.0f;
    public float ComboTimeRatio => ComboEnabled && GetCurrentComboWindowSeconds() > 0.0f ? Mathf.Clamp((float)(_comboTimeRemaining / GetCurrentComboWindowSeconds()), 0.0f, 1.0f) : 0.0f;

    private int _foodTowardsNextSize = 0;
    private int _comboCount = 0;
    private double _comboTimeRemaining = 0.0;
    private World _world;
    private CollisionShape2D _collisionShape;
    private AnimatedSprite2D _sprite;
    private Vector2 _smoothedInput = Vector2.Zero;
    private float _swimTime = 0.0f;
    private float _visualTilt = 0.0f;

    public override void _Ready()
    {
        _world = GetParentOrNull<World>();
        _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite");
        ApplySizeScale();
    }

    public override void _Process(double delta)
    {
        UpdateCombo(delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        var rawInput = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        var hasInput = rawInput.LengthSquared() > 0.0001f;

        var releaseSmoothing = InputSmoothing * Mathf.Max(0.01f, ReleaseInputSmoothingMultiplier);
        var smoothing = hasInput ? InputSmoothing : releaseSmoothing;
        var inputLerpWeight = 1.0f - Mathf.Exp(-smoothing * dt);
        _smoothedInput = _smoothedInput.Lerp(rawInput, inputLerpWeight);

        var targetVelocity = _smoothedInput * Speed;
        var coastDeceleration = Deceleration * Mathf.Max(0.0f, CoastDecelerationMultiplier);
        var accelerationRate = hasInput ? Acceleration : coastDeceleration;
        Velocity = Velocity.MoveToward(targetVelocity, accelerationRate * dt);

        // Apply water drag as a frame-rate independent damping force.
        var dragFactor = Mathf.Clamp(WaterDrag * dt, 0.0f, 1.0f);
        Velocity -= Velocity * dragFactor;

        MoveAndSlide();
        ClampToPlayableArea();
        UpdateSwimmingVisuals(dt);
    }

    public int EatFood(int amount = 1)
    {
        return EatFood(amount, null);
    }

    public int EatFood(int amount, Vector2? worldPosition)
    {
        if (amount <= 0)
        {
            return 0;
        }

        RegisterCombo();
        var gainedAmount = Math.Max(1, Mathf.RoundToInt(amount * ComboMultiplier));
        var popupPosition = worldPosition ?? GlobalPosition;

        FoodEaten += gainedAmount;
        _foodTowardsNextSize += gainedAmount;
        EmitSignal(SignalName.FoodGained, gainedAmount, popupPosition);

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
            EmitSignal(SignalName.Grew, "<GROWN>", GlobalPosition + new Vector2(0.0f, -32.0f));
            GD.Print($"Player grew! Size: {Size} (Food eaten: {FoodEaten})");
        }

        var needed = GetFoodRequiredForNextSize();
        var remaining = needed - _foodTowardsNextSize;
        GD.Print($"Ate {gainedAmount} food. Progress: {_foodTowardsNextSize}/{needed} — {remaining} more needed to grow.");
        EmitSignal(SignalName.StatsChanged);
        return gainedAmount;
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

    private void RegisterCombo()
    {
        if (!ComboEnabled)
        {
            return;
        }

        _comboCount += 1;
        _comboTimeRemaining = Math.Max(0.1f, GetCurrentComboWindowSeconds());

        if (_comboCount > 1)
        {
            EmitSignal(SignalName.ComboTriggered, $"COMBO x{ComboMultiplier:0.0}", GlobalPosition + new Vector2(0.0f, -56.0f));
        }

        EmitSignal(SignalName.StatsChanged);
    }

    private void UpdateCombo(double delta)
    {
        if (!ComboEnabled)
        {
            if (_comboCount != 0 || _comboTimeRemaining > 0.0)
            {
                _comboCount = 0;
                _comboTimeRemaining = 0.0;
                EmitSignal(SignalName.StatsChanged);
            }
            return;
        }

        if (_comboCount <= 0)
        {
            return;
        }

        var previousRatio = ComboTimeRatio;
        _comboTimeRemaining = Math.Max(0.0, _comboTimeRemaining - delta);
        if (_comboTimeRemaining > 0.0)
        {
            if (_comboCount > 1 && !Mathf.IsEqualApprox(previousRatio, ComboTimeRatio))
            {
                EmitSignal(SignalName.StatsChanged);
            }
            return;
        }

        _comboCount = 0;
        _comboTimeRemaining = 0.0;
        EmitSignal(SignalName.StatsChanged);
    }

    private float GetCurrentComboWindowSeconds()
    {
        var comboWindow = ComboWindowSeconds + Math.Max(0, _comboCount - 1) * ComboExtraWindowPerChain;
        return Mathf.Min(comboWindow, MaxComboWindowSeconds);
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

    private void UpdateSwimmingVisuals(float dt)
    {
        if (_sprite == null)
        {
            return;
        }

        var speed = Velocity.Length();
        if (speed > MinTurnSpeed && Mathf.Abs(Velocity.X) > 0.01f)
        {
            _sprite.FlipH = Velocity.X < 0.0f;
        }

        var speedRatio = Mathf.Clamp(speed / Mathf.Max(1.0f, Speed), 0.0f, 1.0f);
        var facingSign = _sprite.FlipH ? -1.0f : 1.0f;
        var targetTilt = Mathf.Clamp(Velocity.Y / Mathf.Max(1.0f, Speed), -1.0f, 1.0f)
            * Mathf.DegToRad(MaxTiltDegrees)
            * facingSign;
        var tiltLerpWeight = 1.0f - Mathf.Exp(-TiltResponsiveness * dt);
        _visualTilt = Mathf.Lerp(_visualTilt, targetTilt, tiltLerpWeight);

        _swimTime += dt * BodySwaySpeed * (0.35f + speedRatio);
        var bodySway = Mathf.Sin(_swimTime) * BodySwayAmount * speedRatio;

        _sprite.Rotation = _visualTilt + bodySway;
    }
}
