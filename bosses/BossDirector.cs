using Godot;

/// <summary>
/// Decides when a boss turns up, spawns it, and relays its state to the world.
///
/// Bosses gate on two things: how big the player has grown, and how deep they have
/// swum. That keeps a size-3 player from stumbling into the kraken on their way
/// past, and gives a reason to go deeper once the shallows are easy.
///
/// Only one boss is ever alive at a time.
/// </summary>
public partial class BossDirector : Node
{
    [Signal]
    public delegate void BossEncounterStartedEventHandler(string title, string subtitle, int maxHealth);

    [Signal]
    public delegate void BossHealthChangedEventHandler(int currentHealth, int maxHealth);

    [Signal]
    public delegate void BossDefeatedEventHandler(string title, bool wasFinalBoss);

    [Signal]
    public delegate void BossEncounterEndedEventHandler();

    [Export]
    public PackedScene GothMermaidScene { get; set; }

    [Export(PropertyHint.Range, "1,30,1")]
    public int GothMermaidTriggerSize { get; set; } = 5;

    [Export(PropertyHint.Range, "0,20000,1")]
    public float GothMermaidTriggerDepth { get; set; } = 1200.0f;

    [Export]
    public PackedScene KrakenScene { get; set; }

    [Export(PropertyHint.Range, "1,30,1")]
    public int KrakenTriggerSize { get; set; } = 9;

    [Export(PropertyHint.Range, "0,20000,1")]
    public float KrakenTriggerDepth { get; set; } = 3200.0f;

    /// <summary>The kraken stays in the dark until the mermaid has been dealt with.</summary>
    [Export]
    public bool KrakenRequiresMermaidDefeated { get; set; } = true;

    [Export(PropertyHint.Range, "100,3000,10")]
    public float SpawnDistanceFromPlayer { get; set; } = 560.0f;

    [Export(PropertyHint.Range, "0,60,0.5")]
    public float SecondsBetweenEncounters { get; set; } = 6.0f;

    [Export]
    public bool Enabled { get; set; } = true;

    public bool IsBossActive => _activeBoss != null && IsInstanceValid(_activeBoss);
    public bool MermaidDefeated { get; private set; } = false;
    public bool KrakenDefeated { get; private set; } = false;

    private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
    private World _world;
    private Player _player;
    private Boss _activeBoss;
    private float _cooldownTimer = 0.0f;

    public override void _Ready()
    {
        _rng.Randomize();
        _world = GetParentOrNull<World>();
        _player = _world?.GetNodeOrNull<Player>("Player");
    }

    public override void _Process(double delta)
    {
        if (!Enabled || Engine.IsEditorHint() || _world == null || _player == null)
        {
            return;
        }

        _cooldownTimer = Mathf.Max(0.0f, _cooldownTimer - (float)delta);

        if (IsBossActive || _cooldownTimer > 0.0f || _player.IsDead)
        {
            return;
        }

        TrySpawnNextBoss();
    }

    private void TrySpawnNextBoss()
    {
        var depth = GetPlayerDepth();

        if (!KrakenDefeated
            && (MermaidDefeated || !KrakenRequiresMermaidDefeated)
            && _player.Size >= KrakenTriggerSize
            && depth >= KrakenTriggerDepth)
        {
            SpawnBoss(KrakenScene, isFinalBoss: true);
            return;
        }

        if (!MermaidDefeated
            && _player.Size >= GothMermaidTriggerSize
            && depth >= GothMermaidTriggerDepth)
        {
            SpawnBoss(GothMermaidScene, isFinalBoss: false);
        }
    }

    private void SpawnBoss(PackedScene bossScene, bool isFinalBoss)
    {
        if (bossScene == null)
        {
            GD.PrintErr($"BossDirector: no scene assigned for the {(isFinalBoss ? "kraken" : "goth mermaid")}.");
            // Do not retry every frame when a scene slot is left empty.
            _cooldownTimer = 30.0f;
            return;
        }

        var boss = bossScene.Instantiate<Boss>();
        if (boss == null)
        {
            GD.PrintErr("BossDirector: boss scene root must use a Boss script.");
            _cooldownTimer = 30.0f;
            return;
        }

        boss.Position = GetSpawnPosition();
        boss.HealthChanged += OnBossHealthChanged;
        boss.Defeated += position => OnBossDefeated(boss, isFinalBoss, position);
        boss.TreeExited += OnBossTreeExited;

        _activeBoss = boss;
        _world.AddChild(boss);

        EmitSignal(SignalName.BossEncounterStarted, boss.BossTitle, boss.BossSubtitle, boss.MaxHealth);
        EmitSignal(SignalName.BossHealthChanged, boss.Health, boss.MaxHealth);
    }

    private void OnBossHealthChanged(int currentHealth, int maxHealth)
    {
        EmitSignal(SignalName.BossHealthChanged, currentHealth, maxHealth);
    }

    private void OnBossDefeated(Boss boss, bool isFinalBoss, Vector2 worldPosition)
    {
        if (isFinalBoss)
        {
            KrakenDefeated = true;
        }
        else
        {
            MermaidDefeated = true;
        }

        _cooldownTimer = Mathf.Max(0.0f, SecondsBetweenEncounters);
        EmitSignal(SignalName.BossDefeated, boss.BossTitle, isFinalBoss);
    }

    private void OnBossTreeExited()
    {
        _activeBoss = null;
        EmitSignal(SignalName.BossEncounterEnded);
    }

    /// <summary>Clears the current fight and re-arms every encounter, for a fresh run.</summary>
    public void ResetEncounters()
    {
        if (IsBossActive)
        {
            _activeBoss.QueueFree();
        }

        _activeBoss = null;
        MermaidDefeated = false;
        KrakenDefeated = false;
        _cooldownTimer = Mathf.Max(0.0f, SecondsBetweenEncounters);
        EmitSignal(SignalName.BossEncounterEnded);
    }

    private float GetPlayerDepth()
    {
        if (_player == null)
        {
            return 0.0f;
        }

        var playableArea = _world.GetPlayableArea();
        return Mathf.Max(0.0f, _player.GlobalPosition.Y - playableArea.Position.Y);
    }

    /// <summary>Picks a point a fixed distance from the player, kept inside the world.</summary>
    private Vector2 GetSpawnPosition()
    {
        var playableArea = _world.GetPlayableArea();
        var distance = Mathf.Max(1.0f, SpawnDistanceFromPlayer);

        for (var attempt = 0; attempt < 12; attempt++)
        {
            var angle = _rng.RandfRange(0.0f, Mathf.Tau);
            var candidate = _player.GlobalPosition + Vector2.Right.Rotated(angle) * distance;
            if (playableArea.HasPoint(candidate))
            {
                return candidate;
            }
        }

        // Fall back to just above the player, clamped into the world.
        var fallback = _player.GlobalPosition + new Vector2(0.0f, -distance);
        return new Vector2(
            Mathf.Clamp(fallback.X, playableArea.Position.X, playableArea.End.X),
            Mathf.Clamp(fallback.Y, playableArea.Position.Y, playableArea.End.Y)
        );
    }
}
