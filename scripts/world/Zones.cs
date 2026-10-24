using System.Linq;
using Godot;

namespace RambleRumble;

/// <summary>A Wilderness zone: where it is, how it looks, and what spawns there.</summary>
/// <param name="Id">Map letter (A–G).</param>
/// <param name="Tier">Difficulty label shown to the player.</param>
/// <param name="Cells">Grid cells the zone covers.</param>
/// <param name="Cap">Most monsters alive in this zone at once.</param>
/// <param name="Respawn">Seconds between spawns while under the cap.</param>
public record ZoneInfo(string Id, string Name, string Tier, Color TierColor, Rect2[] Cells, Color Ground,
    Color Accent, (string Species, int Weight)[] Spawns, int Cap, float Respawn)
{
    public bool Contains(Vector2 point) => Cells.Any(c => c.HasPoint(point));
    public string Title => $"{Name} ({Tier})";
}

/// <summary>
/// Wilderness layout from the world map concept: a 3x3 grid of cells, with
/// difficulty rising away from the Starter Plains gate in the south-west.
/// <code>
///   F Volcanic   E Crystal   G Ruins
///   B Forest     D Rocky     D Rocky
///   A Starter    C River     C River
/// </code>
/// </summary>
public static class Zones
{
    public static readonly Rect2 WildRect = new(1600, -1350, 3600, 2700);
    private const float CellW = 1200f;
    private const float CellH = 900f;

    private static Rect2 Cell(int col, int row) =>
        new(WildRect.Position.X + col * CellW, WildRect.Position.Y + row * CellH, CellW, CellH);

    public static readonly ZoneInfo[] All =
    {
        new("A", "Starter Plains", "Low Level", new Color(0.75f, 0.95f, 0.6f), new[] { Cell(0, 2) },
            new Color(0.40f, 0.62f, 0.32f), new Color(0.95f, 0.85f, 0.35f),
            new[] { ("Slime", 1) }, 6, 3f),
        new("B", "Forest Zone", "Common", GameConfig.RarityColor("Common"), new[] { Cell(0, 1) },
            new Color(0.22f, 0.40f, 0.20f), new Color(0.12f, 0.28f, 0.12f),
            new[] { ("Slime", 40), ("Shroom", 60) }, 7, 3f),
        new("C", "River Area", "Uncommon", GameConfig.RarityColor("Uncommon"), new[] { Cell(1, 2), Cell(2, 2) },
            new Color(0.30f, 0.52f, 0.36f), new Color(0.25f, 0.55f, 0.85f),
            new[] { ("Aqua Slime", 60), ("Wolf", 40) }, 9, 3f),
        new("D", "Rocky Hills", "Rare", GameConfig.RarityColor("Rare"), new[] { Cell(1, 1), Cell(2, 1) },
            new Color(0.52f, 0.42f, 0.30f), new Color(0.40f, 0.33f, 0.26f),
            new[] { ("Goblin", 50), ("Boar", 50) }, 9, 4f),
        new("E", "Crystal Caves", "Epic", GameConfig.RarityColor("Epic"), new[] { Cell(1, 0) },
            new Color(0.20f, 0.17f, 0.32f), new Color(0.55f, 0.45f, 1.00f),
            new[] { ("Crystal Bat", 1) }, 6, 5f),
        new("F", "Volcanic Ridge", "Legendary", GameConfig.RarityColor("Legendary"), new[] { Cell(0, 0) },
            new Color(0.30f, 0.14f, 0.10f), new Color(1.00f, 0.45f, 0.10f),
            new[] { ("Fire Imp", 1) }, 5, 6f),
        new("G", "Ancient Ruins", "Boss", GameConfig.RarityColor("Mythic"), new[] { Cell(2, 0) },
            new Color(0.32f, 0.36f, 0.36f), new Color(0.35f, 0.90f, 0.85f),
            new[] { ("Stone Golem", 1) }, 1, 45f),
    };

    /// <summary>Travel gate back to the Lobby, and where players arrive from it.</summary>
    public static readonly Vector2 Gate = new(1720, 1180);
    public static readonly Vector2 ArrivalPoint = new(1880, 1050);

    /// <summary>Campfire rest spots, on zone corners.</summary>
    public static readonly Vector2[] Campfires = { new(2800, 450), new(2800, -450), new(4000, -450) };
    public const float CampfireRadius = 90f;

    /// <summary>Where world bosses and swarm events will appear (Phase 3).</summary>
    public static readonly Vector2 SwarmMarker = new(4600, 900);

    public static ZoneInfo? At(Vector2 point) => All.FirstOrDefault(z => z.Contains(point));
    public static ZoneInfo ById(string id) => All.First(z => z.Id == id);
}
