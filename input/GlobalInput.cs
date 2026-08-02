using Godot;

public partial class GlobalInput : Node
{
    [Signal]
    public delegate void CancelPressedEventHandler();

    [Export]
    public bool QuitOnCancel = true;

    public override void _Ready()
    {
        // Keep global input responsive even when game logic is paused (for future menus).
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
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
}
