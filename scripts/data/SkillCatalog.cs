using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble;

/// <summary>
/// How a skill behaves when cast. The compendium's 200 skills reuse 20
/// archetypes across 10 lineages, so each archetype is coded once in
/// <see cref="SkillExecutor"/> and skills are just data.
/// </summary>
public enum SkillArchetype
{
    /// <summary>Not built yet: listed in the catalog but can't drop or be cast.</summary>
    Planned,
    Strike,
    Projectile,
    Area,
    Dash,
    Surge,
    Guard,
    Step,
}

/// <summary>Extra effect layered on an archetype.</summary>
public enum SkillEffect
{
    None,
    Burn,
    Stun,
    Curve,
}

/// <param name="Code">Compendium id (SK001–SK200); starters use ST01–ST04.</param>
/// <param name="Damage">Multiplier on the player's attack power (or the buff strength for Surge/Guard).</param>
/// <param name="Cost">Rumble energy per cast.</param>
public record SkillInfo(string Id, string Code, string Name, string Short, string Desc, string Lineage,
    SkillArchetype Archetype, int UnlockLevel, float Cooldown, float Damage, int Cost, int Cp, Color Color,
    SkillEffect Effect = SkillEffect.None)
{
    public bool Implemented => Archetype != SkillArchetype.Planned;
    public bool IsStarter => Code.StartsWith("ST");
}

/// <summary>Starter skills plus the Ember lineage (the first lineage built; others follow as data).</summary>
public static class SkillCatalog
{
    private static readonly Color Ember = new(1.00f, 0.45f, 0.15f);

    public static readonly SkillInfo[] All =
    {
        // Starters: one per original lineage. Choosing one sets the player's primary lineage.
        new("flame_fist", "ST01", "Flame Fist", "Flame", "Burning melee strike", "ember",
            SkillArchetype.Strike, 1, 1.5f, 1.6f, 15, 40, Ember, SkillEffect.Burn),
        new("wind_slash", "ST02", "Wind Slash", "Wind", "Fast ranged slash", "gale",
            SkillArchetype.Projectile, 1, 1.2f, 1.2f, 12, 40, new Color(0.45f, 0.90f, 1.00f)),
        new("stone_breaker", "ST03", "Stone Breaker", "Stone", "Heavy stun attack", "stone",
            SkillArchetype.Area, 1, 4.0f, 2.0f, 25, 40, new Color(0.70f, 0.52f, 0.32f), SkillEffect.Stun),
        new("thunder_dash", "ST04", "Thunder Dash", "Dash", "Mobility and burst damage", "thunder",
            SkillArchetype.Dash, 1, 3.0f, 1.5f, 20, 40, new Color(1.00f, 0.90f, 0.25f)),

        // Ember lineage, SK001–SK020. Entries 1–5 are built for the first milestone.
        new("ember_spark", "SK001", "Ember Spark", "Spark", "Light strike", "ember", SkillArchetype.Strike, 5, 0.9f, 1.3f, 8, 30, Ember, SkillEffect.Burn),
        new("ember_arc", "SK002", "Ember Arc", "Arc", "Curved projectile", "ember", SkillArchetype.Projectile, 12, 1.6f, 1.5f, 14, 45, Ember, SkillEffect.Curve),
        new("ember_surge", "SK003", "Ember Surge", "Surge", "Short damage boost", "ember", SkillArchetype.Surge, 20, 12f, 0.35f, 25, 55, Ember),
        new("ember_guard", "SK004", "Ember Guard", "Guard", "Brief damage reduction", "ember", SkillArchetype.Guard, 28, 10f, 0.5f, 20, 60, Ember),
        new("ember_step", "SK005", "Ember Step", "Step", "Quick reposition", "ember", SkillArchetype.Step, 35, 4f, 0f, 15, 60, Ember),
        Planned("ember_pulse", "SK006", "Ember Pulse", "Radial hit", 45),
        Planned("ember_lance", "SK007", "Ember Lance", "Piercing shot", 55),
        Planned("ember_ring", "SK008", "Ember Ring", "Close-range shockwave", 65),
        Planned("ember_snare", "SK009", "Ember Snare", "Short root", 75),
        Planned("ember_ward", "SK010", "Ember Ward", "Temporary shield", 85),
        Planned("ember_burst", "SK011", "Ember Burst", "Delayed explosion", 95),
        Planned("ember_mark", "SK012", "Ember Mark", "Damage-over-time mark", 105),
        Planned("ember_rush", "SK013", "Ember Rush", "Gap closer", 115),
        Planned("ember_beacon", "SK014", "Ember Beacon", "Team buff zone", 125),
        Planned("ember_crest", "SK015", "Ember Crest", "Counterattack", 135),
        Planned("ember_chain", "SK016", "Ember Chain", "Jumping damage", 145),
        Planned("ember_nova", "SK017", "Ember Nova", "Large area strike", 155),
        Planned("ember_domain", "SK018", "Ember Domain", "Persistent hazard field", 165),
        Planned("ember_judgment", "SK019", "Ember Judgment", "High-cost finisher", 180),
        Planned("ember_transcendence", "SK020", "Ember Transcendence", "Ultimate form burst (Ascension only)", 200),
    };

    public static readonly string[] StarterSkills = { "flame_fist", "wind_slash", "stone_breaker", "thunder_dash" };

    private static SkillInfo Planned(string id, string code, string name, string desc, int level) =>
        new(id, code, name, name.Split(' ')[1], desc, "ember", SkillArchetype.Planned, level, 1f, 1f, 0, 0, Ember);

    private static readonly Dictionary<string, SkillInfo> ById = All.ToDictionary(s => s.Id);

    public static SkillInfo Get(string id) => ById[id];
    public static bool Exists(string id) => ById.ContainsKey(id);

    /// <summary>Non-starter skills of a lineage, in unlock order.</summary>
    public static IEnumerable<SkillInfo> LineageSkills(string lineage) =>
        All.Where(s => s.Lineage == lineage && !s.IsStarter).OrderBy(s => s.UnlockLevel);
}
