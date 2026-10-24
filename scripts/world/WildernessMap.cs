using Godot;

namespace RambleRumble;

/// <summary>
/// Draws the seven Wilderness zones from <see cref="Zones"/> with simple
/// per-zone decorations, plus the gate, campfires and swarm marker.
/// Placeholder art until a real tileset arrives.
/// </summary>
public partial class WildernessMap : Node2D
{
    public override void _Draw()
    {
        DrawRect(Zones.WildRect.Grow(40), new Color(0.18f, 0.16f, 0.14f));
        foreach (var zone in Zones.All)
        {
            foreach (var cell in zone.Cells)
                DrawRect(cell, zone.Ground);
        }
        // Each zone seeds its own RNG so decorations stay put between redraws.
        int seed = 1;
        foreach (var zone in Zones.All)
        {
            var rng = new RandomNumberGenerator { Seed = (ulong)(seed++ * 7919) };
            foreach (var cell in zone.Cells)
                Decorate(zone, cell, rng);
        }
        foreach (var zone in Zones.All)
        {
            var labelAt = zone.Cells[0].Position + new Vector2(zone.Cells[0].Size.X / 2, 70);
            MapArt.Label(this, labelAt, $"{zone.Id}  •  {zone.Name.ToUpper()}", 30, zone.TierColor);
            MapArt.Label(this, labelAt + new Vector2(0, 32), zone.Tier, 20, new Color(1, 1, 1, 0.75f));
        }

        foreach (var fire in Zones.Campfires)
            MapArt.Campfire(this, fire, Zones.CampfireRadius);
        DrawSwarmMarker();
        MapArt.Portal(this, Zones.Gate, "LOBBY");
    }

    private void Decorate(ZoneInfo zone, Rect2 cell, RandomNumberGenerator rng)
    {
        var inner = cell.Grow(-40);
        Vector2 RandomPoint() => new(rng.RandfRange(inner.Position.X, inner.End.X), rng.RandfRange(inner.Position.Y, inner.End.Y));
        switch (zone.Id)
        {
            case "A": // Open grass with flowers and a few trees.
                for (int i = 0; i < 90; i++)
                    DrawCircle(RandomPoint(), 4, i % 3 == 0 ? zone.Accent : new Color(0.95f, 0.95f, 0.95f));
                for (int i = 0; i < 12; i++)
                    MapArt.Tree(this, RandomPoint(), rng.RandfRange(24, 34), new Color(0.20f, 0.45f, 0.22f), new Color(0.30f, 0.58f, 0.28f));
                break;
            case "B": // Dense forest.
                for (int i = 0; i < 70; i++)
                    MapArt.Tree(this, RandomPoint(), rng.RandfRange(26, 44), zone.Accent, new Color(0.20f, 0.42f, 0.18f));
                break;
            case "C": // A winding river with wooden bridges; based on world x so it joins across cells.
                var water = new Vector2[13];
                for (int i = 0; i < water.Length; i++)
                {
                    float x = cell.Position.X + i * cell.Size.X / 12f;
                    water[i] = new Vector2(x, cell.GetCenter().Y + Mathf.Sin(x * 0.0045f) * 110f);
                }
                DrawPolyline(water, new Color(0.18f, 0.40f, 0.65f), 110f);
                DrawPolyline(water, zone.Accent, 84f);
                var bridge = water[6];
                DrawRect(new Rect2(bridge - new Vector2(40, 70), new Vector2(80, 140)), new Color(0.55f, 0.38f, 0.22f));
                for (int i = 0; i < 8; i++)
                    DrawLine(bridge + new Vector2(-40, -70 + i * 18), bridge + new Vector2(40, -70 + i * 18), new Color(0.40f, 0.27f, 0.15f), 2f);
                for (int i = 0; i < 10; i++)
                    MapArt.Tree(this, RandomPoint(), rng.RandfRange(22, 30), new Color(0.18f, 0.40f, 0.22f), new Color(0.28f, 0.52f, 0.28f));
                break;
            case "D": // Rocks and boulders.
                for (int i = 0; i < 45; i++)
                {
                    var p = RandomPoint();
                    float r = rng.RandfRange(14, 40);
                    DrawCircle(p + new Vector2(4, 6), r, new Color(0, 0, 0, 0.18f));
                    DrawCircle(p, r, zone.Accent);
                    DrawCircle(p + new Vector2(-r * 0.3f, -r * 0.3f), r * 0.45f, new Color(0.58f, 0.50f, 0.40f));
                }
                break;
            case "E": // Glowing crystals.
                for (int i = 0; i < 40; i++)
                {
                    var p = RandomPoint();
                    float h = rng.RandfRange(20, 50);
                    DrawCircle(p, h * 0.9f, new Color(zone.Accent, 0.12f));
                    DrawColoredPolygon(new[] { p + new Vector2(-h * 0.3f, 0), p + new Vector2(0, -h), p + new Vector2(h * 0.3f, 0) }, zone.Accent);
                    DrawColoredPolygon(new[] { p + new Vector2(0, 0), p + new Vector2(0, -h), p + new Vector2(h * 0.3f, 0) }, zone.Accent.Lightened(0.35f));
                }
                break;
            case "F": // Lava pools and dark rock.
                for (int i = 0; i < 16; i++)
                {
                    var p = RandomPoint();
                    float r = rng.RandfRange(30, 70);
                    DrawCircle(p, r + 8, new Color(0.18f, 0.08f, 0.06f));
                    DrawCircle(p, r, zone.Accent);
                    DrawCircle(p, r * 0.55f, new Color(1f, 0.80f, 0.30f));
                }
                break;
            case "G": // Broken stone floor and glowing pillars.
                for (int x = 0; x < 10; x++)
                {
                    for (int y = 0; y < 7; y++)
                    {
                        if (rng.Randf() < 0.45f)
                            DrawRect(new Rect2(inner.Position + new Vector2(x * 110 + 10, y * 115 + 10), new Vector2(96, 100)), new Color(0.40f, 0.44f, 0.44f));
                    }
                }
                for (int i = 0; i < 10; i++)
                {
                    var p = RandomPoint();
                    DrawRect(new Rect2(p - new Vector2(16, 40), new Vector2(32, 60)), new Color(0.50f, 0.54f, 0.54f));
                    DrawCircle(p - new Vector2(0, 46), 8, zone.Accent);
                }
                break;
        }
    }

    private void DrawSwarmMarker()
    {
        var at = Zones.SwarmMarker;
        DrawCircle(at, 70, new Color(0.45f, 0.20f, 0.65f, 0.35f));
        DrawArc(at, 70, 0f, Mathf.Tau, 48, new Color(0.75f, 0.45f, 1f), 4f);
        DrawCircle(at + new Vector2(0, -6), 26, new Color(0.92f, 0.90f, 0.86f));
        DrawRect(new Rect2(at + new Vector2(-14, 12), new Vector2(28, 14)), new Color(0.92f, 0.90f, 0.86f));
        DrawCircle(at + new Vector2(-10, -8), 7, Colors.Black);
        DrawCircle(at + new Vector2(10, -8), 7, Colors.Black);
        MapArt.Label(this, at + new Vector2(0, -90), "WORLD BOSS / SWARMS", 18, new Color(0.85f, 0.65f, 1f));
        MapArt.Label(this, at + new Vector2(0, 100), "(Phase 3)", 14, new Color(1, 1, 1, 0.6f));
    }
}
