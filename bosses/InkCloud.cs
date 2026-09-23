using Godot;

/// <summary>
/// A blot of ink the kraken leaves behind. It expands, lingers, then fades.
/// While the player is inside it they are dragged to a crawl, which is what makes
/// the kraken's follow-up dash so hard to dodge.
///
/// This is spawned from code (see <see cref="Kraken"/>), so it needs no scene file.
/// </summary>
public partial class InkCloud : Node2D
{
    [Export]
    public float MaxRadius { get; set; } = 150.0f;

    [Export]
    public float GrowSeconds { get; set; } = 0.35f;

    [Export]
    public float HoldSeconds { get; set; } = 2.6f;

    [Export]
    public float FadeSeconds { get; set; } = 1.0f;

    /// <summary>
    /// Fraction of the player's speed bled off per second inside the cloud. Like the
    /// mermaid's song this fights the player's own acceleration, so it is set high
    /// enough that swimming inside the ink is a crawl rather than a mild slowdown.
    /// </summary>
    [Export]
    public float DragStrength { get; set; } = 8.0f;

    [Export]
    public Color InkColor { get; set; } = new Color(0.05f, 0.02f, 0.12f, 0.82f);

    private Player _player;
    private float _age = 0.0f;
    private float _radius = 0.0f;

    public override void _Ready()
    {
        _player = GetParentOrNull<World>()?.GetNodeOrNull<Player>("Player");
        ZIndex = 1;
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _age += dt;

        var totalLifetime = GrowSeconds + HoldSeconds + FadeSeconds;
        if (_age >= totalLifetime)
        {
            QueueFree();
            return;
        }

        _radius = _age < GrowSeconds
            ? MaxRadius * Mathf.Clamp(_age / Mathf.Max(0.01f, GrowSeconds), 0.0f, 1.0f)
            : MaxRadius;

        DragPlayer(dt);
        QueueRedraw();
    }

    private void DragPlayer(float dt)
    {
        if (_player == null || _player.IsDead || _radius <= 0.0f)
        {
            return;
        }

        if (_player.GlobalPosition.DistanceTo(GlobalPosition) > _radius)
        {
            return;
        }

        // Bleed off speed rather than teleporting the player: thick water, not a wall.
        var dragFactor = Mathf.Clamp(DragStrength * dt, 0.0f, 1.0f);
        _player.ApplyKnockback(-_player.Velocity * dragFactor);
    }

    public override void _Draw()
    {
        if (_radius <= 0.0f)
        {
            return;
        }

        var color = InkColor;
        var fadeStart = GrowSeconds + HoldSeconds;
        if (_age > fadeStart)
        {
            var fadeProgress = Mathf.Clamp((_age - fadeStart) / Mathf.Max(0.01f, FadeSeconds), 0.0f, 1.0f);
            color.A *= 1.0f - fadeProgress;
        }

        DrawCircle(Vector2.Zero, _radius, color);
        DrawArc(Vector2.Zero, _radius, 0.0f, Mathf.Tau, 48, new Color(color, color.A * 0.5f), 3.0f, true);
    }
}
