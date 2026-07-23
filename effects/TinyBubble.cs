using Godot;
using System;

public partial class TinyBubble : AnimatedSprite2D
{
	[Export]
	public float RiseSpeed { get; set; } = 10.0f;

	public override void _Ready()
	{
		if (SpriteFrames != null && SpriteFrames.HasAnimation(Animation))
		{
			SpriteFrames.SetAnimationLoopMode(Animation, SpriteFrames.LoopMode.None);
		}

		AnimationFinished += OnAnimationFinished;
		Play(Animation);
	}

	public override void _Process(double delta)
	{
		GlobalPosition += Vector2.Up * RiseSpeed * (float)delta;
	}

	private void OnAnimationFinished()
	{
		QueueFree();
	}
}
