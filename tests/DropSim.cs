using System;
using System.Collections.Generic;
using Godot;

namespace RambleRumble.Tests;

/// <summary>
/// Simulates many kills to check drop rates, size odds and pity.
/// Run: godot --headless --path . res://tests/DropSim.tscn -- --test
/// </summary>
public partial class DropSim : TestScene
{
    private const int Kills = 200_000;

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator { Seed = 12345 };
        foreach (var species in GameConfig.Species)
        {
            var rarity = GameConfig.Rarity(species.Rarity);
            var pity = new Dictionary<string, int>();
            int pets = 0, boxes = 0, drought = 0, longestDrought = 0;
            for (int i = 0; i < Kills; i++)
            {
                var drop = DropRoller.Roll(species.Name, pity, rng);
                if (drop.SkillBox)
                    boxes++;
                if (drop.Pet != null)
                {
                    pets++;
                    drought = 0;
                }
                else
                {
                    longestDrought = Math.Max(longestDrought, ++drought);
                }
            }
            float rate = (float)pets / Kills;
            GD.Print($"{species.Name,-7} {rarity.Name,-9} pet {rate:0.0000} (base {rarity.PetChance:0.000})  " +
                $"box {(float)boxes / Kills:0.0000}  longest drought {longestDrought}");
            Check(rate >= rarity.PetChance * 0.95f, $"{species.Name} pet rate at or above base chance");
            if (rarity.Pity > 0)
                Check(longestDrought < rarity.Pity, $"{species.Name} never exceeds pity limit {rarity.Pity}");
        }

        var counts = new Dictionary<string, int>();
        for (int i = 0; i < Kills; i++)
        {
            string size = DropRoller.RollSize(rng);
            counts[size] = counts.GetValueOrDefault(size) + 1;
        }
        foreach (var size in GameConfig.Sizes)
        {
            float rate = (float)counts.GetValueOrDefault(size.Name) / Kills;
            Check(Math.Abs(rate - size.Chance) <= 0.01f, $"size {size.Name} {rate:0.0000} (target {size.Chance:0.000})");
        }
        Finish();
    }
}
