using Godot;
using System;

/// <summary>
/// Shared behaviour for the game's bosses.
///
/// A boss is a hunter rather than prey: it chases the player instead of fleeing,
/// it cannot be eaten in one bite, and it never despawns. The fight is a loop of
///
///     Chase -> Telegraph -> Attack -> Exhausted -> Chase ...
///
/// Touching the boss hurts the player, EXCEPT while it is Exhausted after an
/// attack: that is the window where a big enough player can bite a chunk of its
/// health off. Subclasses override the hooks to give each boss its own attack.
/// </summary>
public partial class Boss : Area2D
{
    [Signal]
    public delegate void HealthChangedEventHandler(int currentHealth, int maxHealth);

    [Signal]
    public delegate void PhaseChangedEventHandler(int phase);

    [Signal]
    public delegate void DefeatedEventHandler(Vector2 worldPosition);

    public enum BossState
    {
        Chase,
        Telegraph,
        Attack,
        Exhausted,
        Dying
    }

    [Export]
    public string BossTitle { get; set; } = "BOSS";

    [Export]
    public string BossSubtitle { get; set; } = "";

    [Export(PropertyHint.Range, "1,60,1")]
    public int MaxHealth { get; set; } = 8;

    [Export(PropertyHint.Range, "1,10,1")]
    public int ContactDamage { get; set; } = 1;

    /// <summary>The player must be at least this big to bite the boss at all.</summary>
    [Export(PropertyHint.Range, "1,30,1")]
    public int MinimumPlayerSizeToDamage { get; set; } = 4;

    [Export]
    public int FoodReward { get; set; } = 60;

    [Export]
    public float ChaseSpeed { get; set; } = 96.0f;

    [Export]
    public float DashSpeed { get; set; } = 460.0f;

    [Export]
    public float ExhaustedSpeed { get; set; } = 26.0f;

    [Export]
    public float SteeringSmoothing { get; set; } = 3.0f;

    [Export]
    public float TelegraphSeconds { get; set; } = 0.9f;

    [Export]
    public float AttackSeconds { get; set; } = 0.7f;

    [Export]
    public float ExhaustedSeconds { get; set; } = 2.4f;

    [Export]
    public float ChaseSecondsBeforeAttack { get; set; } = 1.8f;

    [Export]
    public float AttackTriggerDistance { get; set; } = 340.0f;

    [Export]
    public float ContactDamageCooldown { get; set; } = 0.6f;

    [Export]
    public float BiteKnockback { get; set; } = 260.0f;

    /// <summary>Grace period after the player lands a bite, so escaping is not a free hit.</summary>
    [Export]
    public float BiteInvulnerabilitySeconds { get; set; } = 0.9f;

    [Export]
    public float HitFlashSeconds { get; set; } = 0.18f;

    /// <summary>Below this health ratio the boss enters its faster second phase.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")]
    public float PhaseTwoHealthRatio { get; set; } = 0.5f;

    [Export]
    public float PhaseTwoSpeedMultiplier { get; set; } = 1.35f;

    [Export]
    public float PhaseTwoAttackRateMultiplier { get; set; } = 0.65f;

    [Export]
    public float DeathAnimationSeconds { get; set; } = 1.2f;

    [Export]
    public float BoundsPadding { get; set; } = 24.0f;

    /// <summary>Set false when the sprite artwork faces left by default.</summary>
    [Export]
    public bool SpriteFacesRight { get; set; } = true;

    [Export]
    public Color VulnerableTint { get; set; } = new Color(0.55f, 1.0f, 0.75f, 1.0f);

    [Export]
    public Color TelegraphTint { get; set; } = new Color(1.0f, 0.72f, 0.72f, 1.0f);

    public int Health { get; private set; }
    public int Phase { get; private set; } = 1;
    public BossState State { get; private set; } = BossState.Chase;
    public bool IsVulnerable => State == BossState.Exhausted;
    public bool IsDying => State == BossState.Dying;

    protected Player TargetPlayer => _player;
    protected World CurrentWorld => _world;
    protected SoundManager Sounds => _soundManager;
    protected AnimatedSprite2D Sprite => _animatedSprite;
    protected RandomNumberGenerator Rng => _rng;

    private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
    private World _world;
    private Player _player;
    private AnimatedSprite2D _animatedSprite;
    private SoundManager _soundManager;
    private Vector2 _direction = Vector2.Right;
    private Vector2 _attackDirection = Vector2.Right;
    private float _stateTimer = 0.0f;
    private float _contactCooldownTimer = 0.0f;
    private float _hitFlashTimer = 0.0f;

    public override void _Ready()
    {
        _rng.Randomize();
        _world = GetParentOrNull<World>();
        _player = _world?.GetNodeOrNull<Player>("Player");
        _animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _soundManager = _world?.GetNodeOrNull<SoundManager>("SoundManager");

        Health = Mathf.Max(1, MaxHealth);
        EnterState(BossState.Chase);
        EmitSignal(SignalName.HealthChanged, Health, Mathf.Max(1, MaxHealth));
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _contactCooldownTimer = Mathf.Max(0.0f, _contactCooldownTimer - dt);
        UpdateHitFlash(dt);

        if (State == BossState.Dying)
        {
            return;
        }

        _stateTimer = Mathf.Max(0.0f, _stateTimer - dt);
        UpdateState(dt);
        ResolvePlayerContact();
        UpdateFacing();
    }

    private void UpdateState(float dt)
    {
        switch (State)
        {
            case BossState.Chase:
                MoveTowardsPlayer(dt, ChaseSpeed * GetPhaseSpeedMultiplier());
                if (_stateTimer <= 0.0f && IsPlayerWithinAttackRange())
                {
                    EnterState(BossState.Telegraph);
                }
                break;

            case BossState.Telegraph:
                // Hold almost still while winding up, so the attack is readable.
                MoveTowardsPlayer(dt, ChaseSpeed * 0.25f);
                _attackDirection = GetDirectionToPlayer();
                OnTelegraphTick(dt);
                if (_stateTimer <= 0.0f)
                {
                    EnterState(BossState.Attack);
                }
                break;

            case BossState.Attack:
                OnAttackTick(dt, _attackDirection);
                if (_stateTimer <= 0.0f)
                {
                    EnterState(BossState.Exhausted);
                }
                break;

            case BossState.Exhausted:
                MoveInDirection(dt, _direction, ExhaustedSpeed);
                if (_stateTimer <= 0.0f)
                {
                    EnterState(BossState.Chase);
                }
                break;
        }
    }

    private void EnterState(BossState nextState)
    {
        State = nextState;

        switch (nextState)
        {
            case BossState.Chase:
                _stateTimer = Mathf.Max(0.1f, ChaseSecondsBeforeAttack * GetPhaseAttackRateMultiplier());
                ApplyStateTint(Colors.White);
                PlayAnimation("swim", "walk", "idle");
                OnChaseStarted();
                break;

            case BossState.Telegraph:
                _stateTimer = Mathf.Max(0.05f, TelegraphSeconds * GetPhaseAttackRateMultiplier());
                ApplyStateTint(TelegraphTint);
                PlayAnimation("idle", "swim");
                OnTelegraphStarted();
                break;

            case BossState.Attack:
                _stateTimer = Mathf.Max(0.05f, AttackSeconds);
                ApplyStateTint(Colors.White);
                PlayAnimation("attack", "walk", "swim");
                OnAttackStarted(_attackDirection);
                break;

            case BossState.Exhausted:
                _stateTimer = Mathf.Max(0.1f, ExhaustedSeconds);
                ApplyStateTint(VulnerableTint);
                PlayAnimation("hurt", "idle", "swim");
                OnExhaustedStarted();
                break;
        }
    }

    /// <summary>Default attack: a straight dash along the locked-in direction.</summary>
    protected virtual void OnAttackTick(float dt, Vector2 attackDirection)
    {
        MoveInDirection(dt, attackDirection, DashSpeed * GetPhaseSpeedMultiplier());
    }

    protected virtual void OnChaseStarted() { }

    protected virtual void OnTelegraphStarted() { }

    /// <summary>Called every physics frame while the boss is winding up an attack.</summary>
    protected virtual void OnTelegraphTick(float dt) { }

    /// <summary>How far the current state is through its timer, from 0 to 1.</summary>
    protected float GetStateProgress(float stateDuration)
    {
        if (stateDuration <= 0.0f)
        {
            return 1.0f;
        }

        return Mathf.Clamp(1.0f - (_stateTimer / stateDuration), 0.0f, 1.0f);
    }

    protected virtual void OnAttackStarted(Vector2 attackDirection) { }

    protected virtual void OnExhaustedStarted() { }

    protected virtual void OnPhaseTwoStarted() { }

    protected virtual void OnDefeated() { }

    protected float GetPhaseSpeedMultiplier()
    {
        return Phase >= 2 ? Mathf.Max(0.1f, PhaseTwoSpeedMultiplier) : 1.0f;
    }

    protected float GetPhaseAttackRateMultiplier()
    {
        return Phase >= 2 ? Mathf.Max(0.05f, PhaseTwoAttackRateMultiplier) : 1.0f;
    }

    protected Vector2 GetDirectionToPlayer()
    {
        if (_player == null)
        {
            return _direction;
        }

        var toPlayer = _player.GlobalPosition - GlobalPosition;
        return toPlayer.LengthSquared() <= 0.0001f ? _direction : toPlayer.Normalized();
    }

    protected void MoveTowardsPlayer(float dt, float speed)
    {
        var targetDirection = GetDirectionToPlayer();
        var lerpWeight = 1.0f - Mathf.Exp(-Mathf.Max(0.01f, SteeringSmoothing) * dt);
        var steered = _direction.Lerp(targetDirection, lerpWeight);

        MoveInDirection(dt, steered.LengthSquared() <= 0.0001f ? targetDirection : steered.Normalized(), speed);
    }

    protected void MoveInDirection(float dt, Vector2 direction, float speed)
    {
        if (direction.LengthSquared() <= 0.0001f)
        {
            return;
        }

        _direction = direction.Normalized();
        GlobalPosition += _direction * speed * dt;
        KeepInsidePlayableArea();
    }

    private bool IsPlayerWithinAttackRange()
    {
        if (_player == null)
        {
            return false;
        }

        return GlobalPosition.DistanceTo(_player.GlobalPosition) <= Mathf.Max(1.0f, AttackTriggerDistance);
    }

    private void ResolvePlayerContact()
    {
        if (_player == null || _player.IsDead || _contactCooldownTimer > 0.0f)
        {
            return;
        }

        var touchesPlayer = false;
        foreach (var body in GetOverlappingBodies())
        {
            if (body == _player)
            {
                touchesPlayer = true;
                break;
            }
        }

        if (!touchesPlayer)
        {
            return;
        }

        if (IsVulnerable && _player.Size >= MinimumPlayerSizeToDamage)
        {
            // The player got a bite in during the recovery window.
            _contactCooldownTimer = Mathf.Max(0.05f, ContactDamageCooldown);

            var awayFromBoss = _player.GlobalPosition - GlobalPosition;
            if (awayFromBoss.LengthSquared() > 0.0001f)
            {
                _player.ApplyKnockback(awayFromBoss.Normalized() * BiteKnockback);
            }

            _player.GrantInvulnerability(BiteInvulnerabilitySeconds);
            TakeDamage(1);
            return;
        }

        if (_player.TakeDamage(ContactDamage, GlobalPosition))
        {
            _contactCooldownTimer = Mathf.Max(0.05f, ContactDamageCooldown);
        }
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || State == BossState.Dying)
        {
            return;
        }

        Health = Math.Max(0, Health - amount);
        _hitFlashTimer = Mathf.Max(0.0f, HitFlashSeconds);
        _soundManager?.PlayEat();
        EmitSignal(SignalName.HealthChanged, Health, Mathf.Max(1, MaxHealth));

        if (Health <= 0)
        {
            Die();
            return;
        }

        if (Phase < 2 && (float)Health / Mathf.Max(1, MaxHealth) <= PhaseTwoHealthRatio)
        {
            Phase = 2;
            EmitSignal(SignalName.PhaseChanged, Phase);
            OnPhaseTwoStarted();
        }

        // A successful bite ends the recovery window, so every hit has to be earned.
        EnterState(BossState.Chase);
    }

    private void Die()
    {
        State = BossState.Dying;
        ApplyStateTint(Colors.White);
        PlayAnimation("death", "hurt", "idle");
        _soundManager?.PlayDeath();
        OnDefeated();

        _player?.EatFood(Mathf.Max(1, FoodReward), GlobalPosition);
        EmitSignal(SignalName.Defeated, GlobalPosition);

        var deathTween = CreateTween();
        deathTween.SetParallel(true);
        deathTween.TweenProperty(this, "modulate:a", 0.0f, DeathAnimationSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        deathTween.TweenProperty(this, "scale", Scale * 1.35f, DeathAnimationSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        deathTween.Finished += QueueFree;
    }

    private void UpdateHitFlash(float dt)
    {
        if (_hitFlashTimer <= 0.0f)
        {
            return;
        }

        _hitFlashTimer = Mathf.Max(0.0f, _hitFlashTimer - dt);
        if (_animatedSprite == null)
        {
            return;
        }

        _animatedSprite.Modulate = _hitFlashTimer > 0.0f
            ? new Color(1.0f, 0.35f, 0.35f, 1.0f)
            : GetTintForState();
    }

    private Color GetTintForState()
    {
        return State switch
        {
            BossState.Telegraph => TelegraphTint,
            BossState.Exhausted => VulnerableTint,
            _ => Colors.White
        };
    }

    private void ApplyStateTint(Color tint)
    {
        if (_animatedSprite == null || _hitFlashTimer > 0.0f)
        {
            return;
        }

        _animatedSprite.Modulate = tint;
    }

    /// <summary>Plays the first of the candidate animations the sprite actually has.</summary>
    protected void PlayAnimation(params string[] candidateNames)
    {
        if (_animatedSprite?.SpriteFrames == null)
        {
            return;
        }

        foreach (var name in candidateNames)
        {
            if (!_animatedSprite.SpriteFrames.HasAnimation(name))
            {
                continue;
            }

            if (_animatedSprite.Animation != name)
            {
                _animatedSprite.Play(name);
            }

            return;
        }
    }

    private void UpdateFacing()
    {
        if (_animatedSprite == null || Mathf.Abs(_direction.X) <= 0.01f)
        {
            return;
        }

        var movingLeft = _direction.X < 0.0f;
        _animatedSprite.FlipH = SpriteFacesRight ? movingLeft : !movingLeft;
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
        GlobalPosition = new Vector2(
            Mathf.Clamp(GlobalPosition.X, playableArea.Position.X + margin, playableArea.End.X - margin),
            Mathf.Clamp(GlobalPosition.Y, playableArea.Position.Y + margin, playableArea.End.Y - margin)
        );
    }
}
