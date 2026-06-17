using Godot;
using System;

public partial class Player : CharacterBody2D
{
    [Export]
    public float Speed = 200.0f;
    
    [Export]
    public int Size = 1;

    public override void _Ready()
    {

    }

    public override void _Process(double delta)
    {
        // Player Movement
        var input = Input.GetVector("move_left", "move_right", "move_up", "move_down").Normalized();
        Velocity = input * Speed;
        MoveAndSlide();
    }
}
