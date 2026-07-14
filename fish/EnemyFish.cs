using Godot;
using System;

public partial class EnemyFish : Area2D
{
    [Export]
    public int Size { get; set; } = 1;

    [Export]
    public int FoodValue { get; set; } = 1;

    [Export]
    public float Speed { get; set; } = 72.0f;

    [Export]
    public float WanderTurnIntervalMin { get; set; } = 2.0f;

    [Export]
    public float WanderTurnIntervalMax { get; set; } = 4.0f;

    [Export]
    public float DirectionSmoothing { get; set; } = 2.2f;

    [Export]
    public float AvoidStrengthMultiplier { get; set; } = 1.8f;

    [Export]
    public float AvoidSpeedBoostMultiplier { get; set; } = 1.45f;

    [Export]
    public float AvoidSpeedBoostDuration { get; set; } = 0.45f;

    [Export]
    public float AvoidSpeedBoostCooldown { get; set; } = 1.4f;

    [Export]
    public float BoundsPadding { get; set; } = 16.0f;

    [Export]
    public float BoundarySteeringDistance { get; set; } = 96.0f;

    [Export]
    public float BoundarySteeringStrength { get; set; } = 2.5f;

    [Export]
    public float IdlePauseChance { get; set; } = 0.28f;

    [Export]
    public float IdlePauseMinSeconds { get; set; } = 0.35f;

    [Export]
    public float IdlePauseMaxSeconds { get; set; } = 1.1f;

    private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
    private World _world;
    private Player _player;
    private AnimatedSprite2D _animatedSprite;
    private Area2D _avoidanceSensor;
    private Vector2 _direction = Vector2.Right;
    private float _wanderTimer = 0.0f;
    private float _idlePauseTimer = 0.0f;
    private float _avoidSpeedBoostTimer = 0.0f;
    private float _avoidSpeedBoostCooldownTimer = 0.0f;
    private bool _playerInAvoidanceRange = false;

    public override void _Ready()
    {
        _rng.Randomize();
        _world = GetParentOrNull<World>();
        _player = _world?.GetNodeOrNull<Player>("Player");
        _animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _avoidanceSensor = GetNodeOrNull<Area2D>("AvoidanceSensor");
        _direction = GetRandomDirection();
        ResetWanderTimer();
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;

        _avoidSpeedBoostTimer = Mathf.Max(0.0f, _avoidSpeedBoostTimer - dt);
        _avoidSpeedBoostCooldownTimer = Mathf.Max(0.0f, _avoidSpeedBoostCooldownTimer - dt);

        if (UpdateIdlePause(dt))
        {
            UpdateVisualFacing();
            return;
        }

        var targetDirection = GetSteeringDirection(dt);
        var lerpWeight = 1.0f - Mathf.Exp(-Mathf.Max(0.01f, DirectionSmoothing) * dt);
        _direction = _direction.Lerp(targetDirection, lerpWeight).Normalized();

        if (_direction.LengthSquared() <= 0.0001f)
        {
            _direction = GetRandomDirection();
        }

        var movementSpeed = GetCurrentMovementSpeed();
        GlobalPosition += _direction * movementSpeed * dt;
        KeepInsidePlayableArea();
        UpdateVisualFacing();
    }

    // Add on body entered signal to detect collision with player
    private void _on_body_entered(Node body)
    {
        if (body is Player player)
        {
            if (Size > player.Size)
            {
                // Enemy fish is bigger than player, player loses
                GD.Print("Player has been eaten!");
                // You can add logic to reset the game or reduce player's size here
            }
            else
            {
                // Player is bigger than enemy fish, player eats the enemy fish
                GD.Print("Enemy fish has been eaten!");
                player.EatFood(FoodValue, GlobalPosition);
                QueueFree(); // Remove the enemy fish from the scene
                // You can add logic to increase player's size here
            }
        }
    }

    private Vector2 GetSteeringDirection(float dt)
    {
        UpdateWanderDirection(dt);

        var steering = _direction;
        steering += GetBoundsSteering();
        steering += GetAvoidanceSteering();

        if (steering.LengthSquared() <= 0.0001f)
        {
            return GetRandomDirection();
        }

        return steering.Normalized();
    }

    private void UpdateWanderDirection(float dt)
    {
        if (_idlePauseTimer > 0.0f)
        {
            return;
        }

        _wanderTimer -= dt;
        if (_wanderTimer > 0.0f)
        {
            return;
        }

        if (TryStartIdlePause())
        {
            return;
        }

        _direction = (_direction + GetRandomDirection() * 0.65f).Normalized();
        if (_direction.LengthSquared() <= 0.0001f)
        {
            _direction = GetRandomDirection();
        }

        ResetWanderTimer();
    }

    private bool TryStartIdlePause()
    {
        if (_rng.Randf() > Mathf.Clamp(IdlePauseChance, 0.0f, 1.0f))
        {
            return false;
        }

        var minPause = Mathf.Max(0.0f, IdlePauseMinSeconds);
        var maxPause = Mathf.Max(minPause, IdlePauseMaxSeconds);
        _idlePauseTimer = _rng.RandfRange(minPause, maxPause);
        ResetWanderTimer();
        return true;
    }

    private bool UpdateIdlePause(float dt)
    {
        if (_idlePauseTimer <= 0.0f)
        {
            return false;
        }

        _idlePauseTimer = Mathf.Max(0.0f, _idlePauseTimer - dt);
        return true;
    }

    private void ResetWanderTimer()
    {
        var minInterval = Mathf.Max(0.2f, WanderTurnIntervalMin);
        var maxInterval = Mathf.Max(minInterval, WanderTurnIntervalMax);
        _wanderTimer = _rng.RandfRange(minInterval, maxInterval);
    }

    private Vector2 GetAvoidanceSteering()
    {
        if (_player == null || !_playerInAvoidanceRange)
        {
            return Vector2.Zero;
        }

        var offsetFromPlayer = GlobalPosition - _player.GlobalPosition;
        var distance = offsetFromPlayer.Length();
        var avoidanceRadius = GetAvoidanceRadius();
        if (distance <= 0.001f || distance >= avoidanceRadius)
        {
            return Vector2.Zero;
        }

        var strength = 1.0f - (distance / Mathf.Max(1.0f, avoidanceRadius));
        TryTriggerAvoidSpeedBoost();
        return offsetFromPlayer.Normalized() * strength * Mathf.Max(0.0f, AvoidStrengthMultiplier);
    }

    private void TryTriggerAvoidSpeedBoost()
    {
        if (_avoidSpeedBoostTimer > 0.0f || _avoidSpeedBoostCooldownTimer > 0.0f)
        {
            return;
        }

        var duration = Mathf.Max(0.0f, AvoidSpeedBoostDuration);
        if (duration <= 0.0f)
        {
            return;
        }

        _avoidSpeedBoostTimer = duration;
        _avoidSpeedBoostCooldownTimer = Mathf.Max(0.0f, AvoidSpeedBoostCooldown);
    }

    private float GetCurrentMovementSpeed()
    {
        var baseSpeed = Mathf.Max(0.0f, Speed);
        if (_avoidSpeedBoostTimer <= 0.0f)
        {
            return baseSpeed;
        }

        var boostMultiplier = Mathf.Max(1.0f, AvoidSpeedBoostMultiplier);
        return baseSpeed * boostMultiplier;
    }

    private float GetAvoidanceRadius()
    {
        if (_avoidanceSensor == null)
        {
            return 0.0f;
        }

        var sensorShapeNode = _avoidanceSensor.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (sensorShapeNode?.Shape is CircleShape2D circleShape)
        {
            return circleShape.Radius * _avoidanceSensor.GlobalScale.X;
        }

        return 0.0f;
    }

    private Vector2 GetBoundsSteering()
    {
        if (_world == null)
        {
            return Vector2.Zero;
        }

        var playableArea = _world.GetPlayableArea();
        if (playableArea.Size == Vector2.Zero)
        {
            return Vector2.Zero;
        }

        var margin = Mathf.Max(0.0f, BoundsPadding);
        var minX = playableArea.Position.X + margin;
        var maxX = playableArea.End.X - margin;
        var minY = playableArea.Position.Y + margin;
        var maxY = playableArea.End.Y - margin;
        var boundaryDistance = Mathf.Max(16.0f, BoundarySteeringDistance);
        var steer = Vector2.Zero;

        if (GlobalPosition.X < minX)
        {
            steer.X += 1.0f + (minX - GlobalPosition.X) / boundaryDistance;
        }
        else if (GlobalPosition.X < minX + boundaryDistance)
        {
            steer.X += 1.0f - (GlobalPosition.X - minX) / boundaryDistance;
        }
        else if (GlobalPosition.X > maxX)
        {
            steer.X -= 1.0f + (GlobalPosition.X - maxX) / boundaryDistance;
        }
        else if (GlobalPosition.X > maxX - boundaryDistance)
        {
            steer.X -= 1.0f - (maxX - GlobalPosition.X) / boundaryDistance;
        }

        if (GlobalPosition.Y < minY)
        {
            steer.Y += 1.0f + (minY - GlobalPosition.Y) / boundaryDistance;
        }
        else if (GlobalPosition.Y < minY + boundaryDistance)
        {
            steer.Y += 1.0f - (GlobalPosition.Y - minY) / boundaryDistance;
        }
        else if (GlobalPosition.Y > maxY)
        {
            steer.Y -= 1.0f + (GlobalPosition.Y - maxY) / boundaryDistance;
        }
        else if (GlobalPosition.Y > maxY - boundaryDistance)
        {
            steer.Y -= 1.0f - (maxY - GlobalPosition.Y) / boundaryDistance;
        }

        if (steer.LengthSquared() <= 0.0001f)
        {
            return Vector2.Zero;
        }

        return steer.Normalized() * Mathf.Max(0.0f, BoundarySteeringStrength);
    }

    private void KeepInsidePlayableArea()
    {
        if (_world == null)
        {
            return;
        }

        var playableArea = _world.GetPlayableArea();
        if (playableArea.Size == Vector2.Zero)
        {
            return;
        }

        var margin = Mathf.Max(0.0f, BoundsPadding);
        var clampedPosition = new Vector2(
            Mathf.Clamp(GlobalPosition.X, playableArea.Position.X + margin, playableArea.End.X - margin),
            Mathf.Clamp(GlobalPosition.Y, playableArea.Position.Y + margin, playableArea.End.Y - margin)
        );

        var wasClamped = clampedPosition != GlobalPosition;
        GlobalPosition = clampedPosition;

        if (wasClamped)
        {
            var inwardBias = (playableArea.GetCenter() - GlobalPosition).Normalized();
            if (inwardBias.LengthSquared() > 0.0001f)
            {
                _direction = (_direction + inwardBias * 0.5f).Normalized();
            }
        }
    }

    private void UpdateVisualFacing()
    {
        if (_animatedSprite == null || Mathf.Abs(_direction.X) <= 0.01f)
        {
            return;
        }

        _animatedSprite.FlipH = _direction.X < 0.0f;
    }

    private Vector2 GetRandomDirection()
    {
        var angle = _rng.RandfRange(0.0f, Mathf.Tau);
        return Vector2.Right.Rotated(angle);
    }

    private void _on_avoidance_sensor_body_entered(Node body)
    {
        if (_player != null && body == _player)
        {
            _playerInAvoidanceRange = true;
        }
    }

    private void _on_avoidance_sensor_body_exited(Node body)
    {
        if (_player != null && body == _player)
        {
            _playerInAvoidanceRange = false;
        }
    }
}
