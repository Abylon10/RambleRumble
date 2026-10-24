using System.Linq;
using Godot;

namespace RambleRumble.Tests;

/// <summary>
/// Saves every monster sprite (both frames) as one enlarged sheet for review.
/// Run: godot --headless --path . res://tests/ArtPreview.tscn -- --test &lt;out.png&gt;
/// </summary>
public partial class ArtPreview : Node
{
    private const int Zoom = 6;
    private const int Cell = MonsterArt.BossSize * Zoom + 12;

    public override void _Ready()
    {
        string output = OS.GetCmdlineUserArgs()[1];
        var species = GameConfig.Species.Select(s => s.Name).ToArray();
        var sheet = Image.CreateEmpty(Cell * 4, Cell * ((species.Length * 2 + 3) / 4), false, Image.Format.Rgba8);
        sheet.Fill(new Color(0.47f, 0.70f, 0.34f));
        int slot = 0;
        foreach (var name in species)
        {
            for (int frame = 0; frame < 2; frame++, slot++)
            {
                var image = MonsterArt.FrameImage(name, frame);
                image.Resize(image.GetWidth() * Zoom, image.GetHeight() * Zoom, Image.Interpolation.Nearest);
                var at = new Vector2I(slot % 4 * Cell + 6, slot / 4 * Cell + 6 + (MonsterArt.BossSize * Zoom - image.GetHeight()));
                sheet.BlendRect(image, new Rect2I(Vector2I.Zero, image.GetSize()), at);
            }
        }
        sheet.SavePng(output);
        GetTree().Quit();
    }
}
