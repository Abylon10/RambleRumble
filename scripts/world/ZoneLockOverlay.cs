using Godot;

namespace RambleRumble;

/// <summary>Purple haze over a sealed region, with what it takes to open it. World toggles visibility.</summary>
public partial class ZoneLockOverlay : Node2D
{
    public ZoneInfo Zone { get; init; } = null!;

    public override void _Draw()
    {
        foreach (var cell in Zone.Cells)
            DrawRect(cell, new Color(0.18f, 0.10f, 0.28f, 0.55f));
        var center = Zone.Cells[0].GetCenter();
        string requirement = Zone.RequiredLevel > 1
            ? $"Complete Chapter {Zone.RequiredChapter} and reach level {Zone.RequiredLevel}"
            : $"Complete Chapter {Zone.RequiredChapter}";
        MapArt.Label(this, center, "SEALED", 40, new Color(0.85f, 0.65f, 1f));
        MapArt.Label(this, center + new Vector2(0, 40), requirement, 22, new Color(1, 1, 1, 0.85f));
        if (Zone.RequiredChapter > Quests.LastChapter)
            MapArt.Label(this, center + new Vector2(0, 72), "(coming in a future update)", 18, new Color(1, 1, 1, 0.6f));
    }
}
