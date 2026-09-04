using Godot;

/// <summary>
/// Final boss: the kraken in the abyss.
///
/// Where the mermaid pulls you in, the kraken takes away your escape. Every lunge
/// starts by dumping a cloud of ink that drags anything inside it to a crawl, and
/// the lunge itself homes, so the dodge has to be made before the ink lands.
/// </summary>
public partial class Kraken : Boss
{
    [Export]
    public PackedScene InkCloudScene { get; set; }

    [Export]
    public float InkCloudRadius { get; set; } = 170.0f;

    /// <summary>How hard the dash curves towards the player, in radians per second.</summary>
    [Export]
    public float DashHomingStrength { get; set; } = 1.6f;

    [Export]
    public float PhaseTwoInkRadiusMultiplier { get; set; } = 1.4f;

    /// <summary>In phase two it also inks while winding up, not only when it lunges.</summary>
    [Export]
    public bool InkOnTelegraphInPhaseTwo { get; set; } = true;

    private Vector2 _dashDirection = Vector2.Right;

    protected override void OnTelegraphStarted()
    {
        if (Phase >= 2 && InkOnTelegraphInPhaseTwo)
        {
            SpawnInkCloud(GlobalPosition);
        }
    }

    protected override void OnAttackStarted(Vector2 attackDirection)
    {
        _dashDirection = attackDirection;
        SpawnInkCloud(GlobalPosition);
        Sounds?.PlayFlee();
    }

    protected override void OnAttackTick(float dt, Vector2 attackDirection)
    {
        // Curve towards the player mid-dash instead of committing to a straight line.
        var desired = GetDirectionToPlayer();
        var maxTurn = Mathf.Max(0.0f, DashHomingStrength) * dt;
        var angleToDesired = _dashDirection.AngleTo(desired);
        var turn = Mathf.Clamp(angleToDesired, -maxTurn, maxTurn);
        _dashDirection = _dashDirection.Rotated(turn).Normalized();

        MoveInDirection(dt, _dashDirection, DashSpeed * GetPhaseSpeedMultiplier());
    }

    protected override void OnPhaseTwoStarted()
    {
        InkCloudRadius *= PhaseTwoInkRadiusMultiplier;
        SpawnInkCloud(GlobalPosition);
    }

    private void SpawnInkCloud(Vector2 worldPosition)
    {
        var parent = GetParent();
        if (parent == null)
        {
            return;
        }

        var cloud = InkCloudScene != null
            ? InkCloudScene.Instantiate<InkCloud>()
            : new InkCloud();

        if (cloud == null)
        {
            return;
        }

        cloud.MaxRadius = Mathf.Max(1.0f, InkCloudRadius);

        // Position is set in the parent's space because the cloud is not in the
        // tree yet, so GlobalPosition would have nothing to resolve against.
        cloud.Position = parent is Node2D parentNode2D ? parentNode2D.ToLocal(worldPosition) : worldPosition;

        // Deferred: this can run from inside a physics callback.
        parent.CallDeferred(Node.MethodName.AddChild, cloud);
    }
}
