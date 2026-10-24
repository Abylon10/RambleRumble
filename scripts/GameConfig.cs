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

    // Which zone each species lives in is set in Zones.cs.
    public static readonly SpeciesInfo[] Species =
    {
        new("Slime", "Common", 45f, 5f, 70f, 16f, 10f, 2, 5, new Color(0.45f, 0.85f, 0.45f), 1),
        new("Shroom", "Common", 55f, 6f, 60f, 17f, 12f, 3, 6, new Color(0.85f, 0.40f, 0.35f), 5),
        new("Wolf", "Uncommon", 80f, 8f, 115f, 18f, 18f, 5, 9, new Color(0.60f, 0.58f, 0.62f), 8),
        new("Aqua Slime", "Uncommon", 90f, 7f, 80f, 18f, 20f, 6, 10, new Color(0.35f, 0.65f, 0.95f), 10),
        new("Goblin", "Rare", 130f, 12f, 90f, 17f, 30f, 10, 18, new Color(0.55f, 0.65f, 0.25f), 15),
        new("Boar", "Rare", 170f, 15f, 140f, 21f, 40f, 14, 24, new Color(0.55f, 0.38f, 0.25f), 20),
        new("Crystal Bat", "Epic", 260f, 22f, 150f, 17f, 60f, 30, 50, new Color(0.55f, 0.40f, 0.95f), 35),
        new("Fire Imp", "Legendary", 480f, 35f, 120f, 19f, 100f, 70, 110, new Color(0.95f, 0.35f, 0.15f), 50),
        new("Stone Golem", "Mythic", 1800f, 60f, 55f, 34f, 180f, 300, 500, new Color(0.45f, 0.55f, 0.55f), 70),
        // Chapter bosses.
        new("Slime King", "Rare", 520f, 10f, 70f, 32f, 26f, 40, 60, new Color(0.55f, 0.90f, 0.40f), 10),
        new("Crimson Fang", "Epic", 1150f, 16f, 150f, 30f, 55f, 120, 180, new Color(0.62f, 0.16f, 0.16f), 18),
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
        "Slime" or "Aqua Slime" or "Slime King" => "Slime Gel",
        "Wolf" or "Boar" or "Crimson Fang" => "Wolf Fang",
        "Shroom" => "Mushroom Cap",
        "Goblin" => "Goblin Scrap",
        _ => "Crystal Shard",
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

    /// <summary>Kill XP: tougher (higher-level) species give more; bosses five times as much.</summary>
    public static int KillXp(SpeciesInfo species) => (8 + 4 * species.Level) * (species.Rarity == "Mythic" ? 5 : 1);

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
