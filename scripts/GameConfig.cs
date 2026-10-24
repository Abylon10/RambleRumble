using System.Collections.Generic;
using System;
using System.Linq;
using Godot;

namespace RambleRumble;

/// <summary>
/// Game balance data from the v1.1 proposal. Every number here is a starting
/// assumption to be tuned in playtests.
/// </summary>
public static class GameConfig
{
    // `record` gives a small immutable data type with value equality for free.
    /// <param name="PetChance">Chance per kill to drop a pet.</param>
    /// <param name="Pity">Guaranteed pet after this many kills without one (0 = no pity).</param>
    /// <param name="Income">Fence gold per minute for a level 1, 1★, Normal-size pet.</param>
    /// <param name="ReleaseGold">Gold paid (with one Star Shard) when releasing a pet.</param>
    public record RarityInfo(string Name, float PetChance, float BoxChance, int Pity, float Income, int ReleaseGold, Color Color);

    /// <param name="Mult">Applies to HP and fence income only, not damage.</param>
    public record SizeInfo(string Name, float Chance, float Scale, float Mult);

    /// <param name="Copies">Duplicates consumed to reach this rank.</param>
    /// <param name="Cost">Gold cost of the merge that reaches this rank.</param>
    public record StarInfo(int MaxLevel, float Mult, int Copies, int Cost);

    /// <param name="Level">Recommended player level; sets kill XP and whether a fight trains skill mastery.</param>
    public record SpeciesInfo(string Name, string Rarity, float Hp, float Damage, float Speed, float Radius,
        float PetPower, int GoldMin, int GoldMax, Color Color, int Level);

    /// <summary>Cost of the next level = BaseCost * Growth ^ current level.</summary>
    public record UpgradeInfo(string Id, string Name, string Desc, int Max, int BaseCost, float Growth);

    public static readonly RarityInfo[] Rarities =
    {
        new("Common", 0.25f, 0.10f, 0, 1f, 10, new Color(0.70f, 0.70f, 0.70f)),
        new("Uncommon", 0.18f, 0.12f, 0, 2f, 20, new Color(0.40f, 0.85f, 0.40f)),
        new("Rare", 0.10f, 0.15f, 25, 4f, 40, new Color(0.35f, 0.60f, 1.00f)),
        new("Epic", 0.05f, 0.20f, 50, 8f, 80, new Color(0.75f, 0.40f, 1.00f)),
        new("Legendary", 0.02f, 0.25f, 120, 16f, 160, new Color(1.00f, 0.65f, 0.10f)),
        new("Mythic", 0.005f, 0.30f, 400, 32f, 320, new Color(1.00f, 0.30f, 0.40f)),
    };

    public static readonly SizeInfo[] Sizes =
    {
        new("Tiny", 0.20f, 0.8f, 0.90f),
        new("Normal", 0.60f, 1.0f, 1.00f),
        new("Big", 0.15f, 1.25f, 1.10f),
        new("Giant", 0.045f, 1.5f, 1.20f),
        new("Titan", 0.005f, 2.0f, 1.35f),
    };

    // Index 0 is 1★.
    private static readonly StarInfo[] StarTable =
    {
        new(10, 1.00f, 0, 0),
        new(20, 1.25f, 1, 500),
        new(30, 1.60f, 2, 2500),
        new(40, 2.00f, 3, 10000),
        new(50, 2.50f, 4, 40000),
    };

    public const int MaxStars = 5;
    public const float PetLevelBonus = 0.04f;
    /// <summary>Star Shards may replace at most this fraction of a merge's copies (rounded down).</summary>
    public const float ShardReplaceFraction = 0.5f;

    public const int TreatCost = 25;
    public const int TreatXp = 20;
    public const float FenceXpPerMin = 2f;
    /// <summary>Fence income keeps accruing while the app is closed, up to this cap.</summary>
    public const float OfflineCapHours = 8f;

    public const int BaseFenceSlots = 3;
    /// <summary>Highest star rank reachable before any Pet Lab upgrades.</summary>
    public const int BaseMaxStars = 3;

    public static readonly UpgradeInfo[] Upgrades =
    {
        new("fence_slots", "Fence Slots", "+1 pet earning gold in the fence", 9, 200, 1.7f),
        new("energy_well", "Energy Well", "+10 max Rumble energy", 5, 800, 2.0f),
        new("training_ground", "Training Ground", "+25% training XP", 10, 300, 1.8f),
        new("pet_lab", "Pet Lab", "Unlocks the next star rank, -10% merge cost", 2, 5000, 5.0f),
    };

    /// <summary>Stats grow with the species' level; <paramref name="tough"/> scales HP and damage (elites ~1.5).</summary>
    private static SpeciesInfo M(string name, string rarity, int level, float speed, float radius, Color color, float tough = 1f)
    {
        int goldMin = 2 + (int)(level * 0.7f * tough);
        return new(name, rarity, (40f + 6f * level) * tough, (4.5f + 0.5f * level) * tough, speed, radius,
            (9f + 1.4f * level) * tough, goldMin, goldMin * 2 + 1, color, level);
    }

    /// <summary>The Chapter 1-2 monster roster; stages pick from it in StageCatalog.cs.</summary>
    public static readonly SpeciesInfo[] Species =
    {
        // Chapter 1: Whispering Plains.
        M("Green Slime", "Common", 1, 70f, 16f, new Color(0.40f, 0.82f, 0.38f)),
        M("Red Slime", "Common", 1, 75f, 16f, new Color(0.90f, 0.30f, 0.30f)),
        M("Blue Slime", "Common", 2, 80f, 16f, new Color(0.30f, 0.60f, 0.95f)),
        M("Slime Plimt", "Common", 3, 70f, 16f, new Color(0.55f, 0.82f, 0.35f)),
        M("Sproutling", "Common", 3, 85f, 15f, new Color(0.62f, 0.45f, 0.28f)),
        M("Field Rat", "Common", 4, 120f, 14f, new Color(0.62f, 0.45f, 0.30f)),
        M("Grasshopper", "Uncommon", 4, 130f, 15f, new Color(0.55f, 0.82f, 0.30f)),
        M("Beelet", "Uncommon", 5, 110f, 14f, new Color(0.98f, 0.80f, 0.25f)),
        M("Plains Boar", "Uncommon", 5, 125f, 19f, new Color(0.55f, 0.36f, 0.24f)),
        M("Scarecrow", "Uncommon", 6, 75f, 18f, new Color(0.70f, 0.50f, 0.28f)),
        M("Thorn Slime", "Rare", 6, 70f, 19f, new Color(0.35f, 0.72f, 0.30f), 1.5f),
        M("Rock Slime", "Rare", 7, 55f, 19f, new Color(0.58f, 0.58f, 0.62f), 1.6f),
        M("Wild Dog", "Rare", 7, 140f, 17f, new Color(0.85f, 0.55f, 0.28f), 1.4f),
        M("Horned Boar", "Rare", 8, 135f, 21f, new Color(0.50f, 0.30f, 0.20f), 1.5f),
        M("Wisp Spirit", "Rare", 8, 110f, 15f, new Color(0.45f, 0.65f, 1.00f), 1.4f),
        M("Rambling Knight", "Epic", 9, 85f, 19f, new Color(0.62f, 0.66f, 0.74f), 1.8f),
        // Chapter 2: Verdant Forest.
        M("Forest Slime", "Common", 11, 75f, 16f, new Color(0.30f, 0.70f, 0.35f)),
        M("Wood Sprite", "Common", 11, 115f, 13f, new Color(0.60f, 1.00f, 0.55f)),
        M("Mushcap", "Common", 12, 65f, 17f, new Color(0.88f, 0.25f, 0.22f)),
        M("Leafy", "Common", 12, 120f, 15f, new Color(0.72f, 0.62f, 0.52f)),
        M("Forest Rabbit", "Common", 13, 130f, 14f, new Color(0.92f, 0.86f, 0.76f)),
        M("Green Wolf", "Uncommon", 13, 125f, 18f, new Color(0.45f, 0.55f, 0.38f)),
        M("Goblin Scout", "Uncommon", 14, 100f, 17f, new Color(0.50f, 0.68f, 0.28f)),
        M("Treantling", "Uncommon", 14, 65f, 18f, new Color(0.55f, 0.38f, 0.22f)),
        M("Sporeling", "Uncommon", 15, 70f, 17f, new Color(0.65f, 0.35f, 0.85f)),
        M("Forest Bat", "Uncommon", 15, 145f, 15f, new Color(0.42f, 0.30f, 0.65f)),
        M("Dire Wolf", "Rare", 16, 140f, 20f, new Color(0.38f, 0.38f, 0.44f), 1.5f),
        M("Poison Cap", "Rare", 16, 65f, 19f, new Color(0.60f, 0.30f, 0.75f), 1.5f),
        M("Goblin Brute", "Rare", 17, 85f, 20f, new Color(0.45f, 0.62f, 0.25f), 1.6f),
        M("Ancient Treant", "Epic", 18, 55f, 22f, new Color(0.50f, 0.36f, 0.20f), 1.8f),
        M("Shadow Bat", "Rare", 18, 150f, 17f, new Color(0.35f, 0.22f, 0.58f), 1.5f),
        M("Entling Shaman", "Epic", 19, 75f, 19f, new Color(0.50f, 0.36f, 0.22f), 1.7f),
        // Chapter bosses.
        new("Crimson Fang", "Epic", 620f, 11f, 140f, 30f, 45f, 60, 90, new Color(0.30f, 0.28f, 0.32f), 10),
        new("Elderwood Guardian", "Legendary", 1400f, 18f, 60f, 34f, 70f, 140, 200, new Color(0.50f, 0.36f, 0.20f), 20),
    };

    /// <summary>Chapter bosses: they always drop materials and use the big sprite.</summary>
    public static bool IsBoss(string species) => species is "Crimson Fang" or "Elderwood Guardian";

    /// <summary>Species renamed or retired when the roster was redrawn, for old saves.</summary>
    public static readonly Dictionary<string, string> RenamedSpecies = new()
    {
        ["Slime"] = "Green Slime", ["Aqua Slime"] = "Blue Slime", ["Slime King"] = "Thorn Slime",
        ["Shroom"] = "Mushcap", ["Wolf"] = "Green Wolf", ["Boar"] = "Plains Boar", ["Goblin"] = "Goblin Scout",
        ["Crystal Bat"] = "Shadow Bat", ["Fire Imp"] = "Wisp Spirit", ["Stone Golem"] = "Ancient Treant",
    };

    // Skills (starters and the Ember lineage) live in SkillCatalog.cs.

    public const float BasicAttackCooldown = 0.45f;
    public const float BasicAttackDamage = 1.0f;
    public const float BasicAttackRange = 64f;

    /// <summary>Training XP per tap on the Train button, and per second spent in the zone.</summary>
    public const int TrainTapXp = 2;
    public const float TrainIdleXpPerSec = 1f;

    // --- Lookups and formulas ------------------------------------------------

    public static RarityInfo Rarity(string name) => Rarities.First(r => r.Name == name);
    public static int RarityIndex(string name) => Array.FindIndex(Rarities, r => r.Name == name);
    public static Color RarityColor(string name) => Rarity(name).Color;
    public static SpeciesInfo SpeciesOf(string name) => Species.First(s => s.Name == name);
    public static UpgradeInfo Upgrade(string id) => Upgrades.First(u => u.Id == id);
    public static StarInfo Star(int stars) => StarTable[stars - 1];
    public static SizeInfo Size(string name) => Sizes.FirstOrDefault(s => s.Name == name) ?? Sizes[1];

    public static int TrainingXpNeeded(int level) => 10 + 5 * level;

    // --- Materials, items and the Market -------------------------------------

    /// <param name="SellPrice">Gold the Market pays for one.</param>
    public record MaterialInfo(string Name, int SellPrice, Color Color);

    public static readonly MaterialInfo[] Materials =
    {
        new("Slime Gel", 4, new Color(0.45f, 0.85f, 0.45f)),
        new("Meadow Leaf", 5, new Color(0.55f, 0.80f, 0.30f)),
        new("Oak Wood", 6, new Color(0.62f, 0.45f, 0.26f)),
        new("Mushroom Cap", 8, new Color(0.85f, 0.30f, 0.25f)),
        new("Forest Herb", 9, new Color(0.30f, 0.70f, 0.50f)),
        new("Wolf Fang", 10, new Color(0.92f, 0.90f, 0.82f)),
        new("Goblin Scrap", 14, new Color(0.62f, 0.62f, 0.66f)),
        new("Crystal Shard", 40, new Color(0.55f, 0.75f, 1.00f)),
    };

    public static MaterialInfo Material(string name) => Materials.First(m => m.Name == name);

    /// <summary>What each species may drop (35% per kill; bosses always drop three).</summary>
    public static string MaterialFrom(string species) => species switch
    {
        "Green Slime" or "Red Slime" or "Blue Slime" or "Thorn Slime" or "Rock Slime" or "Forest Slime" => "Slime Gel",
        "Slime Plimt" or "Sproutling" or "Grasshopper" or "Beelet" => "Meadow Leaf",
        "Wood Sprite" or "Leafy" or "Entling Shaman" => "Forest Herb",
        "Treantling" or "Ancient Treant" or "Elderwood Guardian" => "Oak Wood",
        "Mushcap" or "Sporeling" or "Poison Cap" => "Mushroom Cap",
        "Scarecrow" or "Goblin Scout" or "Goblin Brute" or "Rambling Knight" => "Goblin Scrap",
        "Wisp Spirit" or "Shadow Bat" => "Crystal Shard",
        _ => "Wolf Fang", // rats, dogs, boars, wolves, rabbits, bats
    };

    public const float MaterialDropChance = 0.35f;

    /// <summary>Materials found in a chapter's treasure chests.</summary>
    public static string[] ChapterMaterials(int chapter) =>
        chapter == 1 ? new[] { "Meadow Leaf", "Oak Wood" } : new[] { "Forest Herb", "Oak Wood" };

    /// <param name="Max">Most you can carry.</param>
    public record ItemInfo(string Id, string Name, string Desc, int Price, int Max, Color Color);

    public static readonly ItemInfo[] Items =
    {
        new("health_potion", "Health Potion", "Restores half your HP in a stage", 40, 9, new Color(0.95f, 0.35f, 0.40f)),
        new("energy_tonic", "Energy Tonic", "Refills your Rumble energy", 30, 9, new Color(0.40f, 0.70f, 1.00f)),
        new("skill_box", "Skill Box", "Opens at once: a skill of your lineage or mastery", 1500, 99, new Color(1.00f, 0.80f, 0.30f)),
    };

    public static ItemInfo Item(string id) => Items.First(i => i.Id == id);

    // --- Character level (separate from training) -----------------------------

    public const int MaxLevel = 200;

    /// <summary>XP from <paramref name="level"/> to the next level.</summary>
    public static int XpToNextLevel(int level) => (int)Math.Round(30 + 15 * Math.Pow(level, 1.2));

    /// <summary>Kill XP: tougher (higher-level) species give more; chapter bosses five times as much.</summary>
    public static int KillXp(SpeciesInfo species) => (8 + 4 * species.Level) * (IsBoss(species.Name) ? 5 : 1);

    /// <summary>Character levels that each unlock one more equipped skill slot.</summary>
    public static readonly int[] SkillSlotLevels = { 1, 25, 60, 100, 150, 200 };

    public static int SkillSlotsAt(int level) => SkillSlotLevels.Count(l => level >= l);

    public record RankInfo(int MinLevel, string Name);

    public static readonly RankInfo[] Ranks =
    {
        new(1, "Novice"), new(21, "Apprentice"), new(41, "Adept"), new(61, "Veteran"), new(81, "Elite"),
        new(101, "Master"), new(121, "Grandmaster"), new(141, "Champion"), new(161, "Legendary"), new(181, "Ascended"),
    };

    public static string RankAt(int level) => Ranks.Last(r => level >= r.MinLevel).Name;

    // --- Rumble energy and skill mastery --------------------------------------

    public const float BaseMaxEnergy = 100f;
    public const float EnergyPerWellLevel = 10f;
    public const float EnergyRegenPerSec = 10f;

    public const int MaxMasteryRank = 20;
    /// <summary>Mastery XP from rank r to r + 1.</summary>
    public static int MasteryXpToNext(int rank) => 20 * rank;
    /// <summary>Mastery XP for each skill use that hits a non-trivial enemy.</summary>
    public const int MasteryPerUse = 3;
    /// <summary>Mastery XP when a skill box rolls a skill you already have.</summary>
    public const int MasteryFromDuplicateBox = 60;
    /// <summary>Enemies this many levels below the player no longer train mastery.</summary>
    public const int TrivialLevelGap = 10;
    public static int PetXpNeeded(int level) => 20 + 10 * (level - 1);

    public static int UpgradeCost(string id, int currentLevel)
    {
        var upgrade = Upgrade(id);
        return (int)Math.Round(upgrade.BaseCost * Math.Pow(upgrade.Growth, currentLevel));
    }
}
