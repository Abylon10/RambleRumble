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

/// <param name="Route">Stage circle positions painted on <paramref name="MapArt"/>, 0..1 of the picture.</param>
/// <param name="MapArt">The painted chapter map (cut from the UI mockup by tools/cut_ui.py).</param>
public record ChapterMapDef(int Number, string Region, string Blurb, Color Ground, Color GroundDark, Color Accent,
    Vector2[] Route, string MapArt, StageDef[] Stages);

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
                new(0.159f, 0.310f), new(0.255f, 0.342f), new(0.368f, 0.226f), new(0.448f, 0.365f), new(0.625f, 0.423f),
                new(0.360f, 0.710f), new(0.453f, 0.839f), new(0.566f, 0.800f), new(0.682f, 0.768f), new(0.871f, 0.803f),
            },
            "res://art/ui/chapter1_map.png",
            new StageDef[]
            {
                new("1-1", 1, 1, "Greenfield Road", "Slime Fields", StageLayout.Fields, 1, 1.00f,
                    new[] { new Wave(E(("Green Slime", 3))), new Wave(E(("Green Slime", 2), ("Red Slime", 2))) }, 30, 40),
                new("1-2", 1, 2, "Restless Meadow", "Slime Fields", StageLayout.Fields, 2, 1.05f,
                    new[] { new Wave(E(("Red Slime", 3), ("Blue Slime", 1))), new Wave(E(("Blue Slime", 3), ("Green Slime", 2))) }, 35, 50),
                new("1-3", 1, 3, "Red in the Grass", "Slime Fields", StageLayout.Swarm, 3, 1.05f,
                    new[] { new Wave(E(("Red Slime", 4), ("Green Slime", 2))), new Wave(E(("Red Slime", 3)), Leader: "Red Slime") }, 60, 100,
                    Corrupted: true, Story: "The red-streaked slime swarm heading for Hearthwood."),
                new("1-4", 1, 4, "Traveler's Camp", "Traveler's Camp", StageLayout.Camp, 4, 1.05f,
                    new[] { new Wave(E(("Slime Plimt", 3), ("Sproutling", 2))), new Wave(E(("Sproutling", 3), ("Field Rat", 2))) }, 45, 70),
                new("1-5", 1, 5, "Camp at Dusk", "Traveler's Camp", StageLayout.Camp, 5, 1.05f,
                    new[] { new Wave(E(("Field Rat", 3))), new Wave(E(("Grasshopper", 3), ("Field Rat", 1))),
                        new Wave(E(("Sproutling", 2), ("Grasshopper", 2)), Leader: "Thorn Slime") }, 50, 80,
                    SkillBoxFirstClear: true),
                new("1-6", 1, 6, "Old Windmill", "Old Windmill", StageLayout.Windmill, 6, 1.05f,
                    new[] { new Wave(E(("Beelet", 3), ("Grasshopper", 2))), new Wave(E(("Plains Boar", 2), ("Beelet", 2)), Leader: "Rock Slime") }, 55, 90),
                new("1-7", 1, 7, "Windmill Hill", "Old Windmill", StageLayout.Windmill, 7, 1.10f,
                    new[] { new Wave(E(("Plains Boar", 3))), new Wave(E(("Scarecrow", 2), ("Beelet", 3)), Leader: "Horned Boar") }, 60, 100),
                new("1-8", 1, 8, "Broken Bridge", "Broken Bridge", StageLayout.Bridge, 8, 1.10f,
                    new[] { new Wave(E(("Scarecrow", 3), ("Field Rat", 2))), new Wave(E(("Plains Boar", 2), ("Wild Dog", 2)), Leader: "Wisp Spirit") }, 65, 110),
                new("1-9", 1, 9, "River Crossing", "Broken Bridge", StageLayout.Bridge, 9, 1.10f,
                    new[] { new Wave(E(("Wild Dog", 3), ("Scarecrow", 2))), new Wave(E(("Rock Slime", 2), ("Thorn Slime", 2))),
                        new Wave(E(("Horned Boar", 2)), Leader: "Rambling Knight") },
                    70, 120),
                new("1-10", 1, 10, "Crimson Fang", "Slime Fields", StageLayout.PlainsBoss, 10, 1.10f,
                    new[] { new Wave(E(("Wild Dog", 3))), new Wave(E(("Wild Dog", 2), ("Horned Boar", 1))) }, 200, 400,
                    Boss: "Crimson Fang", Corrupted: true, SkillBoxFirstClear: true,
                    Story: "A once-peaceful wolf corrupted by unknown energy guards the end of the plains."),
            }),
        new(2, "Verdant Forest", "A deep forest where the trees whisper. Home to wolves, sprites, goblins and walking trees.",
            new Color(0.25f, 0.48f, 0.26f), new Color(0.17f, 0.36f, 0.19f), new Color(0.55f, 0.95f, 0.65f),
            new Vector2[]
            {
                new(0.220f, 0.396f), new(0.349f, 0.268f), new(0.523f, 0.346f), new(0.777f, 0.275f), new(0.840f, 0.429f),
                new(0.657f, 0.625f), new(0.574f, 0.693f), new(0.491f, 0.757f), new(0.311f, 0.614f), new(0.323f, 0.839f),
            },
            "res://art/ui/chapter2_map.png",
            new StageDef[]
            {
                new("2-1", 2, 1, "Forest Edge", "Forest Edge", StageLayout.ForestEdge, 11, 1.25f,
                    new[] { new Wave(E(("Forest Slime", 3))), new Wave(E(("Forest Slime", 2), ("Wood Sprite", 2))) }, 80, 140),
                new("2-2", 2, 2, "Whispering Trees", "Forest Edge", StageLayout.ForestEdge, 12, 1.25f,
                    new[] { new Wave(E(("Wood Sprite", 3), ("Mushcap", 1))), new Wave(E(("Mushcap", 3), ("Leafy", 2))) }, 85, 150),
                new("2-3", 2, 3, "Hidden Grove", "Hidden Grove", StageLayout.Grove, 13, 1.30f,
                    new[] { new Wave(E(("Leafy", 3), ("Forest Rabbit", 2))), new Wave(E(("Forest Rabbit", 3), ("Mushcap", 2)), Leader: "Poison Cap") }, 90, 160),
                new("2-4", 2, 4, "Merchant in Peril", "Hidden Grove", StageLayout.Grove, 14, 1.30f,
                    new[] { new Wave(E(("Green Wolf", 3))), new Wave(E(("Green Wolf", 2), ("Forest Rabbit", 2)), Leader: "Dire Wolf") }, 95, 170,
                    Story: "Wolves have cornered a travelling merchant."),
                new("2-5", 2, 5, "Waterfall Path", "Waterfall", StageLayout.Waterfall, 15, 1.35f,
                    new[] { new Wave(E(("Goblin Scout", 3))), new Wave(E(("Goblin Scout", 2), ("Treantling", 2))) }, 100, 180,
                    SkillBoxFirstClear: true),
                new("2-6", 2, 6, "Forest Ruin", "Forest Ruin", StageLayout.Ruin, 16, 1.35f,
                    new[] { new Wave(E(("Treantling", 3), ("Sporeling", 1))), new Wave(E(("Goblin Scout", 3), ("Sporeling", 2)), Leader: "Goblin Brute") }, 105, 190),
                new("2-7", 2, 7, "Ruin Depths", "Forest Ruin", StageLayout.Ruin, 17, 1.40f,
                    new[] { new Wave(E(("Sporeling", 3), ("Forest Bat", 2))), new Wave(E(("Forest Bat", 3), ("Goblin Scout", 2)), Leader: "Shadow Bat") }, 110, 200),
                new("2-8", 2, 8, "Ancient Roots", "Ancient Tree", StageLayout.AncientTree, 18, 1.40f,
                    new[] { new Wave(E(("Treantling", 3), ("Green Wolf", 2))), new Wave(E(("Dire Wolf", 1), ("Poison Cap", 2), ("Forest Bat", 2)), Leader: "Ancient Treant") }, 115, 210),
                new("2-9", 2, 9, "The Ancient Tree", "Ancient Tree", StageLayout.AncientTree, 19, 1.45f,
                    new[] { new Wave(E(("Goblin Brute", 2), ("Treantling", 2))), new Wave(E(("Ancient Treant", 1), ("Shadow Bat", 2))),
                        new Wave(E(("Sporeling", 3)), Leader: "Entling Shaman") }, 120, 220),
                new("2-10", 2, 10, "Elderwood Guardian", "Ancient Tree", StageLayout.ForestBoss, 20, 1.50f,
                    new[] { new Wave(E(("Treantling", 3))), new Wave(E(("Entling Shaman", 1), ("Treantling", 2))) }, 400, 700,
                    Boss: "Elderwood Guardian", Corrupted: true, SkillBoxFirstClear: true,
                    Story: "The forest's ancient guardian tree, now corrupted, attacks anything near."),
            }),
    };

    public static ChapterMapDef Chapter(int number) => Chapters.First(c => c.Number == number);
    public static StageDef Get(string id) => Chapters.SelectMany(c => c.Stages).First(s => s.Id == id);
    public static bool Exists(string id) => Chapters.SelectMany(c => c.Stages).Any(s => s.Id == id);
}
