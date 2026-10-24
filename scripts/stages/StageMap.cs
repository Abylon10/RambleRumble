using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble;

public enum TileKind
{
    Grass,
    Path,
    Water,
    Bridge,
    Stone,
    Corrupted,
}

public enum DecorKind
{
    Tree,
    Pine,
    Bush,
    Rock,
    Pebbles,
    Flowers,
    Tent,
    Campfire,
    Windmill,
    Crate,
    Cart,
    BridgePost,
    Pillar,
    BrokenPillar,
    AncientTree,
    Mushroom,
    FairyLight,
    RedCrystal,
    Waterfall,
    Fence,
    Signpost,
    StartArrow,
    GoalFlag,
}

/// <summary>A piece of scenery standing on a tile.</summary>
public record DecorSpec(DecorKind Kind, Vector2I Tile, Vector2 Offset, int Seed)
{
    /// <summary>Collision radius at the base, or 0 if you can walk through it.</summary>
    public float BlockRadius => Kind switch
    {
        DecorKind.Tree => 14f,
        DecorKind.Pine => 12f,
        DecorKind.Rock => 15f,
        DecorKind.Tent => 28f,
        DecorKind.Campfire => 14f,
        DecorKind.Windmill => 34f,
        DecorKind.Crate => 13f,
        DecorKind.Cart => 22f,
        DecorKind.Pillar => 14f,
        DecorKind.BrokenPillar => 13f,
        DecorKind.AncientTree => 60f,
        DecorKind.RedCrystal => 10f,
        DecorKind.Fence => 8f,
        _ => 0f,
    };

    public Vector2 Ground => Iso.TileCenter(Tile.X, Tile.Y) + Offset;
}

/// <summary>Colours of one chapter's battlefields.</summary>
public record StagePalette(Color Grass, Color Path, Color Water, Color Bridge, Color Stone, Color Earth, Color Sky, Color SkyLow);

/// <summary>
/// The tile grid, heights and scenery of one stage: a long strip of land with
/// a road from the start (bottom-left on screen) to the goal flag (top-right),
/// plus spots set aside for the treasure chest and the stage's puzzle.
/// </summary>
public sealed class StageMap
{
    public int W { get; }
    public int H { get; }
    public TileKind[,] Kind { get; }
    public int[,] Height { get; }
    public List<DecorSpec> Decor { get; } = new();
    public StagePalette Palette { get; init; } = null!;
    public Vector2I Start { get; set; }
    public Vector2I Goal { get; set; }
    /// <summary>The road, in order from start to goal.</summary>
    public List<Vector2I> Road { get; } = new();
    /// <summary>Tiles kept clear for the puzzle pieces (plates, braziers, levers or crystals).</summary>
    public List<Vector2I> PuzzleTiles { get; } = new();
    public Vector2I PuzzleChest { get; set; }
    public Vector2I TreasureChest { get; set; }

    public StageMap(int w, int h)
    {
        W = w;
        H = h;
        Kind = new TileKind[w, h];
        Height = new int[w, h];
    }

    public bool InBounds(int i, int j) => i >= 0 && j >= 0 && i < W && j < H;

    /// <summary>Raised ground and open water block movement (bridges don't).</summary>
    public bool IsBlockedTile(int i, int j) => Height[i, j] > 0 || Kind[i, j] == TileKind.Water;

    public bool HasBlockingDecor(int i, int j) => Decor.Any(d => d.Tile == new Vector2I(i, j) && d.BlockRadius > 0f);

    public bool IsOpen(int i, int j) => InBounds(i, j) && !IsBlockedTile(i, j) && !HasBlockingDecor(i, j);

    public Vector2 StartPoint => Iso.TileCenter(Start.X, Start.Y);
    public Vector2 GoalPoint => Iso.TileCenter(Goal.X, Goal.Y);

    /// <summary>A point along the road, 0 = start, 1 = goal.</summary>
    public Vector2 RoadPoint(float t)
    {
        var tile = Road[Mathf.Clamp(Mathf.RoundToInt(t * (Road.Count - 1)), 0, Road.Count - 1)];
        return Iso.TileCenter(tile.X, tile.Y);
    }

    /// <summary>Screen-space bounds of the whole island (for camera limits).</summary>
    public Rect2 ScreenBounds()
    {
        var left = Iso.ToScreen(0, H);
        var right = Iso.ToScreen(W, 0);
        var top = Iso.ToScreen(0, 0);
        var bottom = Iso.ToScreen(W, H);
        return new Rect2(left.X, top.Y - 140, right.X - left.X, bottom.Y - top.Y + 200);
    }
}
