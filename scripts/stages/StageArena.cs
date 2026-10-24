using Godot;

namespace RambleRumble;

/// <summary>
/// Draws a stage's isometric ground in a Final Fantasy Tactics style: raised
/// diamond tiles with shaded side faces and a cliff skirt along the front
/// edges, like a floating island. Drawn once; nothing here redraws per frame.
/// </summary>
public partial class StageArena : Node2D
{
    /// <summary>How many height levels the cliff skirt drops below the front edges.</summary>
    private const int SkirtDepth = 4;

    public StageMap Map { get; init; } = null!;

    public override void _Draw()
    {
        var p = Map.Palette;
        // Back to front: tiles with a larger i + j are nearer the viewer.
        for (int sum = 0; sum <= Map.W + Map.H - 2; sum++)
        {
            for (int i = 0; i < Map.W; i++)
            {
                int j = sum - i;
                if (j < 0 || j >= Map.H)
                    continue;
                DrawTile(i, j, p);
            }
        }
    }

    private void DrawTile(int i, int j, StagePalette p)
    {
        var kind = Map.Kind[i, j];
        int height = Map.Height[i, j];
        bool frontEdge = i == Map.W - 1 || j == Map.H - 1;
        float top = height * Iso.Step - (kind == TileKind.Water ? Iso.Step * 0.35f : 0f);
        float bottom = frontEdge ? -SkirtDepth * Iso.Step : 0f;

        var topFace = Iso.Diamond(i, j, top);
        var baseFace = Iso.Diamond(i, j, bottom);
        var earth = height > 0 && kind == TileKind.Stone ? p.Stone.Darkened(0.15f) : p.Earth;
        if (top > bottom + 0.5f)
        {
            // South-west face (lit) and south-east face (shade), from the top down to the base.
            DrawColoredPolygon(new[] { topFace[2], topFace[3], baseFace[3], baseFace[2] }, earth.Darkened(0.05f));
            DrawColoredPolygon(new[] { topFace[1], topFace[2], baseFace[2], baseFace[1] }, earth.Darkened(0.30f));
            if (frontEdge)
                DrawSkirtStrata(topFace, baseFace, top, bottom, earth);
        }

        var colour = TopColour(kind, p, i, j, height);
        DrawColoredPolygon(topFace, colour);
        // Faint grid lines, like a tactics map.
        DrawPolyline(new[] { topFace[0], topFace[1], topFace[2], topFace[3], topFace[0] }, new Color(0, 0, 0, 0.10f), 1f);
        DrawTileDetail(kind, topFace, colour, i, j);
    }

    private static Color TopColour(TileKind kind, StagePalette p, int i, int j, int height)
    {
        var colour = kind switch
        {
            TileKind.Path => p.Path,
            TileKind.Water => p.Water,
            TileKind.Bridge => p.Bridge,
            TileKind.Stone => p.Stone,
            TileKind.Corrupted => p.Grass.Lerp(new Color(0.65f, 0.12f, 0.15f), 0.55f),
            _ => p.Grass,
        };
        // Checkerboard and a little per-tile variation so the grid reads clearly.
        float vary = ((i * 7 + j * 13) % 5 - 2) * 0.012f + ((i + j) % 2 == 0 ? 0.02f : -0.01f);
        colour = colour.Lightened(vary + height * 0.04f);
        return colour;
    }

    /// <summary>Darker bands down the cliff skirt so it reads as layered earth and rock.</summary>
    private void DrawSkirtStrata(Vector2[] topFace, Vector2[] baseFace, float top, float bottom, Color earth)
    {
        for (int level = 1; level < SkirtDepth; level++)
        {
            float t = (float)level / SkirtDepth;
            var line = new Color(0, 0, 0, 0.12f);
            DrawLine(topFace[3].Lerp(baseFace[3], t), topFace[2].Lerp(baseFace[2], t), line, 1.5f);
            DrawLine(topFace[2].Lerp(baseFace[2], t), topFace[1].Lerp(baseFace[1], t), line, 1.5f);
        }
    }

    private void DrawTileDetail(TileKind kind, Vector2[] face, Color colour, int i, int j)
    {
        var centre = (face[0] + face[2]) / 2f;
        switch (kind)
        {
            case TileKind.Water:
                DrawLine(centre + new Vector2(-12, -2), centre + new Vector2(6, -2), colour.Lightened(0.35f), 2f);
                DrawLine(centre + new Vector2(-2, 4), centre + new Vector2(12, 4), colour.Lightened(0.25f), 2f);
                break;
            case TileKind.Bridge:
                for (int k = -2; k <= 2; k++)
                {
                    var a = face[3].Lerp(face[0], 0.5f + k * 0.18f);
                    var b = face[2].Lerp(face[1], 0.5f + k * 0.18f);
                    DrawLine(a, b, colour.Darkened(0.3f), 1.5f);
                }
                break;
            case TileKind.Grass:
            case TileKind.Corrupted:
                if ((i * 31 + j * 17) % 3 == 0)
                {
                    var tuft = colour.Darkened(0.18f);
                    DrawLine(centre + new Vector2(-6, 3), centre + new Vector2(-4, -3), tuft, 1.5f);
                    DrawLine(centre + new Vector2(-2, 3), centre + new Vector2(-2, -4), tuft, 1.5f);
                    DrawLine(centre + new Vector2(2, 3), centre + new Vector2(4, -2), tuft, 1.5f);
                }
                if (kind == TileKind.Corrupted)
                    DrawLine(centre + new Vector2(-14, 0), centre + new Vector2(10, 3), new Color(1f, 0.25f, 0.25f, 0.6f), 2f);
                break;
            case TileKind.Stone:
                DrawLine(face[0].Lerp(face[2], 0.3f), face[3].Lerp(face[1], 0.6f), colour.Darkened(0.2f), 1f);
                break;
        }
    }
}
