using Godot;

[Tool]
public partial class World : Node2D
{
    [Export]
    public PackedScene FishScene;

    [Export]
    public int FishCount = 10;

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

    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    private Vector2 _spawnAreaMin = new Vector2(0, 0);
    private Vector2 _spawnAreaMax = new Vector2(2000, 2000);
    private Player _player;
    private GameUI _ui;
    private SoundManager _soundManager;

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

        var fish = FishScene.Instantiate<Node2D>();
        fish.Position = new Vector2(
            _rng.RandfRange(SpawnAreaMin.X, SpawnAreaMax.X),
            _rng.RandfRange(SpawnAreaMin.Y, SpawnAreaMax.Y)
        );

        AddChild(fish);

        // When this fish leaves the tree (eaten or otherwise freed), spawn a replacement
        fish.TreeExited += SpawnFish;
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
