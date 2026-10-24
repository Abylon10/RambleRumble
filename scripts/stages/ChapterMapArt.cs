using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble;

/// <summary>
/// The painted region behind a chapter map: ground, river, trees and landmark
/// icons, and the dirt road linking the stage circles. Redrawn only on resize.
/// </summary>
public partial class ChapterMapArt : Control
{
    public ChapterMapDef Def { get; init; } = null!;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Pass;

    public override void _Draw()
    {
        var size = Size;
        if (size.X < 10f)
            return;
        DrawPolygon(new[] { Vector2.Zero, new Vector2(size.X, 0), size, new Vector2(0, size.Y) },
            new[] { Def.Ground.Lightened(0.08f), Def.Ground, Def.GroundDark, Def.GroundDark.Lightened(0.05f) });
        var rng = new RandomNumberGenerator { Seed = (ulong)(Def.Number * 4241) };
        var route = Route(size);

        if (Def.Number == 1)
            DrawPlains(size, rng, route);
        else
            DrawForest(size, rng, route);

        // The road: dark edge, sandy middle, dashes.
        DrawPolyline(route, new Color(0.35f, 0.24f, 0.14f), 22f, true);
        DrawPolyline(route, new Color(0.85f, 0.72f, 0.48f), 14f, true);
        for (int k = 0; k < route.Length - 1; k += 6)
            DrawLine(route[k], route[k + 1], new Color(1f, 0.95f, 0.8f, 0.7f), 3f);

        foreach (var (name, at) in Def.Landmarks)
            Label(at * size, name);

        DrawCompass(new Vector2(size.X - 70, 70));
        DrawTitleCard();
    }

    /// <summary>Chapter title and blurb in a dark card at the top-left, like a tactics map legend.</summary>
    private void DrawTitleCard()
    {
        var font = ThemeDB.FallbackFont;
        var card = new Rect2(16, 16, 360, 120);
        DrawRect(card, new Color(0.08f, 0.07f, 0.06f, 0.82f));
        DrawRect(card, new Color(0.85f, 0.70f, 0.40f), false, 2f);
        DrawString(font, card.Position + new Vector2(14, 34), $"CHAPTER {Def.Number}", HorizontalAlignment.Left, -1, 22, new Color(1f, 0.92f, 0.75f));
        DrawString(font, card.Position + new Vector2(14, 62), Def.Region.ToUpper(), HorizontalAlignment.Left, -1, 24, Def.Accent);
        DrawMultilineString(font, card.Position + new Vector2(14, 84), Def.Blurb, HorizontalAlignment.Left, 332, 13, -1, new Color(0.9f, 0.9f, 0.85f));
    }

    private void DrawCompass(Vector2 at)
    {
        var ring = new Color(0.95f, 0.92f, 0.85f, 0.75f);
        Shapes.Ring(this, at, 34f, ring, thin: true);
        DrawColoredPolygon(new[] { at + new Vector2(0, -40), at + new Vector2(6, 0), at + new Vector2(-6, 0) }, new Color(0.85f, 0.25f, 0.2f, 0.9f));
        DrawColoredPolygon(new[] { at + new Vector2(0, 40), at + new Vector2(6, 0), at + new Vector2(-6, 0) }, ring);
        DrawColoredPolygon(new[] { at + new Vector2(40, 0), at + new Vector2(0, 6), at + new Vector2(0, -6) }, ring);
        DrawColoredPolygon(new[] { at + new Vector2(-40, 0), at + new Vector2(0, 6), at + new Vector2(0, -6) }, ring);
        DrawString(ThemeDB.FallbackFont, at + new Vector2(-6, -44), "N", HorizontalAlignment.Left, -1, 14, ring);
    }

    /// <summary>Smooth Catmull-Rom curve through the stage positions.</summary>
    private Vector2[] Route(Vector2 size)
    {
        var points = Def.Route.Select(p => p * size).ToList();
        var curve = new List<Vector2>();
        for (int k = 0; k < points.Count - 1; k++)
        {
            var p0 = points[Mathf.Max(k - 1, 0)];
            var p1 = points[k];
            var p2 = points[k + 1];
            var p3 = points[Mathf.Min(k + 2, points.Count - 1)];
            for (int s = 0; s < 12; s++)
            {
                float t = s / 12f;
                curve.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
            }
        }
        curve.Add(points[^1]);
        return curve.ToArray();
    }

    private static bool NearRoute(Vector2 at, Vector2[] route, float distance) => route.Any(r => r.DistanceTo(at) < distance);

    private void DrawPlains(Vector2 size, RandomNumberGenerator rng, Vector2[] route)
    {
        // Lighter meadow patches.
        for (int k = 0; k < 18; k++)
        {
            var at = new Vector2(rng.RandfRange(0, size.X), rng.RandfRange(0, size.Y));
            Shapes.Ellipse(this, at, new Vector2(rng.RandfRange(50, 120), rng.RandfRange(20, 40)), Def.Ground.Lightened(0.1f));
        }
        // A river under the Broken Bridge, in the lower right.
        var river = new[] { new Vector2(0.62f, 1.0f), new Vector2(0.68f, 0.82f), new Vector2(0.76f, 0.70f), new Vector2(0.86f, 0.62f), new Vector2(1.0f, 0.55f) }
            .Select(p => p * size).ToArray();
        DrawPolyline(river, new Color(0.22f, 0.45f, 0.75f), 30f, true);
        DrawPolyline(river, new Color(0.35f, 0.62f, 0.92f), 20f, true);
        Trees(size, rng, route, 40, Def.Ground.Darkened(0.3f), pine: false);
        // Slime Fields: little slimes.
        for (int k = 0; k < 5; k++)
        {
            var at = new Vector2(0.06f + k * 0.035f, 0.60f + (k % 2) * 0.04f) * size;
            Shapes.Circle(this, at, 9, new Color(0.45f, 0.85f, 0.45f));
            Shapes.Circle(this, at + new Vector2(-3, -2), 1.8f, Colors.Black);
            Shapes.Circle(this, at + new Vector2(3, -2), 1.8f, Colors.Black);
        }
        // Traveler's Camp: tents.
        foreach (var dx in new[] { 0f, 0.04f })
        {
            var at = new Vector2(0.40f + dx, 0.15f) * size;
            DrawColoredPolygon(new[] { at + new Vector2(-18, 0), at + new Vector2(0, -26), at + new Vector2(18, 0) }, new Color(0.85f, 0.72f, 0.5f));
        }
        // Old Windmill.
        var mill = new Vector2(0.64f, 0.36f) * size;
        DrawColoredPolygon(new[] { mill + new Vector2(-14, 0), mill + new Vector2(-8, -50), mill + new Vector2(8, -50), mill + new Vector2(14, 0) }, new Color(0.88f, 0.84f, 0.74f));
        DrawColoredPolygon(new[] { mill + new Vector2(-12, -48), mill + new Vector2(0, -66), mill + new Vector2(12, -48) }, new Color(0.62f, 0.25f, 0.2f));
        DrawLine(mill + new Vector2(-26, -76), mill + new Vector2(26, -36), new Color(0.95f, 0.92f, 0.85f), 4f);
        DrawLine(mill + new Vector2(-26, -36), mill + new Vector2(26, -76), new Color(0.95f, 0.92f, 0.85f), 4f);
    }

    private void DrawForest(Vector2 size, RandomNumberGenerator rng, Vector2[] route)
    {
        // Waterfall stream down the middle.
        var stream = new[] { new Vector2(0.36f, 0f), new Vector2(0.38f, 0.22f), new Vector2(0.34f, 0.45f), new Vector2(0.40f, 0.70f), new Vector2(0.36f, 1f) }
            .Select(p => p * size).ToArray();
        DrawPolyline(stream, new Color(0.18f, 0.40f, 0.60f), 26f, true);
        DrawPolyline(stream, new Color(0.35f, 0.65f, 0.85f), 16f, true);
        var fall = new Vector2(0.37f, 0.18f) * size;
        DrawRect(new Rect2(fall + new Vector2(-16, -40), new Vector2(32, 44)), new Color(0.75f, 0.9f, 1f, 0.8f));
        Trees(size, rng, route, 90, new Color(0.12f, 0.32f, 0.18f), pine: true);
        // Hidden Grove glow.
        var grove = new Vector2(0.12f, 0.80f) * size;
        Shapes.Circle(this, grove, 40, new Color(0.6f, 1f, 0.75f, 0.18f));
        // Forest Ruin pillars.
        for (int k = 0; k < 3; k++)
        {
            var at = new Vector2(0.58f + k * 0.03f, 0.16f) * size;
            DrawRect(new Rect2(at + new Vector2(-6, -30 + k * 6), new Vector2(12, 30 - k * 6)), new Color(0.6f, 0.62f, 0.58f));
        }
        // The Ancient Tree.
        var tree = new Vector2(0.86f, 0.62f) * size;
        DrawRect(new Rect2(tree + new Vector2(-10, -40), new Vector2(20, 40)), new Color(0.40f, 0.28f, 0.18f));
        Shapes.Circle(this, tree + new Vector2(0, -62), 42, new Color(0.15f, 0.40f, 0.22f));
        Shapes.Circle(this, tree + new Vector2(-18, -76), 22, new Color(0.22f, 0.50f, 0.28f));
        Shapes.Circle(this, tree + new Vector2(0, -40), 6, new Color(0.6f, 1f, 0.75f));
    }

    private void Trees(Vector2 size, RandomNumberGenerator rng, Vector2[] route, int count, Color colour, bool pine)
    {
        for (int k = 0; k < count; k++)
        {
            var at = new Vector2(rng.RandfRange(0, size.X), rng.RandfRange(0, size.Y));
            if (NearRoute(at, route, 46f) || Def.Route.Any(r => (r * size).DistanceTo(at) < 60f))
                continue;
            float r = rng.RandfRange(9, 16);
            if (pine)
            {
                DrawColoredPolygon(new[] { at + new Vector2(-r, 0), at + new Vector2(0, -r * 2.4f), at + new Vector2(r, 0) }, colour.Lightened(rng.RandfRange(0, 0.12f)));
            }
            else
            {
                Shapes.Circle(this, at, r, colour);
                Shapes.Circle(this, at + new Vector2(-r * 0.3f, -r * 0.3f), r * 0.55f, colour.Lightened(0.15f));
            }
        }
    }

    private void Label(Vector2 at, string text)
    {
        var font = ThemeDB.FallbackFont;
        var origin = at + new Vector2(-110, 0);
        DrawStringOutline(font, origin, text, HorizontalAlignment.Center, 220, 18, 6, new Color(0, 0, 0, 0.7f));
        DrawString(font, origin, text, HorizontalAlignment.Center, 220, 18, new Color(1f, 0.95f, 0.8f));
    }
}
