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

    public record SpeciesInfo(string Name, string Rarity, float Hp, float Damage, float Speed, float Radius,
        float PetPower, int GoldMin, int GoldMax, Color Color);

    /// <param name="Damage">Multiplier on the player's attack power.</param>
    public record SkillInfo(string Id, string Name, string Short, string Desc, float Cooldown, float Damage, int Cp, Color Color);

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
        new("skill_slots", "Skill Slots", "+1 equipped skill", 2, 2000, 5.0f),
        new("training_ground", "Training Ground", "+25% training XP", 10, 300, 1.8f),
        new("pet_lab", "Pet Lab", "Unlocks the next star rank, -10% merge cost", 2, 5000, 5.0f),
    };

    // Which zone each species lives in is set in Zones.cs.
    public static readonly SpeciesInfo[] Species =
    {
        new("Slime", "Common", 45f, 5f, 70f, 16f, 10f, 2, 5, new Color(0.45f, 0.85f, 0.45f)),
        new("Shroom", "Common", 55f, 6f, 60f, 17f, 12f, 3, 6, new Color(0.85f, 0.40f, 0.35f)),
        new("Wolf", "Uncommon", 80f, 8f, 115f, 18f, 18f, 5, 9, new Color(0.60f, 0.58f, 0.62f)),
        new("Aqua Slime", "Uncommon", 90f, 7f, 80f, 18f, 20f, 6, 10, new Color(0.35f, 0.65f, 0.95f)),
        new("Goblin", "Rare", 130f, 12f, 90f, 17f, 30f, 10, 18, new Color(0.55f, 0.65f, 0.25f)),
        new("Boar", "Rare", 170f, 15f, 140f, 21f, 40f, 14, 24, new Color(0.55f, 0.38f, 0.25f)),
        new("Crystal Bat", "Epic", 260f, 22f, 150f, 17f, 60f, 30, 50, new Color(0.55f, 0.40f, 0.95f)),
        new("Fire Imp", "Legendary", 480f, 35f, 120f, 19f, 100f, 70, 110, new Color(0.95f, 0.35f, 0.15f)),
        new("Stone Golem", "Mythic", 1800f, 60f, 55f, 34f, 180f, 300, 500, new Color(0.45f, 0.55f, 0.55f)),
    };

    public static readonly SkillInfo[] Skills =
    {
        new("flame_fist", "Flame Fist", "Flame", "Burning melee strike", 1.5f, 1.6f, 40, new Color(1.00f, 0.45f, 0.15f)),
        new("wind_slash", "Wind Slash", "Wind", "Fast ranged slash", 1.2f, 1.2f, 40, new Color(0.45f, 0.90f, 1.00f)),
        new("stone_breaker", "Stone Breaker", "Stone", "Heavy stun attack", 4.0f, 2.0f, 40, new Color(0.70f, 0.52f, 0.32f)),
        new("thunder_dash", "Thunder Dash", "Dash", "Mobility and burst damage", 3.0f, 1.5f, 40, new Color(1.00f, 0.90f, 0.25f)),
    };
    public static readonly string[] StarterSkills = { "flame_fist", "wind_slash", "stone_breaker", "thunder_dash" };
    public const int BaseSkillSlots = 2;
    public const int SkillBoxDuplicateGold = 50;

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
    public static SkillInfo Skill(string id) => Skills.First(s => s.Id == id);
    public static UpgradeInfo Upgrade(string id) => Upgrades.First(u => u.Id == id);
    public static StarInfo Star(int stars) => StarTable[stars - 1];
    public static SizeInfo Size(string name) => Sizes.FirstOrDefault(s => s.Name == name) ?? Sizes[1];

    public static int TrainingXpNeeded(int level) => 10 + 5 * level;
    public static int PetXpNeeded(int level) => 20 + 10 * (level - 1);

    public static int UpgradeCost(string id, int currentLevel)
    {
        var upgrade = Upgrade(id);
        return (int)Math.Round(upgrade.BaseCost * Math.Pow(upgrade.Growth, currentLevel));
    }
}
