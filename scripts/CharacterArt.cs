using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace RambleRumble;

/// <summary>
/// Loads the character sprite strips listed in art/characters/manifest.json
/// (made by tools/cut_sprites.py) and turns them into SpriteFrames.
/// Swapping in better art only needs new strips and an updated manifest.
/// </summary>
public static class CharacterArt
{
    public static readonly string[] Genders = { "male", "female" };

    private sealed record StripInfo(int Frames, int Width, int Height);

    /// <summary>Playback speed and looping per animation.</summary>
    private static readonly Dictionary<string, (double Fps, bool Loop)> Timing = new()
    {
        ["idle"] = (5, true),
        ["walk"] = (9, true),
        ["run"] = (12, true),
        ["attack"] = (14, false),
        ["hurt"] = (16, false),
        ["death"] = (8, false),
        ["victory"] = (6, false),
    };

    private static Dictionary<string, StripInfo>? _manifest;
    private static readonly Dictionary<string, SpriteFrames> Cache = new();

    private static Dictionary<string, StripInfo> Manifest
    {
        get
        {
            if (_manifest == null)
            {
                string json = FileAccess.GetFileAsString("res://art/characters/manifest.json");
                _manifest = JsonSerializer.Deserialize<Dictionary<string, StripInfo>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            return _manifest;
        }
    }

    public static SpriteFrames FramesFor(string gender)
    {
        if (Cache.TryGetValue(gender, out var cached))
            return cached;
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        foreach (var (anim, (fps, loop)) in Timing)
        {
            if (!Manifest.TryGetValue($"{gender}_{anim}", out var info))
                continue;
            var strip = GD.Load<Texture2D>($"res://art/characters/{gender}_{anim}.png");
            frames.AddAnimation(anim);
            frames.SetAnimationSpeed(anim, fps);
            frames.SetAnimationLoopMode(anim, loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);
            for (int i = 0; i < info.Frames; i++)
            {
                frames.AddFrame(anim, new AtlasTexture
                {
                    Atlas = strip,
                    Region = new Rect2(i * info.Width, 0, info.Width, info.Height),
                });
            }
        }
        Cache[gender] = frames;
        return frames;
    }

    public static Texture2D Portrait(string gender) => GD.Load<Texture2D>($"res://art/portraits/{gender}.png");

    public static Texture2D? SkillIcon(string skillId)
    {
        string path = $"res://art/icons/{skillId}.png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }
}
