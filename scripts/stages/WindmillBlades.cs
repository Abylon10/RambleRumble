using Godot;

namespace RambleRumble;

/// <summary>The windmill's sails; rotated each frame by its parent without redrawing.</summary>
public partial class WindmillBlades : Node2D
{
    public override void _Draw()
    {
        var sail = new Color(0.92f, 0.88f, 0.80f);
        for (int k = 0; k < 4; k++)
        {
            var dir = Vector2.FromAngle(k * Mathf.Pi / 2);
            var side = dir.Orthogonal() * 6f;
            DrawLine(Vector2.Zero, dir * 46f, new Color(0.40f, 0.28f, 0.18f), 3f);
            DrawColoredPolygon(new[] { dir * 12f, dir * 46f, dir * 46f + side, dir * 12f + side }, sail);
        }
        Shapes.Circle(this, Vector2.Zero, 5f, new Color(0.40f, 0.28f, 0.18f));
    }
}
