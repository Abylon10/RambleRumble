using Godot;

namespace RambleRumble;

/// <summary>
/// Isometric (2:1 diamond) tile math for the stage battlefields. Tile (i, j)
/// grows to the lower-right with i and to the lower-left with j, so the
/// map's back corner is tile (0, 0) at the top of the screen.
/// </summary>
public static class Iso
{
    public const float TileW = 64f;
    public const float TileH = 32f;
    /// <summary>Screen pixels per height level.</summary>
    public const float Step = 14f;

    /// <summary>Ground-level screen position of a tile-space point (corners are whole numbers).</summary>
    public static Vector2 ToScreen(float i, float j) => new((i - j) * TileW / 2f, (i + j) * TileH / 2f);

    public static Vector2 TileCenter(int i, int j) => ToScreen(i + 0.5f, j + 0.5f);

    /// <summary>Tile under a ground-level screen position.</summary>
    public static Vector2I ToTile(Vector2 p)
    {
        float a = p.X / (TileW / 2f);
        float b = p.Y / (TileH / 2f);
        return new Vector2I(Mathf.FloorToInt((a + b) / 2f), Mathf.FloorToInt((b - a) / 2f));
    }

    /// <summary>Top diamond of a tile raised by <paramref name="lift"/> pixels: north, east, south, west.</summary>
    public static Vector2[] Diamond(int i, int j, float lift = 0f)
    {
        var up = new Vector2(0, lift);
        return new[] { ToScreen(i, j) - up, ToScreen(i + 1, j) - up, ToScreen(i + 1, j + 1) - up, ToScreen(i, j + 1) - up };
    }
}
