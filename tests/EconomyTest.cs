using System.Linq;

namespace RambleRumble.Tests;

/// <summary>
/// Phase 2 economy checks: fence, income, offline cap, treats, merging,
/// Star Shards, Pet Lab gating, release and upgrades.
/// Run: godot --headless --path . res://tests/EconomyTest.tscn -- --test
/// </summary>
public partial class EconomyTest : TestScene
{
    public override void _Ready()
    {
        Run();
        Finish();
    }

    private static int AddResting(string species, string size = "Normal")
    {
        var pet = Data.AddPet(species, size);
        Data.SetFenced(pet.Id, false);
        return pet.Id;
    }

    private void Run()
    {
        Data.ResetProgress();

        // Fence slots and the one-role rule.
        var ids = Enumerable.Range(0, 4).Select(_ => AddResting("Green Slime")).ToList();
        for (int i = 0; i < 3; i++)
            Data.SetFenced(ids[i], true);
        Check(!Data.SetFenced(ids[3], true), "4th pet can't enter a 3-slot fence");
        Check(Data.FencedPets().Count == 3, "3 pets fenced");

        // Income: 3 Normal Common pets at Lv1/1★ = 3 gold/min.
        Check(System.Math.Abs(Data.IncomePerMin() - 3f) < 0.001f, $"income is 3.0/min (got {Data.IncomePerMin():0.00})");
        Data.Gold = 0;
        Data.Tick(60f);
        Check(Data.Gold == 3, $"one minute of ticks pays 3 gold (got {Data.Gold})");

        // Offline cap: 24h away pays only 8h.
        Data.Gold = 0;
        int earned = Data.ApplyOffline(24 * 3600.0);
        Check(earned == 3 * 60 * 8, $"offline pays at most 8h (got {earned})");
        Check(Data.ApplyOffline(-500.0) == 0, "negative offline time pays nothing");

        // Treats and the level cap (1★ caps at Lv10).
        Data.Gold = 100_000;
        int fed = Data.FeedTreats(ids[3], 1000);
        var pet = Data.FindPet(ids[3])!;
        Check(pet.Level == 10, $"treats level a 1★ pet to its cap of 10 (got {pet.Level})");
        Check(fed < 1000, $"feeding stops at the cap (fed {fed})");

        // Merge 1★ -> 2★ needs 1 copy; result takes the largest size.
        Data.ResetProgress();
        Data.Gold = 1_000_000;
        int baseId = AddResting("Green Wolf");
        int giantCopy = AddResting("Green Wolf", "Giant");
        var plan = Data.PlanMerge(baseId);
        Check(plan.Ok && plan.Fodder.SequenceEqual(new[] { giantCopy }), "2★ plan uses the one duplicate");
        Check(Data.Merge(baseId), "merge to 2★ succeeds");
        pet = Data.FindPet(baseId)!;
        Check(pet.Stars == 2 && pet.Size == "Giant", $"result is 2★ and Giant (got {pet.Stars}★ {pet.Size})");
        Check(Data.FindPet(giantCopy) == null, "fodder pet consumed");
        Check(Data.Gold == 1_000_000 - 500, "2★ merge cost 500 gold");

        // 2★ -> 3★ needs 2 copies; shards may cover 1 of them.
        plan = Data.PlanMerge(baseId);
        Check(!plan.Ok, $"3★ blocked with no copies or shards ({plan.Reason})");
        Data.StarShards["Uncommon"] = 5;
        Check(!Data.PlanMerge(baseId).Ok, "shards alone can't cover more than half the copies");
        AddResting("Green Wolf");
        plan = Data.PlanMerge(baseId);
        Check(plan.Ok && plan.Shards == 1 && plan.Fodder.Count == 1, "1 copy + 1 shard makes 3★");
        Data.Merge(baseId);
        Check(Data.StarShards["Uncommon"] == 4, "one shard spent");

        // Pet Lab gates 4★.
        for (int i = 0; i < 3; i++)
            AddResting("Green Wolf");
        plan = Data.PlanMerge(baseId);
        Check(!plan.Ok && plan.Reason.Contains("Pet Lab"), "4★ needs a Pet Lab upgrade");
        Check(Data.BuyUpgrade("pet_lab"), "buy Pet Lab level 1");
        plan = Data.PlanMerge(baseId);
        Check(plan.Ok && plan.Cost == 9000, $"4★ allowed with 10% discount (cost {plan.Cost})");
        Check(Data.Merge(baseId) && Data.FindPet(baseId)!.Stars == 4, "merge to 4★");

        // Release gives gold and a shard.
        int spare = AddResting("Goblin Brute");
        int goldBefore = Data.Gold;
        Check(Data.ReleasePet(spare), "release succeeds");
        Check(Data.Gold == goldBefore + 40 && Data.StarShards["Rare"] == 1, "release pays 40 gold + 1 Rare shard");

        // Upgrades: costs grow and effects apply.
        Data.ResetProgress();
        Data.Gold = 1_000_000;
        Check(Data.NextUpgradeCost("fence_slots") == 200, "first fence slot costs 200");
        Data.BuyUpgrade("fence_slots");
        Check(Data.FenceSlots == 4 && Data.NextUpgradeCost("fence_slots") == 340, "fence slots 4, next costs 340");
        Check(Data.NextUpgradeCost("energy_well") == 800, "first Energy Well level costs 800");
        Data.BuyUpgrade("energy_well");
        Check(Data.MaxEnergy == 110f, $"Energy Well adds 10 max energy (got {Data.MaxEnergy})");
        for (int i = 0; i < 4; i++)
            Data.BuyUpgrade("energy_well");
        Check(Data.UpgradeMaxed("energy_well") && !Data.BuyUpgrade("energy_well"), "can't buy past max");
        Data.BuyUpgrade("training_ground");
        int xpBefore = Data.TrainingXp;
        Data.Train(4f);
        Check(Data.TrainingXp == xpBefore + 5, $"Training Ground turns 4 base XP into 5 (got +{Data.TrainingXp - xpBefore})");
        Data.Gold = 0;
        Check(!Data.BuyUpgrade("fence_slots"), "can't buy without gold");

        // New drops auto-fence while slots are free.
        Data.ResetProgress();
        Data.Pity["Rare"] = 24;
        Data.RecordKill("Goblin Brute");
        Check(Data.Pets.Count == 1 && Data.Pets[0].Role == PetRole.Fence, "new pet goes into a free fence slot");

        // Save/load keeps the new fields.
        Data.Upgrades["fence_slots"] = 2;
        Data.StarShards["Rare"] = 3;
        Data.SaveGame();
        Data.Upgrades.Clear();
        Data.StarShards.Clear();
        Data.LoadGame();
        Check(Data.UpgradeLevel("fence_slots") == 2 && Data.StarShards["Rare"] == 3, "upgrades and shards survive save/load");
    }
}
