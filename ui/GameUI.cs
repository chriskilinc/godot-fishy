using System;
using Godot;

public partial class GameUI : Control
{
    [Signal]
    public delegate void RestartRequestedEventHandler();

    private const char FullHeart = '\u2665';
    private const char EmptyHeart = '\u2661';

    private Label _sizeLabel;
    private Label _healthLabel;
    private Label _foodLabel;
    private Label _comboLabel;
    private ProgressBar _comboTimerBar;
    private TextureProgressBar _growthBar;
    private Label _growthLabel;
    private TextureRect _prevLevelIcon;
    private TextureRect _currentLevelIcon;
    private TextureRect _nextLevelIcon;
    private PanelContainer _prevLevelPanel;
    private PanelContainer _currentLevelPanel;
    private PanelContainer _nextLevelPanel;
    private Label _mutedLabel;
    private Button _pauseButton;
    private Control _floatingTextLayer;
    private Control _bossPanel;
    private Label _bossNameLabel;
    private ProgressBar _bossHealthBar;
    private Label _bannerLabel;
    private Control _gameOverPanel;
    private Button _restartButton;

    public override void _Ready()
    {
        _sizeLabel = GetNodeOrNull<Label>("TopLeft/SizeLabel");
        _healthLabel = GetNodeOrNull<Label>("TopLeft/HealthLabel");
        _foodLabel = GetNodeOrNull<Label>("TopLeft/FoodLabel");
        _comboLabel = GetNodeOrNull<Label>("TopLeft/ComboLabel");
        _comboTimerBar = GetNodeOrNull<ProgressBar>("BottomRight/ComboTimerBar");
        _growthBar = GetNodeOrNull<TextureProgressBar>("GrowthPanel/GrowthBar");
        _growthLabel = GetNodeOrNull<Label>("GrowthPanel/GrowthBar/GrowthLabel");
        _prevLevelIcon = GetNodeOrNull<TextureRect>("GrowthPanel/LevelPreview/PrevLevel/PrevIconPanel/PrevIcon");
        _currentLevelIcon = GetNodeOrNull<TextureRect>("GrowthPanel/LevelPreview/CurrentLevel/CurrentIconPanel/CurrentIcon");
        _nextLevelIcon = GetNodeOrNull<TextureRect>("GrowthPanel/LevelPreview/NextLevel/NextIconPanel/NextIcon");
        _prevLevelPanel = GetNodeOrNull<PanelContainer>("GrowthPanel/LevelPreview/PrevLevel/PrevIconPanel");
        _currentLevelPanel = GetNodeOrNull<PanelContainer>("GrowthPanel/LevelPreview/CurrentLevel/CurrentIconPanel");
        _nextLevelPanel = GetNodeOrNull<PanelContainer>("GrowthPanel/LevelPreview/NextLevel/NextIconPanel");
        _mutedLabel = GetNodeOrNull<Label>("MutedLabel");
        _pauseButton = GetNodeOrNull<Button>("PauseButton");
        _floatingTextLayer = GetNodeOrNull<Control>("FloatingTextLayer");
        _bossPanel = GetNodeOrNull<Control>("BossPanel");
        _bossNameLabel = GetNodeOrNull<Label>("BossPanel/BossNameLabel");
        _bossHealthBar = GetNodeOrNull<ProgressBar>("BossPanel/BossHealthBar");
        _bannerLabel = GetNodeOrNull<Label>("BannerLabel");
        _gameOverPanel = GetNodeOrNull<Control>("GameOverPanel");
        _restartButton = GetNodeOrNull<Button>("GameOverPanel/GameOverBox/RestartButton");

        if (_comboTimerBar != null)
        {
            _comboTimerBar.SelfModulate = new Color("ff4d4d");
            _comboTimerBar.ShowPercentage = false;
        }

        if (_bossHealthBar != null)
        {
            _bossHealthBar.SelfModulate = new Color("c1272d");
            _bossHealthBar.ShowPercentage = false;
        }

        if (_restartButton != null)
        {
            _restartButton.Pressed += OnRestartPressed;
        }

        HideBossBar();
        HideGameOver();

        if (_bannerLabel != null)
        {
            _bannerLabel.Visible = false;
        }

        ProcessMode = ProcessModeEnum.Always;
        UpdatePauseButtonText();
        SetMuted(false);
        UpdateLevelPreview(1);
    }

    public override void _ExitTree()
    {
        if (_restartButton != null)
        {
            _restartButton.Pressed -= OnRestartPressed;
        }
    }

    /// <summary>Shows the boss name plate and health bar for a fight that just started.</summary>
    public void ShowBossBar(string bossName, int maxHealth)
    {
        if (_bossPanel == null)
        {
            return;
        }

        _bossPanel.Visible = true;

        if (_bossNameLabel != null)
        {
            _bossNameLabel.Text = bossName;
        }

        if (_bossHealthBar != null)
        {
            _bossHealthBar.MaxValue = Mathf.Max(1, maxHealth);
            _bossHealthBar.Value = Mathf.Max(1, maxHealth);
        }
    }

    public void UpdateBossHealth(int currentHealth, int maxHealth)
    {
        if (_bossHealthBar == null)
        {
            return;
        }

        _bossHealthBar.MaxValue = Mathf.Max(1, maxHealth);
        _bossHealthBar.Value = Mathf.Clamp(currentHealth, 0, Mathf.Max(1, maxHealth));
    }

    public void HideBossBar()
    {
        if (_bossPanel != null)
        {
            _bossPanel.Visible = false;
        }
    }

    /// <summary>Fades a big line of text across the middle of the screen.</summary>
    public void ShowBanner(string text, Color color, double holdSeconds = 1.6)
    {
        if (_bannerLabel == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _bannerLabel.Text = text;
        _bannerLabel.AddThemeColorOverride("font_color", color);
        _bannerLabel.Visible = true;
        _bannerLabel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);

        var bannerTween = CreateTween();
        bannerTween.TweenProperty(_bannerLabel, "modulate:a", 1.0f, 0.35f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        bannerTween.TweenInterval(holdSeconds);
        bannerTween.TweenProperty(_bannerLabel, "modulate:a", 0.0f, 0.6f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        bannerTween.Finished += () =>
        {
            if (_bannerLabel != null)
            {
                _bannerLabel.Visible = false;
            }
        };
    }

    public void ShowGameOver()
    {
        if (_gameOverPanel == null)
        {
            return;
        }

        _gameOverPanel.Visible = true;
        _restartButton?.GrabFocus();
    }

    public void HideGameOver()
    {
        if (_gameOverPanel != null)
        {
            _gameOverPanel.Visible = false;
        }
    }

    private void OnRestartPressed()
    {
        EmitSignal(SignalName.RestartRequested);
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

        if (_healthLabel != null)
        {
            var maxHealth = Mathf.Max(1, stats.MaxHealth);
            var currentHealth = Mathf.Clamp(stats.Health, 0, maxHealth);
            _healthLabel.Text = new string(FullHeart, currentHealth) + new string(EmptyHeart, maxHealth - currentHealth);
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

        UpdateLevelPreview(stats.Size);
    }

    private void UpdateLevelPreview(int currentLevel)
    {
        var clampedCurrent = Mathf.Max(1, currentLevel);
        var maxDefinedLevel = Mathf.Max(1, FishLevelVisuals.MaxDefinedLevel);
        var prevLevel = Mathf.Clamp(clampedCurrent - 1, 1, maxDefinedLevel);
        var currentDisplayLevel = Mathf.Clamp(clampedCurrent, 1, maxDefinedLevel);
        var nextLevel = Mathf.Clamp(clampedCurrent + 1, 1, maxDefinedLevel);

        if (_prevLevelIcon != null)
        {
            _prevLevelIcon.Texture = FishLevelVisuals.GetIconTextureForLevel(prevLevel);
        }

        if (_currentLevelIcon != null)
        {
            _currentLevelIcon.Texture = FishLevelVisuals.GetIconTextureForLevel(currentDisplayLevel);
        }

        if (_nextLevelIcon != null)
        {
            _nextLevelIcon.Texture = FishLevelVisuals.GetIconTextureForLevel(nextLevel);
        }

        SetLevelPanelHighlight(_prevLevelPanel, false);
        SetLevelPanelHighlight(_currentLevelPanel, true);
        SetLevelPanelHighlight(_nextLevelPanel, false);
    }

    private static void SetLevelPanelHighlight(PanelContainer panel, bool highlighted)
    {
        if (panel == null)
        {
            return;
        }

        panel.SelfModulate = highlighted
            ? new Color(1.0f, 1.0f, 0.78f, 1.0f)
            : new Color(0.78f, 0.86f, 1.0f, 0.92f);

        panel.Scale = highlighted ? new Vector2(1.07f, 1.07f) : Vector2.One;
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
