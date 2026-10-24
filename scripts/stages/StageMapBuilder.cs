using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble;

/// <summary>
/// Builds a stage: a long strip of floating land (9 × 24 tiles) with a road
/// from the start to the goal flag, raised cliffs along the sides, rivers
/// crossed by bridges, scenery for the stage's landmark, and spots for the
/// treasure chest and the puzzle. Seeded by stage id, so a stage always looks
/// the same.
/// </summary>
public static class StageMapBuilder
{
    public const int Width = 9;
    public const int Length = 24;

    public static readonly StagePalette Plains = new(
        Grass: new Color(0.47f, 0.70f, 0.34f), Path: new Color(0.80f, 0.68f, 0.46f), Water: new Color(0.30f, 0.60f, 0.90f),
        Bridge: new Color(0.58f, 0.40f, 0.23f), Stone: new Color(0.62f, 0.62f, 0.58f), Earth: new Color(0.50f, 0.47f, 0.42f),
        Sky: new Color(0.62f, 0.80f, 0.93f), SkyLow: new Color(0.86f, 0.92f, 0.85f));

    public static readonly StagePalette Forest = new(
        Grass: new Color(0.27f, 0.52f, 0.28f), Path: new Color(0.58f, 0.47f, 0.32f), Water: new Color(0.25f, 0.58f, 0.78f),
        Bridge: new Color(0.50f, 0.34f, 0.20f), Stone: new Color(0.52f, 0.57f, 0.52f), Earth: new Color(0.40f, 0.38f, 0.34f),
        Sky: new Color(0.30f, 0.50f, 0.45f), SkyLow: new Color(0.55f, 0.72f, 0.58f));

    /// <summary>Stable across runs (string.GetHashCode is not).</summary>
    private static ulong Seed(string id)
    {
        ulong hash = 1469598103934665603UL;
        foreach (char c in id)
            hash = (hash ^ c) * 1099511628211UL;
        return hash;
    }

    public static StageMap Build(StageDef stage)
    {
        var rng = new RandomNumberGenerator { Seed = Seed(stage.Id) };
        var map = new StageMap(Width, Length) { Palette = stage.Chapter == 1 ? Plains : Forest };
        var reserved = CarveRoad(map, rng);
        ReservePuzzleAndTreasure(map, rng, reserved);

        // Rivers cross the strip; where the road meets them there's a bridge, and they spill off the front cliff.
        int rivers = stage.Layout switch
        {
            StageLayout.Bridge => 2,
            StageLayout.Waterfall => 2,
            StageLayout.Fields or StageLayout.Windmill or StageLayout.Grove or StageLayout.ForestEdge => 1,
            _ => 0,
        };
        foreach (int j in new[] { 7, 15 }.Take(rivers))
            River(map, reserved, j, rng);

        Cliffs(map, rng, reserved, stage.Layout is StageLayout.PlainsBoss or StageLayout.ForestBoss ? 2 : 5);

        bool forest = stage.Chapter == 2;
        var tree = forest ? DecorKind.Pine : DecorKind.Tree;
        switch (stage.Layout)
        {
            case StageLayout.Fields:
                Scatter(map, rng, reserved, DecorKind.Flowers, 22);
                Scatter(map, rng, reserved, tree, 16);
                Scatter(map, rng, reserved, DecorKind.Rock, 4);
                Fences(map, rng, reserved, 6);
                break;
            case StageLayout.Swarm:
                Corrupt(map, rng, 30);
                Scatter(map, rng, reserved, DecorKind.RedCrystal, 5);
                Scatter(map, rng, reserved, tree, 12);
                break;
            case StageLayout.Camp:
                Landmark(map, rng, reserved, DecorKind.Campfire, 12);
                Landmark(map, rng, reserved, DecorKind.Tent, 11);
                Landmark(map, rng, reserved, DecorKind.Tent, 13);
                Landmark(map, rng, reserved, DecorKind.Cart, 16);
                Scatter(map, rng, reserved, DecorKind.Crate, 4);
                Scatter(map, rng, reserved, tree, 14);
                break;
            case StageLayout.Windmill:
                Landmark(map, rng, reserved, DecorKind.Windmill, 11);
                Scatter(map, rng, reserved, DecorKind.Crate, 4);
                Fences(map, rng, reserved, 8);
                Scatter(map, rng, reserved, DecorKind.Flowers, 16);
                Scatter(map, rng, reserved, tree, 10);
                break;
            case StageLayout.Bridge:
                AddBridgePosts(map, rng);
                Scatter(map, rng, reserved, tree, 14);
                Scatter(map, rng, reserved, DecorKind.Rock, 5);
                break;
            case StageLayout.PlainsBoss:
            case StageLayout.ForestBoss:
                Corrupt(map, rng, 40);
                Scatter(map, rng, reserved, DecorKind.RedCrystal, 6);
                Scatter(map, rng, reserved, forest ? DecorKind.Pine : DecorKind.BrokenPillar, 10);
                Scatter(map, rng, reserved, tree, 8);
                break;
            case StageLayout.ForestEdge:
                Scatter(map, rng, reserved, DecorKind.Pine, 26);
                Scatter(map, rng, reserved, DecorKind.Bush, 8);
                Scatter(map, rng, reserved, DecorKind.Mushroom, 6);
                break;
            case StageLayout.Grove:
                Scatter(map, rng, reserved, DecorKind.FairyLight, 10);
                Scatter(map, rng, reserved, DecorKind.Flowers, 12);
                Scatter(map, rng, reserved, DecorKind.Pine, 18);
                if (stage.Number == 4)
                    Landmark(map, rng, reserved, DecorKind.Cart, 12);
                break;
            case StageLayout.Waterfall:
                Scatter(map, rng, reserved, DecorKind.Pine, 20);
                Scatter(map, rng, reserved, DecorKind.Rock, 6);
                break;
            case StageLayout.Ruin:
                StonePatches(map, rng, 30);
                Scatter(map, rng, reserved, DecorKind.Pillar, 7);
                Scatter(map, rng, reserved, DecorKind.BrokenPillar, 7);
                Scatter(map, rng, reserved, DecorKind.Pine, 10);
                break;
            case StageLayout.AncientTree:
                Landmark(map, rng, reserved, DecorKind.AncientTree, 5);
                Scatter(map, rng, reserved, DecorKind.Pine, 18);
                Scatter(map, rng, reserved, DecorKind.Mushroom, 8);
                Scatter(map, rng, reserved, DecorKind.FairyLight, 6);
                break;
        }

        map.Decor.Add(new DecorSpec(DecorKind.StartArrow, map.Start, Vector2.Zero, 0));
        map.Decor.Add(new DecorSpec(DecorKind.GoalFlag, map.Goal, new Vector2(0, -4), 0));
        map.Decor.Add(new DecorSpec(DecorKind.Signpost, map.Road[2], new Vector2(-22, 6), 0));
        return map;
    }

    /// <summary>Winding road along the strip; returns every tile that must stay clear.</summary>
    private static HashSet<Vector2I> CarveRoad(StageMap map, RandomNumberGenerator rng)
    {
        var at = new Vector2I(map.W / 2, map.H - 2);
        map.Start = at;
        map.Road.Add(at);
        while (at.Y > 1)
        {
            if (rng.Randf() < 0.35f)
            {
                int di = rng.Randf() < 0.5f ? -1 : 1;
                if (at.X + di is >= 2 and <= Width - 3)
                {
                    at += new Vector2I(di, 0);
                    map.Road.Add(at);
                }
            }
            at += new Vector2I(0, -1);
            map.Road.Add(at);
        }
        map.Goal = at;
        foreach (var tile in map.Road)
            map.Kind[tile.X, tile.Y] = TileKind.Path;

        // The road plus one tile either side stays clear so fights have room.
        var clear = new HashSet<Vector2I>();
        foreach (var tile in map.Road)
            for (int di = -1; di <= 1; di++)
                if (map.InBounds(tile.X + di, tile.Y))
                    clear.Add(new Vector2I(tile.X + di, tile.Y));
        return clear;
    }

    /// <summary>
    /// The puzzle sits in a pocket beside the road near the middle of the stage;
    /// the treasure chest hides on the opposite side further along.
    /// </summary>
    private static void ReservePuzzleAndTreasure(StageMap map, RandomNumberGenerator rng, HashSet<Vector2I> reserved)
    {
        int mid = map.H / 2;
        var roadAtMid = map.Road.First(t => t.Y == mid);
        int side = roadAtMid.X <= map.W / 2 ? 1 : -1;
        int pocketX = side > 0 ? map.W - 2 : 1;
        for (int j = mid - 2; j <= mid + 1; j++)
        {
            var tile = new Vector2I(pocketX, j);
            map.PuzzleTiles.Add(tile);
            reserved.Add(tile);
            reserved.Add(new Vector2I(pocketX - side, j));
        }
        map.PuzzleChest = new Vector2I(pocketX, mid + 2);
        reserved.Add(map.PuzzleChest);

        int tj = map.H / 4;
        var roadAt = map.Road.First(t => t.Y == tj);
        int tx = roadAt.X <= map.W / 2 ? map.W - 2 : 1;
        map.TreasureChest = new Vector2I(tx, tj);
        reserved.Add(map.TreasureChest);
        reserved.Add(new Vector2I(tx + (tx == 1 ? 1 : -1), tj));
    }

    private static bool Free(StageMap map, HashSet<Vector2I> reserved, int i, int j) =>
        map.InBounds(i, j) && !reserved.Contains(new Vector2I(i, j)) && map.Kind[i, j] != TileKind.Water
        && map.Height[i, j] == 0 && !map.Decor.Any(d => d.Tile == new Vector2I(i, j));

    /// <summary>Raised rocky blocks along the edges of the strip (FFT-style cliffs).</summary>
    private static void Cliffs(StageMap map, RandomNumberGenerator rng, HashSet<Vector2I> reserved, int count)
    {
        for (int n = 0, tries = 0; n < count && tries < 80; tries++)
        {
            int len = rng.RandiRange(2, 4);
            int j0 = rng.RandiRange(1, map.H - len - 1);
            int i0 = rng.Randf() < 0.5f ? 0 : map.W - 2;
            bool ok = true;
            for (int i = i0; i < i0 + 2 && ok; i++)
                for (int j = j0; j < j0 + len && ok; j++)
                    ok = !reserved.Contains(new Vector2I(i, j)) && map.Kind[i, j] != TileKind.Water;
            if (!ok)
                continue;
            int height = rng.RandiRange(1, 2);
            for (int i = i0; i < i0 + 2; i++)
                for (int j = j0; j < j0 + len; j++)
                {
                    map.Height[i, j] = height;
                    map.Kind[i, j] = TileKind.Grass;
                }
            n++;
        }
    }

    private static void Scatter(StageMap map, RandomNumberGenerator rng, HashSet<Vector2I> reserved, DecorKind kind, int count)
    {
        for (int n = 0, tries = 0; n < count && tries < 300; tries++)
        {
            int i = rng.RandiRange(0, map.W - 1), j = rng.RandiRange(0, map.H - 1);
            if (!Free(map, reserved, i, j))
                continue;
            var jitter = new Vector2(rng.RandfRange(-10, 10), rng.RandfRange(-4, 4));
            map.Decor.Add(new DecorSpec(kind, new Vector2I(i, j), jitter, rng.RandiRange(0, 9999)));
            n++;
        }
    }

    /// <summary>A landmark on the free tile nearest row <paramref name="row"/>, beside the road.</summary>
    private static void Landmark(StageMap map, RandomNumberGenerator rng, HashSet<Vector2I> reserved, DecorKind kind, int row)
    {
        Vector2I? best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < map.W; i++)
            for (int j = 0; j < map.H; j++)
            {
                if (!Free(map, reserved, i, j))
                    continue;
                float score = Mathf.Abs(j - row) * 2f + (reserved.Contains(new Vector2I(i + 1, j)) || reserved.Contains(new Vector2I(i - 1, j)) ? 0f : 1f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = new Vector2I(i, j);
                }
            }
        if (best is { } tile)
            map.Decor.Add(new DecorSpec(kind, tile, Vector2.Zero, rng.RandiRange(0, 9999)));
    }

    private static void Fences(StageMap map, RandomNumberGenerator rng, HashSet<Vector2I> reserved, int count)
    {
        // Fences line the road edges.
        foreach (var tile in map.Road.Where((_, k) => k % 3 == 0).Take(count))
        {
            foreach (int di in new[] { -2, 2 })
            {
                int i = tile.X + di;
                if (Free(map, reserved, i, tile.Y))
                {
                    map.Decor.Add(new DecorSpec(DecorKind.Fence, new Vector2I(i, tile.Y), Vector2.Zero, rng.RandiRange(0, 9999)));
                    break;
                }
            }
        }
    }

    /// <summary>A two-tile river across the strip at row <paramref name="j"/>; the road crosses on a bridge.</summary>
    private static void River(StageMap map, HashSet<Vector2I> reserved, int j, RandomNumberGenerator rng)
    {
        for (int i = 0; i < map.W; i++)
        {
            foreach (int k in new[] { j, j + 1 })
            {
                var tile = new Vector2I(i, k);
                if (map.Kind[i, k] == TileKind.Path)
                {
                    map.Kind[i, k] = TileKind.Bridge;
                }
                else if (!map.PuzzleTiles.Contains(tile) && tile != map.PuzzleChest && tile != map.TreasureChest)
                {
                    map.Kind[i, k] = TileKind.Water;
                    map.Height[i, k] = 0;
                }
            }
        }
        map.Decor.Add(new DecorSpec(DecorKind.Waterfall, new Vector2I(map.W - 1, j), new Vector2(18, 10), rng.RandiRange(0, 9999)));
    }

    private static void AddBridgePosts(StageMap map, RandomNumberGenerator rng)
    {
        for (int i = 0; i < map.W; i++)
            for (int j = 0; j < map.H; j++)
                if (map.Kind[i, j] == TileKind.Water && rng.Randf() < 0.2f)
                    map.Decor.Add(new DecorSpec(DecorKind.BridgePost, new Vector2I(i, j), new Vector2(rng.RandfRange(-8, 8), 0), rng.RandiRange(0, 9999)));
    }

    private static void Corrupt(StageMap map, RandomNumberGenerator rng, int count)
    {
        for (int n = 0; n < count; n++)
        {
            int i = rng.RandiRange(0, map.W - 1), j = rng.RandiRange(0, map.H - 1);
            if (map.Kind[i, j] == TileKind.Grass)
                map.Kind[i, j] = TileKind.Corrupted;
        }
    }

    private static void StonePatches(StageMap map, RandomNumberGenerator rng, int count)
    {
        for (int n = 0; n < count; n++)
        {
            int i = rng.RandiRange(0, map.W - 1), j = rng.RandiRange(0, map.H - 1);
            if (map.Kind[i, j] is TileKind.Grass or TileKind.Path)
                map.Kind[i, j] = TileKind.Stone;
        }
    }
}
