using Godot;

namespace RambleRumble;

/// <summary>Sky gradient and soft clouds behind the floating battlefield.</summary>
public partial class StageBackdrop : Node2D
{
    public StageMap Map { get; init; } = null!;

    public override void _Draw()
    {
        var centre = Iso.ToScreen(Map.W / 2f, Map.H / 2f);
        var half = new Vector2(1600, 1000);
        var p = Map.Palette;
        DrawPolygon(
            new[] { centre - half, centre + new Vector2(half.X, -half.Y), centre + half, centre + new Vector2(-half.X, half.Y) },
            new[] { p.Sky, p.Sky, p.SkyLow, p.SkyLow });
        var rng = new RandomNumberGenerator { Seed = 99 };
        for (int k = 0; k < 9; k++)
        {
            var at = centre + new Vector2(rng.RandfRange(-900, 900), rng.RandfRange(-500, 500));
            float r = rng.RandfRange(30, 60);
            var cloud = new Color(1, 1, 1, 0.35f);
            Shapes.Ellipse(this, at, new Vector2(r * 1.8f, r * 0.7f), cloud);
            Shapes.Ellipse(this, at + new Vector2(r * 0.8f, -r * 0.25f), new Vector2(r * 1.1f, r * 0.6f), cloud);
        }
    }
}
