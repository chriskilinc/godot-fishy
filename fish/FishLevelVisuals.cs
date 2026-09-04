using Godot;
using System;
using System.Collections.Generic;

public static class FishLevelVisuals
{
    private readonly struct FishSheet
    {
        public FishSheet(string texturePath, Vector2I frameSize, int swimRows, float swimSpeed, Vector2 collisionSizeMultiplier)
        {
            TexturePath = texturePath;
            FrameSize = frameSize;
            SwimRows = swimRows;
            SwimSpeed = swimSpeed;
            CollisionSizeMultiplier = collisionSizeMultiplier;
        }

        public string TexturePath { get; }
        public Vector2I FrameSize { get; }
        public int SwimRows { get; }
        public float SwimSpeed { get; }
        public Vector2 CollisionSizeMultiplier { get; }
    }

    // Shared order for both enemies and player: same level always resolves to the same species.
    private static readonly FishSheet[] LevelSheets =
    {
        new FishSheet("res://assets/fishes/fish1/Orange.png", new Vector2I(16, 16), 2, 6.0f, new Vector2(1.0f, 1.0f)),
        new FishSheet("res://assets/fishes/fish2/Blue.png", new Vector2I(32, 16), 2, 6.0f, new Vector2(1.0f, 1.0f)),
        new FishSheet("res://assets/fishes/fish3/Orange.png", new Vector2I(32, 16), 2, 5.6f, new Vector2(1.0f, 1.0f)),
        new FishSheet("res://assets/fishes/swordfish/SwordFish.png", new Vector2I(48, 32), 2, 4.4f, new Vector2(1.25f, 1.0f)),
        new FishSheet("res://assets/fishes/saw_shark/SawShark.png", new Vector2I(48, 32), 2, 4.4f, new Vector2(1.45f, 1.0f)),
        new FishSheet("res://assets/fishes/shark/Shark.png", new Vector2I(32, 32), 2, 4.8f, new Vector2(1.45f, 1.0f)),
    };

    private static readonly Dictionary<string, SpriteFrames> FramesCache = new();
    private static readonly Dictionary<int, Texture2D> IconTextureCache = new();

    public static int MaxDefinedLevel => LevelSheets.Length;

    public static Texture2D GetTextureForLevel(int level)
    {
        var sheet = GetSheetForLevel(level);
        return GD.Load<Texture2D>(sheet.TexturePath);
    }

    public static Texture2D GetIconTextureForLevel(int level)
    {
        var sheetIndex = GetSheetIndexForLevel(level);
        if (IconTextureCache.TryGetValue(sheetIndex, out var cachedTexture) && cachedTexture != null)
        {
            return cachedTexture;
        }

        var sheet = LevelSheets[sheetIndex];
        var sourceTexture = GD.Load<Texture2D>(sheet.TexturePath);
        if (sourceTexture == null)
        {
            return null;
        }

        var iconTexture = new AtlasTexture
        {
            Atlas = sourceTexture,
            Region = new Rect2(0, 0, sheet.FrameSize.X, sheet.FrameSize.Y)
        };

        IconTextureCache[sheetIndex] = iconTexture;
        return iconTexture;
    }

    public static void ApplyLevelFrames(AnimatedSprite2D sprite, int level, bool includeEatAnimation)
    {
        if (sprite == null)
        {
            return;
        }

        var key = GetCacheKey(level, includeEatAnimation);
        if (!FramesCache.TryGetValue(key, out var frames))
        {
            frames = BuildFrames(level, includeEatAnimation);
            FramesCache[key] = frames;
        }

        var currentAnimation = sprite.Animation;
        var currentFrame = sprite.Frame;
        var wasPlaying = sprite.IsPlaying();
        var wasFlipped = sprite.FlipH;

        sprite.SpriteFrames = frames;

        if (frames.HasAnimation(currentAnimation))
        {
            sprite.Play(currentAnimation);
        }
        else
        {
            sprite.Play("default");
            currentAnimation = "default";
        }

        var frameCount = frames.GetFrameCount(currentAnimation);
        if (frameCount > 0)
        {
            sprite.Frame = Mathf.Clamp(currentFrame, 0, frameCount - 1);
        }

        if (!wasPlaying)
        {
            sprite.Stop();
        }

        sprite.FlipH = wasFlipped;
    }

    public static Vector2 GetCollisionSizeMultiplierForLevel(int level)
    {
        var sheet = GetSheetForLevel(level);
        return new Vector2(
            Mathf.Max(0.1f, sheet.CollisionSizeMultiplier.X),
            Mathf.Max(0.1f, sheet.CollisionSizeMultiplier.Y)
        );
    }

    private static string GetCacheKey(int level, bool includeEatAnimation)
    {
        var sheetIndex = GetSheetIndexForLevel(level);
        return $"{sheetIndex}:{includeEatAnimation}";
    }

    private static int GetSheetIndexForLevel(int level)
    {
        var clampedLevel = Math.Max(1, level);
        return Math.Min(clampedLevel - 1, LevelSheets.Length - 1);
    }

    private static FishSheet GetSheetForLevel(int level)
    {
        var sheetIndex = GetSheetIndexForLevel(level);
        return LevelSheets[sheetIndex];
    }

    private static SpriteFrames BuildFrames(int level, bool includeEatAnimation)
    {
        var sheet = GetSheetForLevel(level);
        var texture = GD.Load<Texture2D>(sheet.TexturePath);
        var frames = new SpriteFrames();

        if (texture == null)
        {
            GD.PushError($"FishLevelVisuals: Could not load texture '{sheet.TexturePath}'.");
            if (!frames.HasAnimation("default"))
            {
                frames.AddAnimation("default");
            }
            frames.SetAnimationLoopMode("default", (SpriteFrames.LoopMode)1);
            frames.SetAnimationSpeed("default", 6.0f);
            return frames;
        }

        var columns = Math.Max(1, texture.GetWidth() / Math.Max(1, sheet.FrameSize.X));
        var totalRows = Math.Max(1, texture.GetHeight() / Math.Max(1, sheet.FrameSize.Y));
        var swimRows = Math.Min(Math.Max(1, sheet.SwimRows), totalRows);

        if (!frames.HasAnimation("default"))
        {
            frames.AddAnimation("default");
        }
        frames.SetAnimationLoopMode("default", (SpriteFrames.LoopMode)1);
        frames.SetAnimationSpeed("default", sheet.SwimSpeed);

        for (var row = 0; row < swimRows; row++)
        {
            for (var col = 0; col < columns; col++)
            {
                frames.AddFrame("default", CreateAtlasFrame(texture, col, row, sheet.FrameSize));
            }
        }

        if (!includeEatAnimation)
        {
            return frames;
        }

        frames.AddAnimation("eat");
        frames.SetAnimationLoopMode("eat", (SpriteFrames.LoopMode)0);
        frames.SetAnimationSpeed("eat", Math.Max(2.5f, sheet.SwimSpeed - 1.5f));

        // If this sheet has an extra row, use it for bite anticipation; otherwise fallback to a subtle two-frame nibble.
        if (totalRows >= 3)
        {
            frames.AddFrame("eat", CreateAtlasFrame(texture, 0, 2, sheet.FrameSize), 1.0f);
            frames.AddFrame("eat", CreateAtlasFrame(texture, 0, 1, sheet.FrameSize), 2.0f);
        }
        else
        {
            frames.AddFrame("eat", CreateAtlasFrame(texture, 0, 0, sheet.FrameSize), 1.0f);
            frames.AddFrame("eat", CreateAtlasFrame(texture, Math.Min(1, columns - 1), 0, sheet.FrameSize), 1.0f);
        }

        return frames;
    }

    private static AtlasTexture CreateAtlasFrame(Texture2D texture, int col, int row, Vector2I frameSize)
    {
        return new AtlasTexture
        {
            Atlas = texture,
            Region = new Rect2(col * frameSize.X, row * frameSize.Y, frameSize.X, frameSize.Y)
        };
    }
}