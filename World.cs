using Godot;

[Tool]
public partial class World : Node2D
{
    [Export]
    public PackedScene FishScene;

    [Export]
    public int FishCount = 15;

    [Export]
    public Vector2 SpawnAreaMin
    {
        get => _spawnAreaMin;
        set
        {
            _spawnAreaMin = value;
            UpdateBackgroundBounds();
            QueueRedraw();
        }
    }

    [Export]
    public Vector2 SpawnAreaMax
    {
        get => _spawnAreaMax;
        set
        {
            _spawnAreaMax = value;
            UpdateBackgroundBounds();
            QueueRedraw();
        }
    }

    [Export]
    public Color SpawnBorderColor = new Color(0.2f, 0.8f, 1.0f, 0.9f);

    [Export(PropertyHint.Range, "1,12,1")]
    public int SpawnBorderWidth = 2;

    [Export(PropertyHint.Range, "0,5000,1")]
    public float BackgroundHorizontalOverflow = 400f;

    [Export(PropertyHint.Range, "0,4000,1")]
    public float RespawnNearPlayerMinDistance = 260f;

    [Export(PropertyHint.Range, "0,6000,1")]
    public float RespawnNearPlayerMaxDistance = 520f;

    [Export(PropertyHint.Range, "0,1500,1")]
    public float RespawnOutOfSightMargin = 64f;

    [Export(PropertyHint.Range, "1,50,1")]
    public int RespawnSearchAttempts = 16;

    [Export(PropertyHint.Range, "1,20,1")]
    public int MaxFishLevel = 6; // TODO: maybe this should be handled by the fish script itself

    [Export(PropertyHint.Range, "0,20000,1")]
    public float LevelDepthStart = 0.0f;

    [Export(PropertyHint.Range, "100,10000,1")]
    public float LevelDepthRangeSize = 2000.0f;

    [Export(PropertyHint.Range, "1,10000,1")]
    public float LevelDepthStep = 750.0f;

    [Export]
    public bool DebugEnabled
    {
        get => _debugEnabled;
        set
        {
            _debugEnabled = value;
            QueueRedraw();
        }
    }

    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    private Vector2 _spawnAreaMin = new Vector2(0, 0);
    private Vector2 _spawnAreaMax = new Vector2(2000, 6000);
    private Player _player;
    private GameUI _ui;
    private SoundManager _soundManager;
    private bool _debugEnabled = false;

    public override void _Ready()
    {
        QueueRedraw();

        EnsureBackgroundNode();
        UpdateBackgroundBounds();

        _player = GetNodeOrNull<Player>("Player");
        _ui = GetNodeOrNull<GameUI>("CanvasLayer/UI");
        _soundManager = GetNodeOrNull<SoundManager>("SoundManager");

        if (_player != null)
        {
            _player.StatsChanged += OnPlayerStatsChanged;
            _player.FoodGained += OnPlayerFoodGained;
            _player.Grew += OnPlayerGrew;
            _player.ComboTriggered += OnPlayerComboTriggered;
        }

        if (_soundManager != null)
        {
            _soundManager.MuteChanged += OnMuteChanged;
        }

        UpdateHud();
        _ui?.SetMuted(_soundManager?.IsMuted == true);

        if (Engine.IsEditorHint())
        {
            return;
        }

        Input.MouseMode = Input.MouseModeEnum.Hidden;

        _soundManager?.StartLoops();

        _rng.Randomize();

        for (int i = 0; i < FishCount; i++)
        {
            SpawnFish();
        }

        LogAliveFishCount("Initial spawn complete");
    }

    public override void _ExitTree()
    {
        if (!Engine.IsEditorHint())
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        if (_player != null)
        {
            _player.StatsChanged -= OnPlayerStatsChanged;
            _player.FoodGained -= OnPlayerFoodGained;
            _player.Grew -= OnPlayerGrew;
            _player.ComboTriggered -= OnPlayerComboTriggered;
        }

        if (_soundManager != null)
        {
            _soundManager.MuteChanged -= OnMuteChanged;
        }
    }

    public override void _Draw()
    {
        if (!DebugEnabled)
        {
            return;
        }

        var playableArea = GetPlayableArea();
        var areaPosition = playableArea.Position;
        var areaSize = playableArea.Size;

        if (areaSize == Vector2.Zero)
        {
            return;
        }

        DrawRect(new Rect2(areaPosition, areaSize), SpawnBorderColor, false, SpawnBorderWidth);
    }

    public Rect2 GetPlayableArea()
    {
        var areaPosition = new Vector2(
            Mathf.Min(SpawnAreaMin.X, SpawnAreaMax.X),
            Mathf.Min(SpawnAreaMin.Y, SpawnAreaMax.Y)
        );
        var areaSize = new Vector2(
            Mathf.Abs(SpawnAreaMax.X - SpawnAreaMin.X),
            Mathf.Abs(SpawnAreaMax.Y - SpawnAreaMin.Y)
        );

        return new Rect2(areaPosition, areaSize);
    }

    public bool IsDebugEnabled()
    {
        return DebugEnabled;
    }

    private void EnsureBackgroundNode()
    {
        if (GetNodeOrNull<Background>("Background") != null)
        {
            return;
        }

        var background = new Background
        {
            Name = "Background",
        };

        AddChild(background);
        MoveChild(background, 0);
    }

    private void UpdateBackgroundBounds()
    {
        var background = GetNodeOrNull<Background>("Background");
        if (background == null)
        {
            return;
        }

        var playableArea = GetPlayableArea();
        var horizontalOverflow = Mathf.Max(0f, BackgroundHorizontalOverflow);

        background.AreaMin = new Vector2(playableArea.Position.X - horizontalOverflow, playableArea.Position.Y);
        background.AreaMax = new Vector2(playableArea.End.X + horizontalOverflow, playableArea.End.Y);
        background.QueueRedraw();
    }

    private void SpawnFish()
    {
        if (FishScene == null)
        {
            GD.PrintErr("World: FishScene is not assigned.");
            return;
        }

        var fish = FishScene.Instantiate<EnemyFish>();
        if (fish == null)
        {
            GD.PrintErr("World: FishScene must be an EnemyFish scene.");
            return;
        }

        var spawnPosition = GetFishSpawnPosition();
        fish.Position = spawnPosition;

        var fishLevel = GetFishLevelForDepth(spawnPosition.Y);
        ConfigureFishForLevel(fish, fishLevel);

        AddChild(fish);

        // When this fish leaves the tree (eaten or otherwise freed), spawn a replacement
        fish.TreeExited += OnFishTreeExited;
    }

    // Select a fish level from every depth band that contains this position.
    // Bands may overlap when the range is wider than the step, so choose among
    // all matches to keep the level distribution varied at those boundaries.
    private int GetFishLevelForDepth(float depthY)
    {
        var maxLevel = Mathf.Max(1, MaxFishLevel);
        var depthStart = LevelDepthStart;
        var depthRangeSize = Mathf.Max(1.0f, LevelDepthRangeSize);
        var depthStep = Mathf.Max(1.0f, LevelDepthStep);

        var matchingLevels = new System.Collections.Generic.List<int>(maxLevel);
        for (var level = 1; level <= maxLevel; level++)
        {
            var bandStart = depthStart + (level - 1) * depthStep;
            var bandEnd = bandStart + depthRangeSize;
            if (depthY >= bandStart && depthY <= bandEnd)
            {
                matchingLevels.Add(level);
            }
        }

        if (matchingLevels.Count > 0)
        {
            return matchingLevels[_rng.RandiRange(0, matchingLevels.Count - 1)];
        }

        // Keep positions above the configured depth at the smallest level and
        // positions below all bands at the largest configured level.
        if (depthY < depthStart)
        {
            return 1;
        }

        return maxLevel;
    }

    private void ConfigureFishForLevel(EnemyFish fish, int level)
    {
        if (fish == null)
        {
            return;
        }

        var clampedLevel = Mathf.Clamp(level, 1, Mathf.Max(1, MaxFishLevel));
        fish.Size = clampedLevel;
        fish.FoodValue = clampedLevel;
    }

    private void OnFishTreeExited()
    {
        if (!IsInsideTree() || Engine.IsEditorHint())
        {
            return;
        }

        LogAliveFishCount("Fish removed");
        SpawnFish();
        LogAliveFishCount("Replacement fish spawned");
    }

    private int CountAliveFish()
    {
        var aliveCount = 0;
        foreach (var child in GetChildren())
        {
            if (child is EnemyFish)
            {
                aliveCount++;
            }
        }

        return aliveCount;
    }

    private void LogAliveFishCount(string context)
    {
        if (!DebugEnabled)
        {
            return;
        }

        GD.Print($"World: Alive fish count = {CountAliveFish()} ({context})");
    }

    private Vector2 GetFishSpawnPosition()
    {
        if (!Engine.IsEditorHint() && !DebugEnabled && _player != null && TryGetOutOfSightSpawnPosition(out var outOfSightPosition))
        {
            return outOfSightPosition;
        }

        return GetRandomSpawnPositionInPlayableArea();
    }

    private Vector2 GetRandomSpawnPositionInPlayableArea()
    {
        var playableArea = GetPlayableArea();
        if (playableArea.Size == Vector2.Zero)
        {
            return Vector2.Zero;
        }

        return new Vector2(
            _rng.RandfRange(playableArea.Position.X, playableArea.End.X),
            _rng.RandfRange(playableArea.Position.Y, playableArea.End.Y)
        );
    }

    private bool TryGetOutOfSightSpawnPosition(out Vector2 spawnPosition)
    {
        spawnPosition = Vector2.Zero;

        if (_player == null)
        {
            return false;
        }

        var playableArea = GetPlayableArea();
        if (playableArea.Size == Vector2.Zero)
        {
            return false;
        }

        var visibleWorldRect = GetVisibleWorldRect().Grow(Mathf.Max(0.0f, RespawnOutOfSightMargin));
        var minDistance = Mathf.Max(0.0f, RespawnNearPlayerMinDistance);
        var maxDistance = Mathf.Max(minDistance + 1.0f, RespawnNearPlayerMaxDistance);
        var attempts = Mathf.Max(1, RespawnSearchAttempts);

        for (var i = 0; i < attempts; i++)
        {
            var angle = _rng.RandfRange(0.0f, Mathf.Tau);
            var distance = _rng.RandfRange(minDistance, maxDistance);
            var candidate = _player.GlobalPosition + Vector2.Right.Rotated(angle) * distance;

            if (!playableArea.HasPoint(candidate))
            {
                continue;
            }

            if (visibleWorldRect.HasPoint(candidate))
            {
                continue;
            }

            spawnPosition = candidate;
            return true;
        }

        for (var i = 0; i < attempts; i++)
        {
            var candidate = GetRandomSpawnPositionInPlayableArea();
            if (!visibleWorldRect.HasPoint(candidate))
            {
                spawnPosition = candidate;
                return true;
            }
        }

        return false;
    }

    private Rect2 GetVisibleWorldRect()
    {
        var viewport = GetViewport();
        if (viewport == null)
        {
            return new Rect2(Vector2.Zero, Vector2.Zero);
        }

        var visibleScreenRect = viewport.GetVisibleRect();
        var inverseCanvasTransform = viewport.GetCanvasTransform().AffineInverse();

        var topLeft = inverseCanvasTransform * visibleScreenRect.Position;
        var topRight = inverseCanvasTransform * new Vector2(visibleScreenRect.End.X, visibleScreenRect.Position.Y);
        var bottomLeft = inverseCanvasTransform * new Vector2(visibleScreenRect.Position.X, visibleScreenRect.End.Y);
        var bottomRight = inverseCanvasTransform * visibleScreenRect.End;

        var minX = Mathf.Min(Mathf.Min(topLeft.X, topRight.X), Mathf.Min(bottomLeft.X, bottomRight.X));
        var minY = Mathf.Min(Mathf.Min(topLeft.Y, topRight.Y), Mathf.Min(bottomLeft.Y, bottomRight.Y));
        var maxX = Mathf.Max(Mathf.Max(topLeft.X, topRight.X), Mathf.Max(bottomLeft.X, bottomRight.X));
        var maxY = Mathf.Max(Mathf.Max(topLeft.Y, topRight.Y), Mathf.Max(bottomLeft.Y, bottomRight.Y));

        return new Rect2(new Vector2(minX, minY), new Vector2(maxX - minX, maxY - minY));
    }

    public void ShowFoodPopup(int amount, Vector2 worldPosition)
    {
        if (_ui == null || amount <= 0)
        {
            return;
        }

        var screenPosition = GetViewport().GetCanvasTransform() * worldPosition;
        _ui.ShowFoodPopup(amount, screenPosition);
    }

    public void ShowGrowthPopup(string text, Vector2 worldPosition)
    {
        if (_ui == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var screenPosition = GetViewport().GetCanvasTransform() * worldPosition;
        _ui.ShowGrowthPopup(text, screenPosition);
    }

    public void ShowComboPopup(string text, Vector2 worldPosition)
    {
        if (_ui == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var screenPosition = GetViewport().GetCanvasTransform() * worldPosition;
        _ui.ShowComboPopup(text, screenPosition);
    }

    private void OnPlayerStatsChanged()
    {
        UpdateHud();
    }

    private void OnPlayerFoodGained(int amount, Vector2 worldPosition)
    {
        _soundManager?.PlayEat();
        ShowFoodPopup(amount, worldPosition);
    }

    private void OnPlayerGrew(string text, Vector2 worldPosition)
    {
        _soundManager?.PlayGrow();
        _ui?.PlayGrowthBarEffect();
        // ShowGrowthPopup(text, worldPosition); // Text on player when grown - change text in player.cs
    }

    private void OnPlayerComboTriggered(string text, Vector2 worldPosition)
    {
        _soundManager?.PlayCombo();
        ShowComboPopup(text, worldPosition);
    }

    private void OnMuteChanged(bool muted)
    {
        _ui?.SetMuted(muted);
    }

    private void UpdateHud()
    {
        if (_player == null || _ui == null)
        {
            return;
        }

        _ui.UpdateStats(new HudStats
        {
            Size = _player.Size,
            FoodEaten = _player.FoodEaten,
            FoodTowardsNextSize = _player.FoodTowardsNextSize,
            FoodNeededForNextSize = _player.FoodNeededForNextSize,
            ComboCount = _player.ComboCount,
            ComboMultiplier = _player.ComboMultiplier,
            ComboTimeRemaining = _player.ComboTimeRemaining,
            ComboTimeRatio = _player.ComboTimeRatio
        });
    }
}
