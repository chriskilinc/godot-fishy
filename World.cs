using Godot;

public partial class World : Node2D
{
    [Export]
    public PackedScene FishScene;

    [Export]
    public int FishCount = 10;

    [Export]
    public Vector2 SpawnAreaMin = new Vector2(0, 0);

    [Export]
    public Vector2 SpawnAreaMax = new Vector2(2000, 1000);

    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    public override void _Ready()
    {
        _rng.Randomize();

        var viewport = GetViewportRect();
        SpawnAreaMin = viewport.Position;
        SpawnAreaMax = viewport.End;

        for (int i = 0; i < FishCount; i++)
        {
            SpawnFish();
        }
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
}
