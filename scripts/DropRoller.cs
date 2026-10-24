using System.Collections.Generic;
using Godot;

namespace RambleRumble;

/// <summary>What a single kill rewarded.</summary>
public sealed class KillDrop
{
    public int Gold { get; init; }
    /// <summary>Species and size of the dropped pet, or null if none dropped.</summary>
    public (string Species, string Rarity, string Size)? Pet { get; init; }
    public bool PityHit { get; init; }
    public bool SkillBox { get; init; }
    /// <summary>Filled in by PlayerData when the skill box is opened.</summary>
    public string SkillLearned { get; set; } = "";
    /// <summary>False when the box rolled a skill already owned (it gave mastery XP instead).</summary>
    public bool SkillIsNew { get; set; }
    /// <summary>Character XP for the kill.</summary>
    public int Xp { get; set; }
    /// <summary>Material dropped ("" = none), filled in by PlayerData.</summary>
    public string Material { get; set; } = "";
    public int MaterialCount { get; set; }
    /// <summary>Filled in by PlayerData once the pet is added.</summary>
    public Pet? AddedPet { get; set; }
}

/// <summary>
/// Rolls monster-kill rewards: gold, a pet (with size) and a skill box.
/// Pure logic with an injected RNG so it can be simulated in tests.
/// </summary>
public static class DropRoller
{
    /// <param name="pity">Rarity -> kills since that rarity's last pet drop; updated in place.</param>
    public static KillDrop Roll(string speciesId, Dictionary<string, int> pity, RandomNumberGenerator rng)
    {
        var species = GameConfig.SpeciesOf(speciesId);
        var rarity = GameConfig.Rarity(species.Rarity);

        int gold = rng.RandiRange(species.GoldMin, species.GoldMax);
        bool skillBox = rng.Randf() < rarity.BoxChance;

        int count = pity.GetValueOrDefault(rarity.Name) + 1;
        bool gotPet = rng.Randf() < rarity.PetChance;
        bool pityHit = false;
        if (!gotPet && rarity.Pity > 0 && count >= rarity.Pity)
        {
            gotPet = true;
            pityHit = true;
        }
        pity[rarity.Name] = gotPet ? 0 : count;

        return new KillDrop
        {
            Gold = gold,
            Pet = gotPet ? (speciesId, rarity.Name, RollSize(rng)) : null,
            PityHit = pityHit,
            SkillBox = skillBox,
        };
    }

    public static string RollSize(RandomNumberGenerator rng)
    {
        float roll = rng.Randf();
        float acc = 0f;
        foreach (var size in GameConfig.Sizes)
        {
            acc += size.Chance;
            if (roll < acc)
                return size.Name;
        }
        return "Normal";
    }
}
