using Godot;

/// <summary>
/// First boss: a goth mermaid who sings you onto the rocks.
///
/// Her wind-up is a siren song that drags the player towards her, so the wail is
/// not just a warning light - it actively fights the player's swimming. Escape it
/// and her lunge misses, leaving her winded and open to a bite.
/// </summary>
public partial class GothMermaid : Boss
{
    [Export]
    public float LureRadius { get; set; } = 420.0f;

    /// <summary>
    /// Pull applied per second at the centre of the song, fading to zero at its edge.
    /// This has to comfortably exceed the player's Acceleration (900 by default),
    /// because the player's own swimming re-accelerates against it every frame.
    /// </summary>
    [Export]
    public float LureStrength { get; set; } = 2400.0f;

    [Export]
    public Color SongColor { get; set; } = new Color(0.78f, 0.33f, 0.86f, 0.55f);

    [Export(PropertyHint.Range, "1,8,1")]
    public int SongRingCount { get; set; } = 3;

    [Export]
    public float PhaseTwoLureMultiplier { get; set; } = 1.4f;

    private bool _isSinging = false;
    private float _songProgress = 0.0f;

    protected override void OnTelegraphStarted()
    {
        _isSinging = true;
        _songProgress = 0.0f;
        Sounds?.PlayCombo();
    }

    protected override void OnTelegraphTick(float dt)
    {
        _songProgress = GetStateProgress(TelegraphSeconds * GetPhaseAttackRateMultiplier());
        QueueRedraw();

        var player = TargetPlayer;
        if (player == null || player.IsDead)
        {
            return;
        }

        var toMermaid = GlobalPosition - player.GlobalPosition;
        var distance = toMermaid.Length();
        if (distance <= 0.001f || distance >= LureRadius)
        {
            return;
        }

        // Strongest at the centre of the song, nothing at all at its edge.
        var falloff = 1.0f - (distance / LureRadius);
        var pull = LureStrength * falloff * (Phase >= 2 ? PhaseTwoLureMultiplier : 1.0f);
        player.ApplyKnockback(toMermaid.Normalized() * pull * dt);
    }

    protected override void OnAttackStarted(Vector2 attackDirection)
    {
        StopSinging();
    }

    protected override void OnChaseStarted()
    {
        StopSinging();
    }

    protected override void OnDefeated()
    {
        StopSinging();
    }

    protected override void OnPhaseTwoStarted()
    {
        // Wounded, she sings louder and lunges more often.
        LureRadius *= 1.15f;
    }

    private void StopSinging()
    {
        if (!_isSinging)
        {
            return;
        }

        _isSinging = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_isSinging)
        {
            return;
        }

        // Rings sweeping outwards, so the pull has a shape the player can read.
        var ringCount = Mathf.Max(1, SongRingCount);
        for (var i = 0; i < ringCount; i++)
        {
            var ringPhase = Mathf.PosMod(_songProgress + (float)i / ringCount, 1.0f);
            var radius = LureRadius * ringPhase;
            if (radius <= 1.0f)
            {
                continue;
            }

            var color = SongColor;
            color.A *= 1.0f - ringPhase;
            DrawArc(Vector2.Zero, radius, 0.0f, Mathf.Tau, 64, color, 2.0f, true);
        }
    }
}
