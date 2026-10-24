using Godot;

namespace RambleRumble;

/// <summary>
/// One piece of stage scenery (tree, tent, windmill, ruin...), drawn once in a
/// chunky tactics style. It lives in the y-sorted actor layer so characters
/// walk behind and in front of it correctly.
/// </summary>
public partial class IsoDecor : Node2D
{
    /// <summary>
    /// Characters' origins sit at their body centre, about this far above their
    /// feet, so scenery sorts from the same height to overlap correctly.
    /// </summary>
    public const float SortBias = 18f;

    public DecorSpec Spec { get; init; } = null!;
    public StagePalette Palette { get; init; } = null!;

    private Node2D? _blades;

    public override void _Ready()
    {
        Position = Spec.Ground - new Vector2(0, SortBias);
        if (Spec.Kind == DecorKind.Windmill)
        {
            _blades = new WindmillBlades { Position = new Vector2(0, SortBias - 92) };
            AddChild(_blades);
        }
        // Only the windmill animates; everything else is drawn once.
        SetProcess(_blades != null);
    }

    public override void _Process(double delta)
    {
        if (_blades != null)
            _blades.Rotation += (float)delta * 0.8f;
    }

    public override void _Draw()
    {
        // Draw relative to the ground point.
        var g = new Vector2(0, SortBias);
        var rng = new RandomNumberGenerator { Seed = (ulong)Spec.Seed };
        switch (Spec.Kind)
        {
            case DecorKind.Tree: Tree(g, rng); break;
            case DecorKind.Pine: Pine(g, rng); break;
            case DecorKind.Bush: Bush(g); break;
            case DecorKind.Rock: Rock(g, 1f); break;
            case DecorKind.Pebbles: Rock(g + new Vector2(-6, 2), 0.35f); Rock(g + new Vector2(7, -1), 0.3f); break;
            case DecorKind.Flowers: Flowers(g, rng); break;
            case DecorKind.Tent: Tent(g, rng); break;
            case DecorKind.Campfire: Campfire(g); break;
            case DecorKind.Windmill: Windmill(g); break;
            case DecorKind.Crate: Box(g, 11f, 14f, new Color(0.62f, 0.45f, 0.25f)); break;
            case DecorKind.Cart: Cart(g); break;
            case DecorKind.BridgePost: Post(g); break;
            case DecorKind.Pillar: Pillar(g, 52f); break;
            case DecorKind.BrokenPillar: Pillar(g, 22f + rng.RandfRange(0, 10)); break;
            case DecorKind.AncientTree: AncientTree(g); break;
            case DecorKind.Mushroom: Mushroom(g); break;
            case DecorKind.FairyLight: FairyLight(g, rng); break;
            case DecorKind.RedCrystal: Crystal(g, new Color(0.95f, 0.18f, 0.22f)); break;
            case DecorKind.Waterfall: Waterfall(g); break;
            case DecorKind.Fence: Fence(g); break;
            case DecorKind.Signpost: Signpost(g); break;
            case DecorKind.StartArrow: StartArrow(g); break;
            case DecorKind.GoalFlag: GoalFlag(g); break;
        }
    }

    private void Shadow(Vector2 g, float w) => Shapes.Ellipse(this, g, new Vector2(w, w * 0.42f), new Color(0, 0, 0, 0.25f));

    private void Tree(Vector2 g, RandomNumberGenerator rng)
    {
        float s = rng.RandfRange(0.9f, 1.15f);
        Shadow(g, 20 * s);
        DrawRect(new Rect2(g + new Vector2(-4 * s, -26 * s), new Vector2(8 * s, 26 * s)), new Color(0.45f, 0.30f, 0.18f));
        var dark = Palette.Grass.Darkened(0.35f);
        Shapes.Circle(this, g + new Vector2(0, -40 * s), 21 * s, dark);
        Shapes.Circle(this, g + new Vector2(-9 * s, -46 * s), 13 * s, Palette.Grass.Darkened(0.12f));
        Shapes.Circle(this, g + new Vector2(8 * s, -50 * s), 12 * s, Palette.Grass.Darkened(0.05f));
        Shapes.Circle(this, g + new Vector2(-2 * s, -56 * s), 9 * s, Palette.Grass.Lightened(0.12f));
    }

    private void Pine(Vector2 g, RandomNumberGenerator rng)
    {
        float s = rng.RandfRange(0.9f, 1.25f);
        Shadow(g, 16 * s);
        DrawRect(new Rect2(g + new Vector2(-3 * s, -14 * s), new Vector2(6 * s, 14 * s)), new Color(0.40f, 0.27f, 0.16f));
        var dark = new Color(0.12f, 0.32f, 0.20f);
        for (int tier = 0; tier < 3; tier++)
        {
            float y = -12 * s - tier * 14 * s;
            float w = (20 - tier * 5) * s;
            var shade = dark.Lightened(tier * 0.08f);
            DrawColoredPolygon(new[] { g + new Vector2(-w, y), g + new Vector2(0, y - 24 * s), g + new Vector2(w, y) }, shade);
            DrawColoredPolygon(new[] { g + new Vector2(0, y), g + new Vector2(0, y - 24 * s), g + new Vector2(w, y) }, shade.Darkened(0.2f));
        }
    }

    private void Bush(Vector2 g)
    {
        Shadow(g, 14);
        Shapes.Circle(this, g + new Vector2(-6, -7), 9, Palette.Grass.Darkened(0.28f));
        Shapes.Circle(this, g + new Vector2(6, -6), 8, Palette.Grass.Darkened(0.2f));
        Shapes.Circle(this, g + new Vector2(0, -11), 8, Palette.Grass.Darkened(0.08f));
    }

    private void Rock(Vector2 g, float s)
    {
        Shadow(g, 18 * s);
        var stone = new Color(0.58f, 0.57f, 0.55f);
        DrawColoredPolygon(new[] { g + new Vector2(-17, 0) * s, g + new Vector2(-12, -16) * s, g + new Vector2(2, -22) * s,
            g + new Vector2(16, -12) * s, g + new Vector2(17, 0) * s }, stone);
        DrawColoredPolygon(new[] { g + new Vector2(2, -22) * s, g + new Vector2(16, -12) * s, g + new Vector2(17, 0) * s, g + new Vector2(4, 0) * s },
            stone.Darkened(0.25f));
        DrawColoredPolygon(new[] { g + new Vector2(-12, -16) * s, g + new Vector2(2, -22) * s, g + new Vector2(-2, -12) * s }, stone.Lightened(0.2f));
    }

    private void Flowers(Vector2 g, RandomNumberGenerator rng)
    {
        Color[] colours = { new(1f, 0.85f, 0.3f), new(1f, 1f, 1f), new(0.95f, 0.5f, 0.6f), new(0.7f, 0.6f, 1f) };
        for (int k = 0; k < 5; k++)
        {
            var at = g + new Vector2(rng.RandfRange(-18, 18), rng.RandfRange(-7, 7));
            Shapes.Circle(this, at, 2.5f, colours[rng.RandiRange(0, colours.Length - 1)]);
        }
    }

    private void Tent(Vector2 g, RandomNumberGenerator rng)
    {
        Shadow(g, 30);
        var cloth = rng.Randf() < 0.5f ? new Color(0.85f, 0.75f, 0.55f) : new Color(0.75f, 0.45f, 0.35f);
        var peak = g + new Vector2(0, -42);
        DrawColoredPolygon(new[] { g + new Vector2(-30, 0), peak, g + new Vector2(0, 10) }, cloth);
        DrawColoredPolygon(new[] { g + new Vector2(0, 10), peak, g + new Vector2(30, 0) }, cloth.Darkened(0.25f));
        DrawColoredPolygon(new[] { g + new Vector2(-10, 6), g + new Vector2(-4, -16), g + new Vector2(2, 8) }, new Color(0.2f, 0.15f, 0.1f));
        DrawLine(peak, peak + new Vector2(0, -14), new Color(0.4f, 0.3f, 0.2f), 2f);
        DrawColoredPolygon(new[] { peak + new Vector2(0, -14), peak + new Vector2(12, -10), peak + new Vector2(0, -6) }, new Color(0.85f, 0.2f, 0.2f));
    }

    private void Campfire(Vector2 g)
    {
        Shapes.Ellipse(this, g, new Vector2(26, 11), new Color(1f, 0.6f, 0.2f, 0.18f));
        for (int k = 0; k < 6; k++)
            Shapes.Circle(this, g + Vector2.FromAngle(k * Mathf.Tau / 6) * new Vector2(14, 6), 4f, new Color(0.45f, 0.43f, 0.40f));
        DrawLine(g + new Vector2(-10, 3), g + new Vector2(10, -3), new Color(0.40f, 0.26f, 0.15f), 4f);
        DrawLine(g + new Vector2(-10, -3), g + new Vector2(10, 3), new Color(0.40f, 0.26f, 0.15f), 4f);
        DrawColoredPolygon(new[] { g + new Vector2(-8, 0), g + new Vector2(0, -24), g + new Vector2(8, 0) }, new Color(1f, 0.5f, 0.1f));
        DrawColoredPolygon(new[] { g + new Vector2(-4, 0), g + new Vector2(0, -13), g + new Vector2(4, 0) }, new Color(1f, 0.9f, 0.4f));
    }

    private void Windmill(Vector2 g)
    {
        Shadow(g, 38);
        var wall = new Color(0.86f, 0.82f, 0.72f);
        // Tapered stone tower: lit left face and shaded right face.
        DrawColoredPolygon(new[] { g + new Vector2(-26, 0), g + new Vector2(-16, -86), g + new Vector2(0, -86), g + new Vector2(0, 8) }, wall);
        DrawColoredPolygon(new[] { g + new Vector2(0, 8), g + new Vector2(0, -86), g + new Vector2(16, -86), g + new Vector2(26, 0) }, wall.Darkened(0.25f));
        DrawRect(new Rect2(g + new Vector2(-6, -22), new Vector2(10, 22)), new Color(0.40f, 0.27f, 0.16f));
        DrawRect(new Rect2(g + new Vector2(-12, -58), new Vector2(8, 10)), new Color(0.25f, 0.2f, 0.18f));
        // Conical roof.
        var roof = new Color(0.62f, 0.25f, 0.20f);
        DrawColoredPolygon(new[] { g + new Vector2(-22, -84), g + new Vector2(0, -116), g + new Vector2(0, -80) }, roof);
        DrawColoredPolygon(new[] { g + new Vector2(0, -80), g + new Vector2(0, -116), g + new Vector2(22, -84) }, roof.Darkened(0.25f));
    }

    private void Box(Vector2 g, float half, float height, Color colour)
    {
        Shadow(g, half * 1.4f);
        var top = new[] { g + new Vector2(0, -height - half / 2), g + new Vector2(half, -height), g + new Vector2(0, -height + half / 2), g + new Vector2(-half, -height) };
        DrawColoredPolygon(new[] { top[3], top[2], g + new Vector2(0, half / 2), g + new Vector2(-half, 0) }, colour.Darkened(0.1f));
        DrawColoredPolygon(new[] { top[2], top[1], g + new Vector2(half, 0), g + new Vector2(0, half / 2) }, colour.Darkened(0.35f));
        DrawColoredPolygon(top, colour.Lightened(0.12f));
    }

    private void Cart(Vector2 g)
    {
        Box(g + new Vector2(0, -6), 18f, 16f, new Color(0.60f, 0.42f, 0.24f));
        Shapes.Circle(this, g + new Vector2(-14, 2), 7, new Color(0.30f, 0.22f, 0.15f));
        Shapes.Circle(this, g + new Vector2(10, 6), 7, new Color(0.30f, 0.22f, 0.15f));
        Box(g + new Vector2(-2, -24), 8f, 8f, new Color(0.85f, 0.75f, 0.45f));
    }

    private void Post(Vector2 g)
    {
        var wood = Palette.Bridge.Darkened(0.2f);
        DrawRect(new Rect2(g + new Vector2(-4, -22), new Vector2(8, 26)), wood);
        DrawRect(new Rect2(g + new Vector2(0, -22), new Vector2(4, 26)), wood.Darkened(0.25f));
    }

    private void Pillar(Vector2 g, float height)
    {
        Shadow(g, 16);
        var stone = Palette.Stone;
        DrawRect(new Rect2(g + new Vector2(-11, -height), new Vector2(11, height)), stone);
        DrawRect(new Rect2(g + new Vector2(0, -height), new Vector2(11, height)), stone.Darkened(0.25f));
        Shapes.Ellipse(this, g + new Vector2(0, -height), new Vector2(11, 5), stone.Lightened(0.15f));
        DrawLine(g + new Vector2(-8, -height * 0.6f), g + new Vector2(-3, -height * 0.4f), stone.Darkened(0.35f), 1.5f);
    }

    private void AncientTree(Vector2 g)
    {
        Shadow(g, 70);
        var bark = new Color(0.42f, 0.30f, 0.20f);
        DrawColoredPolygon(new[] { g + new Vector2(-30, 4), g + new Vector2(-18, -90), g + new Vector2(18, -90), g + new Vector2(30, 4) }, bark);
        DrawColoredPolygon(new[] { g + new Vector2(0, 6), g + new Vector2(0, -90), g + new Vector2(18, -90), g + new Vector2(30, 4) }, bark.Darkened(0.25f));
        foreach (var root in new[] { -44f, -26f, 26f, 44f })
            DrawLine(g + new Vector2(root * 0.4f, -6), g + new Vector2(root, 6), bark.Darkened(0.1f), 7f);
        var leaf = new Color(0.18f, 0.45f, 0.24f);
        Shapes.Circle(this, g + new Vector2(0, -130), 62, leaf.Darkened(0.2f));
        Shapes.Circle(this, g + new Vector2(-40, -112), 38, leaf);
        Shapes.Circle(this, g + new Vector2(42, -118), 36, leaf.Lightened(0.05f));
        Shapes.Circle(this, g + new Vector2(0, -160), 34, leaf.Lightened(0.12f));
        Shapes.Circle(this, g + new Vector2(0, -60), 9, new Color(0.6f, 1f, 0.75f, 0.85f));
        Shapes.Circle(this, g + new Vector2(0, -60), 16, new Color(0.6f, 1f, 0.75f, 0.25f));
    }

    private void Mushroom(Vector2 g)
    {
        DrawRect(new Rect2(g + new Vector2(-2, -8), new Vector2(4, 8)), new Color(0.92f, 0.88f, 0.78f));
        Shapes.Ellipse(this, g + new Vector2(0, -9), new Vector2(8, 5), new Color(0.85f, 0.22f, 0.20f));
        Shapes.Circle(this, g + new Vector2(-3, -10), 1.5f, Colors.White);
        Shapes.Circle(this, g + new Vector2(3, -9), 1.5f, Colors.White);
    }

    private void FairyLight(Vector2 g, RandomNumberGenerator rng)
    {
        for (int k = 0; k < 3; k++)
        {
            var at = g + new Vector2(rng.RandfRange(-16, 16), rng.RandfRange(-40, -12));
            Shapes.Circle(this, at, 7, new Color(0.7f, 1f, 0.8f, 0.25f));
            Shapes.Circle(this, at, 2.5f, new Color(0.9f, 1f, 0.9f));
        }
    }

    private void Crystal(Vector2 g, Color colour)
    {
        Shapes.Ellipse(this, g, new Vector2(18, 8), new Color(colour, 0.25f));
        DrawColoredPolygon(new[] { g + new Vector2(-8, 0), g + new Vector2(-2, -30), g + new Vector2(4, 0) }, colour);
        DrawColoredPolygon(new[] { g + new Vector2(-2, -30), g + new Vector2(4, 0), g + new Vector2(2, -14) }, colour.Lightened(0.35f));
        DrawColoredPolygon(new[] { g + new Vector2(2, 0), g + new Vector2(10, -18), g + new Vector2(12, 0) }, colour.Darkened(0.15f));
    }

    /// <summary>Water spilling off the front cliff edge into the clouds below.</summary>
    private void Waterfall(Vector2 g)
    {
        var water = Palette.Water;
        var top = g + new Vector2(-6, -2);
        DrawColoredPolygon(new[] { top + new Vector2(-14, 0), top + new Vector2(14, -8), top + new Vector2(14, 92), top + new Vector2(-14, 100) }, water.Lightened(0.12f));
        for (int k = -1; k <= 1; k++)
            DrawLine(top + new Vector2(k * 8, -2 - k * 3), top + new Vector2(k * 8, 96 - k * 3), new Color(1, 1, 1, 0.45f), 2f);
        Shapes.Ellipse(this, top + new Vector2(0, 100), new Vector2(26, 9), new Color(1, 1, 1, 0.6f));
    }

    private void Fence(Vector2 g)
    {
        var wood = new Color(0.55f, 0.38f, 0.22f);
        foreach (float x in new[] { -14f, 0f, 14f })
            DrawRect(new Rect2(g + new Vector2(x - 2, -18 + x * 0.25f), new Vector2(4, 18)), wood.Darkened(0.15f));
        DrawLine(g + new Vector2(-16, -14 - 3.5f), g + new Vector2(16, -14 + 3.5f), wood, 3f);
        DrawLine(g + new Vector2(-16, -7 - 3.5f), g + new Vector2(16, -7 + 3.5f), wood, 3f);
    }

    private void Signpost(Vector2 g)
    {
        var wood = new Color(0.55f, 0.38f, 0.22f);
        DrawRect(new Rect2(g + new Vector2(-2, -30), new Vector2(4, 30)), wood.Darkened(0.2f));
        DrawColoredPolygon(new[] { g + new Vector2(-14, -32), g + new Vector2(12, -36), g + new Vector2(18, -29), g + new Vector2(12, -22), g + new Vector2(-14, -18) }, wood);
    }

    /// <summary>Blue arrow painted on the ground where the stage starts.</summary>
    private void StartArrow(Vector2 g)
    {
        var dir = (Iso.ToScreen(0, -1) - Iso.ToScreen(0, 0)).Normalized(); // along the road, toward the goal
        var side = dir.Orthogonal();
        var tip = g + dir * 22f;
        var colour = new Color(0.35f, 0.65f, 1f, 0.9f);
        DrawColoredPolygon(new[] { tip, g + side * 14f, g + side * 6f, g - dir * 16f + side * 6f, g - dir * 16f - side * 6f, g - side * 6f, g - side * 14f }, colour);
    }

    /// <summary>Red banner on a pole marking the goal.</summary>
    private void GoalFlag(Vector2 g)
    {
        Shadow(g, 14);
        DrawRect(new Rect2(g + new Vector2(-2, -70), new Vector2(4, 70)), new Color(0.35f, 0.25f, 0.18f));
        Shapes.Circle(this, g + new Vector2(0, -72), 4, new Color(1f, 0.85f, 0.3f));
        DrawColoredPolygon(new[] { g + new Vector2(2, -66), g + new Vector2(30, -62), g + new Vector2(30, -36), g + new Vector2(16, -42), g + new Vector2(2, -38) },
            new Color(0.80f, 0.15f, 0.18f));
        DrawLine(g + new Vector2(10, -56), g + new Vector2(22, -48), new Color(1f, 0.85f, 0.3f), 2f);
    }
}
