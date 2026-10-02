using System;

public enum FishHuntStyle
{
    Direct,
    Charge,
    Intercept
}

public sealed record FishBehaviorProfile
{
    public string Name { get; init; } = "";
    public float Speed { get; init; } = 72.0f;
    public float WanderIntervalMin { get; init; } = 2.0f;
    public float WanderIntervalMax { get; init; } = 4.0f;
    public float WanderTurnStrength { get; init; } = 0.65f;
    public float DirectionSmoothing { get; init; } = 2.2f;
    public float IdleChance { get; init; } = 0.28f;
    public float IdleMin { get; init; } = 0.35f;
    public float IdleMax { get; init; } = 1.1f;
    public float FleeRadius { get; init; } = 150.0f;
    public float FleePersistence { get; init; } = 0.4f;
    public float FleeStrength { get; init; } = 2.5f;
    public float FleeSmoothing { get; init; } = 6.0f;
    public float FleeSwerveAmplitude { get; init; } = 0.0f;
    public float FleeSwerveFrequency { get; init; } = 1.0f;
    public float FleeSpeedMultiplier { get; init; } = 1.45f;
    public float FleeBurstDuration { get; init; } = 0.45f;
    public float FleeBurstCooldown { get; init; } = 1.4f;
    public float HuntRadius { get; init; } = 260.0f;
    public float HuntGiveUpDelay { get; init; } = 4.0f;
    public float HuntCooldown { get; init; } = 3.0f;
    public float HuntSpeedMultiplier { get; init; } = 1.25f;
    public float HuntSmoothing { get; init; } = 12.0f;
    public FishHuntStyle HuntStyle { get; init; } = FishHuntStyle.Direct;
    public float ChargeRetargetInterval { get; init; } = 0.8f;
    public float HuntLeadSeconds { get; init; } = 0.0f;
}

public static class FishBehaviorProfiles
{
    private static readonly FishBehaviorProfile[] Profiles =
    {
        new()
        {
            Name = "Skittish", Speed = 78.0f,
            WanderIntervalMin = 1.6f, WanderIntervalMax = 2.6f, WanderTurnStrength = 0.55f,
            DirectionSmoothing = 3.0f, IdleChance = 0.18f, IdleMin = 0.3f, IdleMax = 0.65f,
            FleeRadius = 230.0f, FleePersistence = 0.9f, FleeStrength = 3.5f, FleeSmoothing = 7.0f,
            FleeSwerveAmplitude = 0.28f, FleeSwerveFrequency = 1.05f,
            FleeSpeedMultiplier = 1.55f, FleeBurstDuration = 0.35f, FleeBurstCooldown = 1.4f,
            HuntRadius = 0.0f
        },
        new()
        {
            Name = "Darting", Speed = 88.0f,
            WanderIntervalMin = 1.35f, WanderIntervalMax = 2.2f, WanderTurnStrength = 0.65f,
            DirectionSmoothing = 4.0f, IdleChance = 0.2f, IdleMin = 0.3f, IdleMax = 0.65f,
            FleeRadius = 190.0f, FleePersistence = 0.9f, FleeStrength = 4.0f, FleeSmoothing = 10.0f,
            FleeSwerveAmplitude = 0.30f, FleeSwerveFrequency = 1.6f,
            FleeSpeedMultiplier = 1.85f, FleeBurstDuration = 0.25f, FleeBurstCooldown = 1.1f,
            HuntRadius = 0.0f
        },
        new()
        {
            Name = "Cautious", Speed = 68.0f,
            WanderIntervalMin = 2.5f, WanderIntervalMax = 4.0f, WanderTurnStrength = 0.45f,
            DirectionSmoothing = 2.5f, IdleChance = 0.4f, IdleMin = 0.5f, IdleMax = 1.4f,
            FleeRadius = 165.0f, FleePersistence = 0.6f, FleeStrength = 3.0f, FleeSmoothing = 6.0f,
            FleeSpeedMultiplier = 1.5f, FleeBurstDuration = 0.5f, FleeBurstCooldown = 1.5f,
            HuntRadius = 180.0f, HuntGiveUpDelay = 0.8f, HuntCooldown = 4.0f,
            HuntSpeedMultiplier = 1.2f, HuntSmoothing = 6.0f
        },
        new()
        {
            Name = "Charger", Speed = 100.0f,
            WanderIntervalMin = 3.0f, WanderIntervalMax = 4.5f, WanderTurnStrength = 0.25f,
            DirectionSmoothing = 2.0f, IdleChance = 0.12f,
            FleeRadius = 145.0f, FleePersistence = 0.35f, FleeStrength = 3.5f, FleeSmoothing = 5.0f,
            FleeSpeedMultiplier = 1.6f, FleeBurstDuration = 0.4f, FleeBurstCooldown = 1.6f,
            HuntRadius = 280.0f, HuntGiveUpDelay = 1.5f, HuntCooldown = 2.5f,
            HuntSpeedMultiplier = 1.65f, HuntSmoothing = 4.0f,
            HuntStyle = FishHuntStyle.Charge, ChargeRetargetInterval = 0.9f
        },
        new()
        {
            Name = "Interceptor", Speed = 82.0f,
            WanderIntervalMin = 3.5f, WanderIntervalMax = 5.5f, WanderTurnStrength = 0.35f,
            DirectionSmoothing = 1.8f, IdleChance = 0.18f,
            FleeRadius = 125.0f, FleePersistence = 0.25f, FleeStrength = 2.5f, FleeSmoothing = 4.0f,
            FleeSpeedMultiplier = 1.3f, FleeBurstDuration = 0.6f, FleeBurstCooldown = 2.0f,
            HuntRadius = 320.0f, HuntGiveUpDelay = 3.0f, HuntCooldown = 2.0f,
            HuntSpeedMultiplier = 1.4f, HuntSmoothing = 7.0f,
            HuntStyle = FishHuntStyle.Intercept, HuntLeadSeconds = 0.6f
        },
        new()
        {
            Name = "Relentless", Speed = 92.0f,
            WanderIntervalMin = 4.0f, WanderIntervalMax = 6.0f, WanderTurnStrength = 0.2f,
            DirectionSmoothing = 1.5f, IdleChance = 0.03f,
            FleeRadius = 105.0f, FleePersistence = 0.15f, FleeStrength = 2.0f, FleeSmoothing = 3.0f,
            FleeSpeedMultiplier = 1.2f, FleeBurstDuration = 0.4f, FleeBurstCooldown = 2.5f,
            HuntRadius = 380.0f, HuntGiveUpDelay = 4.5f, HuntCooldown = 0.8f,
            HuntSpeedMultiplier = 1.5f, HuntSmoothing = 10.0f
        }
    };

    public static FishBehaviorProfile GetForLevel(int level)
    {
        return Profiles[Math.Clamp(level - 1, 0, Profiles.Length - 1)];
    }
}