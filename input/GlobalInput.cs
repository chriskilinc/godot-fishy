using Godot;

public partial class GlobalInput : Node
{
    [Signal]
    public delegate void CancelPressedEventHandler();

    [Export]
    public bool QuitOnCancel = true;

    private SoundManager _soundManager;
    private World _world;

    public override void _Ready()
    {
        // Keep global input responsive even when game logic is paused (for future menus).
        ProcessMode = ProcessModeEnum.Always;
        CacheSoundManager();
        CacheWorld();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent
            && keyEvent.Pressed
            && !keyEvent.Echo
            && keyEvent.Keycode == Key.M)
        {
            CacheSoundManager();
            _soundManager?.ToggleMute();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventKey debugKeyEvent
            && debugKeyEvent.Pressed
            && !debugKeyEvent.Echo
            && debugKeyEvent.Keycode == Key.F1)
        {
            CacheWorld();
            if (_world != null)
            {
                _world.DebugEnabled = !_world.DebugEnabled;
                GD.Print($"Debug mode: {(_world.DebugEnabled ? "ON" : "OFF")}");
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (!@event.IsActionPressed("ui_cancel"))
        {
            return;
        }

        EmitSignal(SignalName.CancelPressed);

        if (QuitOnCancel)
        {
            GetTree().Quit();
        }

        GetViewport().SetInputAsHandled();
    }

    private void CacheSoundManager()
    {
        if (_soundManager != null && IsInstanceValid(_soundManager))
        {
            return;
        }

        _soundManager = GetParent()?.GetNodeOrNull<SoundManager>("SoundManager")
            ?? GetTree().CurrentScene?.GetNodeOrNull<SoundManager>("SoundManager");
    }

    private void CacheWorld()
    {
        if (_world != null && IsInstanceValid(_world))
        {
            return;
        }

        _world = GetParent() as World
            ?? GetTree().CurrentScene as World
            ?? GetTree().CurrentScene?.GetNodeOrNull<World>("World");
    }
}
