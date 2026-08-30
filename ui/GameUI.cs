using System;
using Godot;

public partial class GameUI : Control
{
    private Label _sizeLabel;
    private Label _foodLabel;
    private Label _comboLabel;
    private ProgressBar _comboTimerBar;
    private TextureProgressBar _growthBar;
    private Label _growthLabel;
    private Label _mutedLabel;
    private Button _pauseButton;
    private Control _floatingTextLayer;

    public override void _Ready()
    {
        _sizeLabel = GetNodeOrNull<Label>("TopLeft/SizeLabel");
        _foodLabel = GetNodeOrNull<Label>("TopLeft/FoodLabel");
        _comboLabel = GetNodeOrNull<Label>("TopLeft/ComboLabel");
        _comboTimerBar = GetNodeOrNull<ProgressBar>("BottomRight/ComboTimerBar");
        _growthBar = GetNodeOrNull<TextureProgressBar>("GrowthPanel/GrowthBar");
        _growthLabel = GetNodeOrNull<Label>("GrowthPanel/GrowthBar/GrowthLabel");
        _mutedLabel = GetNodeOrNull<Label>("MutedLabel");
        _pauseButton = GetNodeOrNull<Button>("PauseButton");
        _floatingTextLayer = GetNodeOrNull<Control>("FloatingTextLayer");

        if (_comboTimerBar != null)
        {
            _comboTimerBar.SelfModulate = new Color("ff4d4d");
            _comboTimerBar.ShowPercentage = false;
        }

        ProcessMode = ProcessModeEnum.Always;
        UpdatePauseButtonText();
        SetMuted(false);
    }

    public void SetMuted(bool muted)
    {
        if (_mutedLabel == null)
        {
            return;
        }

        _mutedLabel.Visible = muted;
    }

    public void PlayGrowthBarEffect()
    {
        if (_growthBar == null)
        {
            return;
        }

        var flashOverlay = new ColorRect
        {
            Color = new Color(0.65f, 1.0f, 0.95f, 0.0f),
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 5,
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 0.0f,
            OffsetTop = 0.0f,
            OffsetRight = 0.0f,
            OffsetBottom = 0.0f
        };
        _growthBar.AddChild(flashOverlay);

        var flashTween = CreateTween();
        flashTween.TweenProperty(flashOverlay, "color:a", 0.4f, 0.1f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        flashTween.TweenProperty(flashOverlay, "color:a", 0.0f, 0.22f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        flashTween.Finished += flashOverlay.QueueFree;

        if (_floatingTextLayer != null)
        {
            var barCenter = _growthBar.GetGlobalRect().GetCenter();
            ShowFloatingText(
                "GROWTH!",
                barCenter + new Vector2(-44.0f, -8.0f),
                new Color("9ffcff"),
                22,
                new Vector2(0.0f, -20.0f),
                0.5f
            );
        }
    }

    public void UpdateStats(HudStats stats)
    {
        if (_sizeLabel != null)
        {
            _sizeLabel.Text = $"Size: {stats.Size}";
        }

        if (_foodLabel != null)
        {
            _foodLabel.Text = $"Food: {stats.FoodEaten}";
        }

        if (_comboLabel != null)
        {
            _comboLabel.Text = stats.ComboCount > 1 ? $"Combo: x{stats.ComboMultiplier:0.0}" : "Combo: -";
            _comboLabel.Visible = stats.ComboCount > 1;
        }

        if (_comboTimerBar != null)
        {
            _comboTimerBar.Visible = stats.ComboCount > 1 && stats.ComboTimeRemaining > 0.0f;
            _comboTimerBar.MaxValue = 1.0f;
            _comboTimerBar.Value = stats.ComboTimeRatio;
        }

        var maxValue = Mathf.Max(1, stats.FoodNeededForNextSize);
        var clampedValue = Mathf.Clamp(stats.FoodTowardsNextSize, 0, maxValue);

        if (_growthBar != null)
        {
            _growthBar.MaxValue = maxValue;
            _growthBar.Value = clampedValue;
        }

        if (_growthLabel != null)
        {
            _growthLabel.Text = $"{clampedValue}/{maxValue}";
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

    public void ShowComboPopup(string text, Vector2 screenPosition)
    {
        if (_floatingTextLayer == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ShowFloatingText(
            text,
            screenPosition + new Vector2(-28.0f, -20.0f),
            new Color("ff7fd1"),
            28,
            new Vector2(0.0f, -36.0f),
            0.75f
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
