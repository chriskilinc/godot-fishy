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
            QueueRedraw();
        }
    }

    [Export]
    public Color SpawnBorderColor = new Color(0.2f, 0.8f, 1.0f, 0.9f);

    [Export(PropertyHint.Range, "1,12,1")]
    public int SpawnBorderWidth = 2;

    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    private Vector2 _spawnAreaMin = new Vector2(0, 0);
    private Vector2 _spawnAreaMax = new Vector2(2000, 1000);
    private Player _player;
    private GameUI _ui;

    public override void _Ready()
    {
        QueueRedraw();

        _player = GetNodeOrNull<Player>("Player");
        _ui = GetNodeOrNull<GameUI>("CanvasLayer/UI");

        UpdateHud();

        if (Engine.IsEditorHint())
        {
            return;
        }

        _rng.Randomize();

        for (int i = 0; i < FishCount; i++)
        {
            SpawnFish();
        }
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
        {
            return;
        }

        UpdateHud();
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

    private void UpdateHud()
    {
        if (_player == null || _ui == null)
        {
            return;
        }

        _ui.UpdateStats(
            _player.Size,
            _player.FoodEaten,
            _player.FoodTowardsNextSize,
            _player.FoodNeededForNextSize
        );
    }
}
