using Godot;
using System;

public partial class EnemyFish : Area2D
{
    [Export]
    public int Size { get; set; } = 1;

    [Export]
    public int FoodValue { get; set; } = 1;

    [Export]
    public float Speed { get; set; } = 100.0f;

    private Vector2 direction;
    private Vector2 startPosition;

    public override void _Ready()
    {
        // Randomize fish size and speed
        // Size = GD.RandRange(1, 3); // Size between 1 and 3
        // Speed = (float)GD.RandRange(50.0f, 150.0f); // Speed between 50 and 150

        // Set random starting position within the game area
        // startPosition = new Vector2((float)GD.RandRange(0, 800), (float)GD.RandRange(0, 600));
        // Position = startPosition;

        // Set a random direction for the enemy fish
        // direction = new Vector2((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)).Normalized();
    }

    public override void _Process(double delta)
    {
        // // Move the enemy fish in the set direction
        // Position += direction * Speed * (float)delta;

        // // If the fish goes out of bounds, reset its position and direction
        // if (Position.X < 0 || Position.X > 800 || Position.Y < 0 || Position.Y > 600)
        // {
        //     Position = startPosition;
        //     direction = new Vector2((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)).Normalized();
        // }
    }

    // Add on body entered signal to detect collision with player
    private void _on_body_entered(Node body)
    {
        if (body is Player player)
        {
            if (Size > player.Size)
            {
                // Enemy fish is bigger than player, player loses
                GD.Print("Player has been eaten!");
                // You can add logic to reset the game or reduce player's size here
            }
            else
            {
                // Player is bigger than enemy fish, player eats the enemy fish
                GD.Print("Enemy fish has been eaten!");
                player.EatFood(FoodValue);
                QueueFree(); // Remove the enemy fish from the scene
                // You can add logic to increase player's size here
            }
        }
    }
}
