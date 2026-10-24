using System.Collections.Generic;
using Godot;
using RambleRumble.UI;

namespace RambleRumble;

/// <summary>
/// The world map: the chapters as floating islands (art from the UI mockup).
/// Tap an unlocked island to open that chapter's stage map; later chapters
/// show as locked islands.
/// </summary>
public partial class WorldMapPage : CanvasLayer
{
    private const float TopBar = 68f;

    /// <summary>Clickable island areas in pixels of world_map.png: chapter number (0 = coming soon), rect.</summary>
    private static readonly (int Chapter, Rect2 Area)[] Islands =
    {
        (1, new Rect2(0, 58, 320, 330)),
        (2, new Rect2(330, 48, 360, 330)),
        (3, new Rect2(0, 393, 200, 135)),
        (4, new Rect2(230, 393, 200, 135)),
        (0, new Rect2(470, 393, 190, 135)),
    };

    private TextureRect _map = null!;
    private readonly List<(int Chapter, Rect2 Area, Control Hotspot)> _hotspots = new();
    private Label _toast = null!;
    private float _toastTime;

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        World.SetupInput();
        var root = new Control { Theme = UiKit.GameTheme() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var sky = new ColorRect { Color = new Color(0.07f, 0.10f, 0.18f) };
        sky.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(sky);

        _map = new TextureRect
        {
            Texture = UiKit.Texture("res://art/ui/world_map.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        _map.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _map.OffsetTop = TopBar;
        root.AddChild(_map);

        foreach (var (chapter, area) in Islands)
        {
            bool open = chapter > 0 && chapter <= StageCatalog.Chapters.Length && Data.IsChapterUnlocked(chapter);
            var hotspot = new Button { Flat = true, TooltipText = chapter > 0 ? $"Chapter {chapter}" : "Coming soon" };
            hotspot.AddThemeStyleboxOverride("hover", Transparent(UiKit.NavyStyle()));
            int number = chapter;
            hotspot.Pressed += () => OpenChapter(number);
            root.AddChild(hotspot);
            // A playable chapter that isn't unlocked yet is dimmed with a lock.
            if (!open && chapter is >= 1 and <= 2)
            {
                var shade = new ColorRect { Color = new Color(0.03f, 0.04f, 0.08f, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore };
                shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                hotspot.AddChild(shade);
                var lockLabel = UiKit.MakeLabel($"🔒 Clear Stage {chapter - 1}-10", 20, UiKit.Cream);
                lockLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
                lockLabel.GrowHorizontal = lockLabel.GrowVertical = Control.GrowDirection.Both;
                hotspot.AddChild(lockLabel);
            }
            _hotspots.Add((chapter, area, hotspot));
        }
        _map.Resized += PlaceHotspots;

        BuildTopBar(root);
        _toast = UiKit.MakeLabel("", 22, UiKit.Cream);
        _toast.HorizontalAlignment = HorizontalAlignment.Center;
        _toast.AddThemeColorOverride("font_outline_color", Colors.Black);
        _toast.AddThemeConstantOverride("outline_size", 8);
        UiKit.Place(_toast, new Vector2(0.5f, 1f), new Vector2(-400, -60), new Vector2(800, 40));
        root.AddChild(_toast);
        PlaceHotspots();
    }

    public override void _Process(double delta)
    {
        if (_toastTime > 0f && (_toastTime -= (float)delta) <= 0f)
            _toast.Text = "";
    }

    /// <summary>Opens a chapter's stage map if it's unlocked; otherwise says why not.</summary>
    public bool OpenChapter(int chapter)
    {
        if (chapter >= 1 && chapter <= StageCatalog.Chapters.Length && Data.IsChapterUnlocked(chapter))
        {
            Game.Instance?.ShowChapterMap(chapter);
            return true;
        }
        Toast(chapter == 0 || chapter > StageCatalog.Chapters.Length
            ? "More of the world is on the way."
            : $"Clear Stage {chapter - 1}-10 to reach Chapter {chapter}.");
        return false;
    }

    private void Toast(string text)
    {
        _toast.Text = text;
        _toastTime = 2.5f;
    }

    private static StyleBoxFlat Transparent(StyleBoxFlat style)
    {
        style.BgColor = new Color(1f, 0.9f, 0.6f, 0.08f);
        style.SetCornerRadiusAll(24);
        return style;
    }

    /// <summary>Keeps the island buttons over the islands wherever the picture ends up on screen.</summary>
    private void PlaceHotspots()
    {
        if (_map.Texture == null)
            return;
        var picture = _map.Texture.GetSize();
        float scale = Mathf.Min(_map.Size.X / picture.X, _map.Size.Y / picture.Y);
        var origin = _map.Position + (_map.Size - picture * scale) / 2f;
        foreach (var (_, area, hotspot) in _hotspots)
        {
            hotspot.Position = origin + area.Position * scale;
            hotspot.Size = area.Size * scale;
        }
    }

    private void BuildTopBar(Control root)
    {
        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", UiKit.NavyStyle(radius: 0, margin: 8f));
        bar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        bar.OffsetBottom = TopBar;
        root.AddChild(bar);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        bar.AddChild(row);
        var back = new Button { Text = "◀  Hearthwood", CustomMinimumSize = new Vector2(170, 48) };
        back.Pressed += () => Game.Instance?.ShowVillage(fromGate: true);
        row.AddChild(back);
        var title = UiKit.MakeLabel("World Map", 28, UiKit.Cream);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(title);
        var portrait = new TextureRect
        {
            Texture = CharacterArt.Portrait(Data.Gender),
            CustomMinimumSize = new Vector2(48, 48),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        row.AddChild(portrait);
        var level = UiKit.MakeLabel($"Lv. {Data.Level}\n{Data.Rank}", 17, UiKit.Cream);
        row.AddChild(level);
        var goldIcon = new TextureRect
        {
            Texture = UiKit.ItemIcon("Gold"),
            CustomMinimumSize = new Vector2(36, 36),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        row.AddChild(goldIcon);
        var gold = UiKit.MakeLabel($"{Data.Gold:N0}", 22, new Color(1f, 0.85f, 0.4f));
        gold.VerticalAlignment = VerticalAlignment.Center;
        gold.CustomMinimumSize = new Vector2(110, 0);
        row.AddChild(gold);
    }
}
