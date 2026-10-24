using System.Linq;
using Godot;

namespace RambleRumble;

/// <param name="Classes">The five class names, Tier I to Tier V (Ascension).</param>
/// <param name="StarterSkill">Starter that begins this lineage, or "" if it opens later through quests.</param>
public record LineageInfo(string Id, string Name, string Focus, string[] Classes, string StarterSkill, Color Color);

/// <summary>The ten class lineages (5 tiers each, 50 classes) from the skills and classes compendium.</summary>
public static class Lineages
{
    /// <summary>Minimum character level for class tiers I–V.</summary>
    public static readonly int[] TierLevels = { 1, 40, 80, 140, 200 };

    public static readonly LineageInfo[] All =
    {
        new("ember", "Ember", "Burn and explosive melee",
            new[] { "Flame Initiate", "Cinder Brawler", "Pyre Knight", "Inferno Warden", "Solar Ascendant" }, "flame_fist", new Color(1.00f, 0.45f, 0.15f)),
        new("gale", "Gale", "Mobility and slicing projectiles",
            new[] { "Breeze Scout", "Wind Dancer", "Tempest Duelist", "Skybreaker", "Storm Ascendant" }, "wind_slash", new Color(0.45f, 0.90f, 1.00f)),
        new("stone", "Stone", "Defense, stagger and terrain",
            new[] { "Pebble Guard", "Granite Fighter", "Quake Sentinel", "Mountain Titan", "Earth Ascendant" }, "stone_breaker", new Color(0.70f, 0.52f, 0.32f)),
        new("thunder", "Thunder", "Dashes and chain lightning",
            new[] { "Spark Runner", "Volt Striker", "Lightning Vanguard", "Storm Herald", "Thunder Ascendant" }, "thunder_dash", new Color(1.00f, 0.90f, 0.25f)),
        new("tide", "Tide", "Healing, displacement and flow",
            new[] { "Stream Adept", "Wavecaller", "Tidal Monk", "Abyss Navigator", "Ocean Ascendant" }, "", new Color(0.30f, 0.60f, 1.00f)),
        new("frost", "Frost", "Slows, shields and shatter",
            new[] { "Snow Apprentice", "Ice Weaver", "Glacier Ranger", "Winter Sovereign", "Frost Ascendant" }, "", new Color(0.70f, 0.90f, 1.00f)),
        new("nature", "Nature", "Vines, summons and regeneration",
            new[] { "Sprout Keeper", "Thorn Hunter", "Grove Warden", "Wildheart Sage", "Verdant Ascendant" }, "", new Color(0.45f, 0.85f, 0.35f)),
        new("shadow", "Shadow", "Stealth, marks and ambush",
            new[] { "Dusk Sneak", "Nightblade", "Umbral Stalker", "Eclipse Reaper", "Void Ascendant" }, "", new Color(0.55f, 0.40f, 0.75f)),
        new("light", "Light", "Barriers, purification and support",
            new[] { "Dawn Acolyte", "Radiant Guardian", "Sunblade", "Aurora Champion", "Celestial Ascendant" }, "", new Color(1.00f, 0.95f, 0.65f)),
        new("echo", "Echo", "Monster bonds and resonance",
            new[] { "Resonance Student", "Pulse Tamer", "Echo Binder", "Rumble Conductor", "Worldheart Ascendant" }, "", new Color(0.95f, 0.55f, 0.85f)),
    };

    public static LineageInfo Get(string id) => All.First(l => l.Id == id);

    public static string ClassName(string lineage, int tier) => Get(lineage).Classes[tier - 1];
}
