using System.Linq;
using Godot;

namespace RambleRumble;

/// <summary>Which hand-shaped battlefield a stage uses (see <see cref="StageMapBuilder"/>).</summary>
/// <summary>Puzzles that unlock each stage's second chest.</summary>
public enum PuzzleKind
{
    /// <summary>Step on the numbered plates in order.</summary>
    RunePlates,
    /// <summary>Light all braziers within a few seconds of each other.</summary>
    Braziers,
    /// <summary>Each lever flips itself and its neighbours; turn them all on.</summary>
    Levers,
    /// <summary>Watch the crystals flash a sequence, then step on them in that order.</summary>
    EchoCrystals,
}

public enum StageLayout
{
    Fields,
    Swarm,
    Camp,
    Windmill,
    Bridge,
    PlainsBoss,
    ForestEdge,
    Grove,
    Waterfall,
    Ruin,
    AncientTree,
    ForestBoss,
}

/// <param name="Leader">Species of a tougher labelled leader that joins this wave ("" = none).</param>
public record Wave((string Species, int Count)[] Enemies, string Leader = "");

/// <param name="Scale">Multiplier on enemy HP and damage.</param>
/// <param name="Boss">Boss species that appears in the last wave ("" = none).</param>
/// <param name="Corrupted">Enemies show the red streaks of corrupted Rumble.</param>
/// <param name="SkillBoxFirstClear">The first clear also opens a skill box.</param>
public record StageDef(string Id, int Chapter, int Number, string Name, string Landmark, StageLayout Layout,
    int Level, float Scale, Wave[] Waves, int Gold, int Xp, string Boss = "", bool Corrupted = false,
    bool SkillBoxFirstClear = false, string Story = "")
{
    public bool IsBoss => Boss != "";
    public string Title => $"{Id}  {Name}";

    /// <summary>Suggested Combat Power for a comfortable clear.</summary>
    public int RecommendedCp => 100 + 30 * Level;

    /// <summary>The puzzle guarding this stage's second chest; the four kinds take turns.</summary>
    public PuzzleKind Puzzle => (PuzzleKind)((Number - 1) % 4);

    /// <summary>Distinct enemy species in this stage, boss last.</summary>
    public string[] EnemySpecies => Waves.SelectMany(w => w.Enemies.Select(e => e.Species))
        .Concat(Waves.Where(w => w.Leader != "").Select(w => w.Leader))
        .Concat(IsBoss ? new[] { Boss } : System.Array.Empty<string>()).Distinct().ToArray();

    /// <summary>Materials this stage can give (enemy drops and chests).</summary>
    public string[] PossibleMaterials => EnemySpecies.Select(GameConfig.MaterialFrom)
        .Concat(GameConfig.ChapterMaterials(Chapter)).Append("Crystal Shard").Distinct().ToArray();
}

/// <param name="Route">Stage node positions on the chapter map, 0..1 of the map area.</param>
/// <param name="Landmarks">Landmark labels on the chapter map, 0..1 of the map area.</param>
public record ChapterMapDef(int Number, string Region, string Blurb, Color Ground, Color GroundDark, Color Accent,
    Vector2[] Route, (string Name, Vector2 At)[] Landmarks, StageDef[] Stages);

/// <summary>
/// Chapter maps in the Alien Creeps style: a winding road of 10 stages per
/// chapter, each played on its own small isometric battlefield.
/// Chapter 1 is the Whispering Plains and Chapter 2 the Verdant Forest.
/// </summary>
public static class StageCatalog
{
    private static (string, int)[] E(params (string, int)[] enemies) => enemies;

    public static readonly ChapterMapDef[] Chapters =
    {
        new(1, "Whispering Plains", "Wide open fields filled with slimes and other early monsters. A perfect place for new Ramblers.",
            new Color(0.47f, 0.70f, 0.34f), new Color(0.36f, 0.56f, 0.27f), new Color(0.95f, 0.85f, 0.35f),
            new Vector2[]
            {
                new(0.08f, 0.82f), new(0.20f, 0.70f), new(0.27f, 0.50f), new(0.17f, 0.32f), new(0.31f, 0.17f),
                new(0.48f, 0.27f), new(0.56f, 0.47f), new(0.70f, 0.58f), new(0.82f, 0.40f), new(0.90f, 0.18f),
            },
            new[] { ("Slime Fields", new Vector2(0.12f, 0.56f)), ("Traveler's Camp", new Vector2(0.42f, 0.09f)),
                ("Old Windmill", new Vector2(0.62f, 0.27f)), ("Broken Bridge", new Vector2(0.80f, 0.66f)) },
            new StageDef[]
            {
                new("1-1", 1, 1, "Greenfield Road", "Slime Fields", StageLayout.Fields, 1, 1.00f,
                    new[] { new Wave(E(("Slime", 3))), new Wave(E(("Slime", 4))) }, 30, 40),
                new("1-2", 1, 2, "Restless Meadow", "Slime Fields", StageLayout.Fields, 2, 1.10f,
                    new[] { new Wave(E(("Slime", 4))), new Wave(E(("Slime", 5))) }, 35, 50),
                new("1-3", 1, 3, "Red in the Grass", "Slime Fields", StageLayout.Swarm, 3, 1.15f,
                    new[] { new Wave(E(("Slime", 5))), new Wave(E(("Slime", 3)), Leader: "Slime") }, 60, 100,
                    Corrupted: true, Story: "The red-streaked slime swarm heading for Hearthwood."),
                new("1-4", 1, 4, "Traveler's Camp", "Traveler's Camp", StageLayout.Camp, 4, 1.25f,
                    new[] { new Wave(E(("Slime", 3), ("Wolf", 1))), new Wave(E(("Wolf", 2), ("Slime", 2))) }, 45, 70),
                new("1-5", 1, 5, "Camp at Dusk", "Traveler's Camp", StageLayout.Camp, 5, 1.30f,
                    new[] { new Wave(E(("Wolf", 2))), new Wave(E(("Wolf", 2), ("Slime", 3))), new Wave(E(("Wolf", 3))) }, 50, 80,
                    SkillBoxFirstClear: true),
                new("1-6", 1, 6, "Old Windmill", "Old Windmill", StageLayout.Windmill, 6, 1.35f,
                    new[] { new Wave(E(("Slime", 4), ("Aqua Slime", 1))), new Wave(E(("Wolf", 2), ("Aqua Slime", 2))) }, 55, 90),
                new("1-7", 1, 7, "Windmill Hill", "Old Windmill", StageLayout.Windmill, 7, 1.40f,
                    new[] { new Wave(E(("Aqua Slime", 3), ("Wolf", 2))), new Wave(E(("Wolf", 4))) }, 60, 100),
                new("1-8", 1, 8, "Broken Bridge", "Broken Bridge", StageLayout.Bridge, 8, 1.50f,
                    new[] { new Wave(E(("Aqua Slime", 4))), new Wave(E(("Aqua Slime", 3), ("Wolf", 2))) }, 65, 110),
                new("1-9", 1, 9, "River Crossing", "Broken Bridge", StageLayout.Bridge, 9, 1.55f,
                    new[] { new Wave(E(("Aqua Slime", 3), ("Wolf", 3))), new Wave(E(("Wolf", 3), ("Slime", 4))), new Wave(E(("Aqua Slime", 4))) },
                    70, 120),
                new("1-10", 1, 10, "The Slime King", "Slime Fields", StageLayout.PlainsBoss, 10, 1.60f,
                    new[] { new Wave(E(("Slime", 4))), new Wave(E(("Slime", 3))) }, 200, 400,
                    Boss: "Slime King", Corrupted: true, SkillBoxFirstClear: true,
                    Story: "A giant slime pulsing with red Rumble rules the fields."),
            }),
        new(2, "Verdant Forest", "A deep forest where the trees whisper. Home to wolves, fairies, and mysterious creatures.",
            new Color(0.25f, 0.48f, 0.26f), new Color(0.17f, 0.36f, 0.19f), new Color(0.55f, 0.95f, 0.65f),
            new Vector2[]
            {
                new(0.08f, 0.24f), new(0.20f, 0.40f), new(0.14f, 0.63f), new(0.29f, 0.80f), new(0.43f, 0.63f),
                new(0.48f, 0.40f), new(0.62f, 0.24f), new(0.75f, 0.42f), new(0.70f, 0.67f), new(0.88f, 0.80f),
            },
            new[] { ("Hidden Grove", new Vector2(0.10f, 0.86f)), ("Waterfall", new Vector2(0.38f, 0.30f)),
                ("Forest Ruin", new Vector2(0.62f, 0.10f)), ("Ancient Tree", new Vector2(0.86f, 0.55f)) },
            new StageDef[]
            {
                new("2-1", 2, 1, "Forest Edge", "Forest Edge", StageLayout.ForestEdge, 11, 1.70f,
                    new[] { new Wave(E(("Shroom", 3))), new Wave(E(("Shroom", 3), ("Slime", 2))) }, 80, 140),
                new("2-2", 2, 2, "Whispering Trees", "Forest Edge", StageLayout.ForestEdge, 12, 1.80f,
                    new[] { new Wave(E(("Shroom", 4))), new Wave(E(("Wolf", 2), ("Shroom", 2))) }, 85, 150),
                new("2-3", 2, 3, "Hidden Grove", "Hidden Grove", StageLayout.Grove, 13, 1.90f,
                    new[] { new Wave(E(("Shroom", 3), ("Wolf", 1))), new Wave(E(("Wolf", 3))) }, 90, 160),
                new("2-4", 2, 4, "Merchant in Peril", "Hidden Grove", StageLayout.Grove, 14, 2.00f,
                    new[] { new Wave(E(("Wolf", 3))), new Wave(E(("Goblin", 1), ("Wolf", 2))) }, 95, 170,
                    Story: "Wolves have cornered a travelling merchant."),
                new("2-5", 2, 5, "Waterfall Path", "Waterfall", StageLayout.Waterfall, 15, 2.10f,
                    new[] { new Wave(E(("Goblin", 2))), new Wave(E(("Goblin", 2), ("Shroom", 3))) }, 100, 180,
                    SkillBoxFirstClear: true),
                new("2-6", 2, 6, "Forest Ruin", "Forest Ruin", StageLayout.Ruin, 16, 2.20f,
                    new[] { new Wave(E(("Goblin", 3))), new Wave(E(("Goblin", 2), ("Wolf", 2))) }, 105, 190),
                new("2-7", 2, 7, "Ruin Depths", "Forest Ruin", StageLayout.Ruin, 17, 2.30f,
                    new[] { new Wave(E(("Goblin", 3), ("Shroom", 2))), new Wave(E(("Goblin", 4))) }, 110, 200),
                new("2-8", 2, 8, "Ancient Roots", "Ancient Tree", StageLayout.AncientTree, 18, 2.40f,
                    new[] { new Wave(E(("Wolf", 4))), new Wave(E(("Goblin", 3), ("Wolf", 2))) }, 115, 210),
                new("2-9", 2, 9, "The Ancient Tree", "Ancient Tree", StageLayout.AncientTree, 19, 2.50f,
                    new[] { new Wave(E(("Goblin", 3), ("Wolf", 3))), new Wave(E(("Wolf", 5))) }, 120, 220),
                new("2-10", 2, 10, "Crimson Fang", "Ancient Tree", StageLayout.ForestBoss, 20, 2.60f,
                    new[] { new Wave(E(("Wolf", 3))), new Wave(E(("Wolf", 2))) }, 400, 700,
                    Boss: "Crimson Fang", Corrupted: true, SkillBoxFirstClear: true,
                    Story: "A giant wolf marked by glowing red veins guards the clearing."),
            }),
    };

    public static ChapterMapDef Chapter(int number) => Chapters.First(c => c.Number == number);
    public static StageDef Get(string id) => Chapters.SelectMany(c => c.Stages).First(s => s.Id == id);
    public static bool Exists(string id) => Chapters.SelectMany(c => c.Stages).Any(s => s.Id == id);
}
