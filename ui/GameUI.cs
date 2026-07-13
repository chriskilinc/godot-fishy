using Godot;

public partial class GameUI : Control
{
    private Label _sizeLabel;
    private Label _foodLabel;
    private ProgressBar _growthBar;
    private Label _growthLabel;
    private Button _pauseButton;
    private Control _floatingTextLayer;

    public override void _Ready()
    {
        _sizeLabel = GetNodeOrNull<Label>("TopLeft/SizeLabel");
        _foodLabel = GetNodeOrNull<Label>("TopLeft/FoodLabel");
        _growthBar = GetNodeOrNull<ProgressBar>("GrowthPanel/GrowthBar");
        _growthLabel = GetNodeOrNull<Label>("GrowthPanel/GrowthLabel");
        _pauseButton = GetNodeOrNull<Button>("PauseButton");
        _floatingTextLayer = GetNodeOrNull<Control>("FloatingTextLayer");

        ProcessMode = Node.ProcessModeEnum.Always;
        UpdatePauseButtonText();
    }

    public void UpdateStats(int size, int foodEaten, int foodTowardsNextSize, int foodNeededForNextSize)
    {
        if (_sizeLabel != null)
        {
            _sizeLabel.Text = $"Size: {size}";
        }

        if (_foodLabel != null)
        {
            _foodLabel.Text = $"Food: {foodEaten}";
        }

        var maxValue = Mathf.Max(1, foodNeededForNextSize);
        var clampedValue = Mathf.Clamp(foodTowardsNextSize, 0, maxValue);

        if (_growthBar != null)
        {
            _growthBar.MaxValue = maxValue;
            _growthBar.Value = clampedValue;
        }

        if (_growthLabel != null)
        {
            _growthLabel.Text = $"Growth: {clampedValue}/{maxValue}";
        }
    }

    public void ShowFoodPopup(int amount, Vector2 screenPosition)
    {
        if (_floatingTextLayer == null || amount <= 0)
        {
            return;
        }

        ShowFloatingText(
            $"+{amount}",
            screenPosition,
            new Color("4fd66f"),
            24,
            new Vector2(0.0f, -32.0f),
            0.7f
        );
    }

    public void ShowGrowthPopup(string text, Vector2 screenPosition)
    {
        if (_floatingTextLayer == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ShowFloatingText(
            text,
            screenPosition + new Vector2(-18.0f, -18.0f),
            new Color("7ce7ff"),
            34,
            new Vector2(0.0f, -48.0f),
            0.9f
        );
    }

    private void ShowFloatingText(string text, Vector2 screenPosition, Color color, int fontSize, Vector2 travelOffset, double duration)
    {
        if (_floatingTextLayer == null)
        {
            return;
        }

        var popupLabel = new Label
        {
            Text = text,
            ZIndex = 100,
            Position = screenPosition
        };

        popupLabel.AddThemeColorOverride("font_color", color);
        popupLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.65f));
        popupLabel.AddThemeFontSizeOverride("font_size", fontSize);
        popupLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        popupLabel.AddThemeConstantOverride("shadow_offset_y", 1);

        _floatingTextLayer.AddChild(popupLabel);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(popupLabel, "position", screenPosition + travelOffset, (float)duration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(popupLabel, "modulate:a", 0.0f, (float)duration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        tween.Finished += popupLabel.QueueFree;
    }

    private void _OnPausePressed()
    {
        var tree = GetTree();
        if (tree == null)
        {
            return;
        }

        tree.Paused = !tree.Paused;
        UpdatePauseButtonText();
    }

    private void UpdatePauseButtonText()
    {
        if (_pauseButton == null)
        {
            return;
        }

        _pauseButton.Text = GetTree()?.Paused == true ? "Resume" : "Pause";
    }
}
