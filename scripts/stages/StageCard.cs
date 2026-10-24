using System.Collections.Generic;
using Godot;
using RambleRumble.UI;

namespace RambleRumble;

/// <summary>
/// A stage card under the chapter map: "1. Greenfield Road", a thumbnail of
/// that spot on the painted map, and the stars earned. Blue frame when selected.
/// </summary>
public partial class StageCard : Button
{
    public StageDef Stage { get; init; } = null!;
    /// <summary>The painted chapter map; the thumbnail is the area around this stage's marker.</summary>
    public Texture2D? MapArt { get; init; }
    public Vector2 MapPoint { get; init; }

    private Label _stars = null!;

    public override void _Ready()
    {
        var data = PlayerData.Instance;
        bool unlocked = data.IsStageUnlocked(Stage);
        CustomMinimumSize = new Vector2(232, 92);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ClipContents = true;

        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 2);
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        box.OffsetLeft = box.OffsetTop = 6;
        box.OffsetRight = box.OffsetBottom = -6;
        AddChild(box);

        var title = UiKit.MakeLabel($"{Stage.Number}. {Stage.Name}", 15, Stage.IsBoss ? new Color(1f, 0.6f, 0.5f) : UiKit.Cream);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.MouseFilter = MouseFilterEnum.Ignore;
        box.AddChild(title);

        var thumb = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = unlocked ? Colors.White : new Color(0.4f, 0.4f, 0.45f),
        };
        if (MapArt != null)
            thumb.Texture = Thumbnail(MapArt, MapPoint);
        box.AddChild(thumb);
        if (!unlocked)
        {
            var lockLabel = UiKit.MakeLabel("🔒", 22, UiKit.Cream);
            lockLabel.SetAnchorsPreset(LayoutPreset.Center);
            lockLabel.GrowHorizontal = lockLabel.GrowVertical = GrowDirection.Both;
            thumb.AddChild(lockLabel);
        }

        _stars = UiKit.MakeLabel("", 15, new Color(1f, 0.82f, 0.3f));
        _stars.HorizontalAlignment = HorizontalAlignment.Center;
        _stars.MouseFilter = MouseFilterEnum.Ignore;
        box.AddChild(_stars);
        int stars = data.StageStars.GetValueOrDefault(Stage.Id);
        _stars.Text = new string('★', stars) + new string('☆', 3 - stars);
    }

    public void SetSelected(bool selected)
    {
        if (selected)
            AddThemeStyleboxOverride("normal", UiKit.NavyStyle(UiKit.Current, 3, margin: 6f));
        else
            RemoveThemeStyleboxOverride("normal");
    }

    /// <summary>A 2.3:1 window of the painted map just above the stage marker (so its number stays out of the picture).</summary>
    private static AtlasTexture Thumbnail(Texture2D map, Vector2 point)
    {
        var size = map.GetSize();
        var region = new Vector2(size.X * 0.24f, size.X * 0.24f / 2.3f);
        var corner = (point * size - new Vector2(region.X / 2f, region.Y * 1.1f)).Clamp(Vector2.Zero, size - region);
        return new AtlasTexture { Atlas = map, Region = new Rect2(corner, region) };
    }
}
