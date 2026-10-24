using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace RambleRumble;

/// <summary>The outcome of planning a star-up merge, before anything changes.</summary>
public sealed class MergePlan
{
    public bool Ok { get; set; }
    public string Reason { get; set; } = "";
    public int NextStars { get; set; }
    /// <summary>Ids of the duplicates that would be consumed.</summary>
    public List<int> Fodder { get; set; } = new();
    public int Shards { get; set; }
    public int Cost { get; set; }
    public string ResultSize { get; set; } = "";
    public int CopiesNeeded { get; set; }
    public int CopiesOwned { get; set; }
    public int ShardLimit { get; set; }
    public int ShardsOwned { get; set; }
}

/// <summary>
/// The player's progress, saved locally. Registered as the autoload
/// "PlayerData"; reach it from anywhere with <c>PlayerData.Instance</c>.
/// When online raids arrive (Phase 5) the backend becomes the source of truth,
/// so every change goes through here.
/// </summary>
public partial class PlayerData : Node
{
    public static PlayerData Instance { get; private set; } = null!;

    /// <summary>Raised after any change the UI should show.</summary>
    public event Action? Changed;
    /// <summary>Raised for a message the HUD should toast.</summary>
    public event Action<string, Color>? Notify;

    private const int SaveVersion = 2;
    private const float AutosaveInterval = 15f;

    /// <summary>"male" or "female"; picks the character art.</summary>
    public string Gender { get; private set; } = "male";
    public int Gold { get; set; }
    public int TrainingLevel { get; private set; } = 1;
    public int TrainingXp { get; private set; }
    public List<string> OwnedSkills { get; } = new();
    public List<string> EquippedSkills { get; } = new();
    public List<Pet> Pets { get; } = new();
    public Dictionary<string, int> Pity { get; } = new();
    public int Kills { get; private set; }
    /// <summary>Upgrade id -> level bought.</summary>
    public Dictionary<string, int> Upgrades { get; } = new();
    /// <summary>Rarity -> Star Shards owned.</summary>
    public Dictionary<string, int> StarShards { get; } = new();
    /// <summary>Bumped whenever the pet list or a pet changes, so UI can skip rebuilding.</summary>
    public int PetsVersion { get; private set; }
    /// <summary>Gold earned while the app was closed, reported once by the HUD.</summary>
    public int OfflineGold { get; set; }

    public RandomNumberGenerator Rng { get; } = new();

    // Tests pass "-- --test" so they never touch the real save.
    private readonly string _savePath =
        OS.GetCmdlineUserArgs().Contains("--test") ? "user://test_save.json" : "user://save.json";

    private int _nextPetId = 1;
    private float _trainFrac;
    private float _incomeFrac;
    private float _fenceXpFrac;
    private float _autosaveLeft = AutosaveInterval;

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        Rng.Randomize();
        LoadGame();
    }

    public override void _Process(double delta)
    {
        Tick((float)delta);
        _autosaveLeft -= (float)delta;
        if (_autosaveLeft <= 0f)
        {
            _autosaveLeft = AutosaveInterval;
            SaveGame();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationPaused || what == NotificationWMCloseRequest)
            SaveGame();
    }

    public void SetGender(string gender)
    {
        if (!CharacterArt.Genders.Contains(gender) || gender == Gender)
            return;
        Gender = gender;
        Commit();
    }

    // --- Skills ---------------------------------------------------------------

    public bool HasStarter => OwnedSkills.Count > 0;

    public void ChooseStarter(string skillId)
    {
        if (HasStarter || !GameConfig.StarterSkills.Contains(skillId))
            return;
        OwnedSkills.Add(skillId);
        EquippedSkills.Add(skillId);
        Commit();
    }

    public int SkillSlots => GameConfig.BaseSkillSlots + UpgradeLevel("skill_slots");

    public void ToggleEquip(string skillId)
    {
        if (EquippedSkills.Contains(skillId))
        {
            if (EquippedSkills.Count > 1)
                EquippedSkills.Remove(skillId);
        }
        else if (OwnedSkills.Contains(skillId) && EquippedSkills.Count < SkillSlots)
        {
            EquippedSkills.Add(skillId);
        }
        Commit();
    }

    /// <summary>Returns the learned skill id, or "" if the box was a duplicate converted to gold.</summary>
    private string OpenSkillBox()
    {
        var unowned = GameConfig.Skills.Select(s => s.Id).Where(id => !OwnedSkills.Contains(id)).ToList();
        if (unowned.Count == 0)
        {
            Gold += GameConfig.SkillBoxDuplicateGold;
            return "";
        }
        string skillId = unowned[Rng.RandiRange(0, unowned.Count - 1)];
        OwnedSkills.Add(skillId);
        if (EquippedSkills.Count < SkillSlots)
            EquippedSkills.Add(skillId);
        return skillId;
    }

    // --- Training and stats ---------------------------------------------------

    /// <summary>Adds training XP after the Training Ground bonus; fractions carry over.</summary>
    public void Train(float baseXp)
    {
        _trainFrac += baseXp * TrainingMultiplier;
        int whole = (int)_trainFrac;
        if (whole > 0)
        {
            _trainFrac -= whole;
            AddTrainingXp(whole);
        }
    }

    public float TrainingMultiplier => 1f + 0.25f * UpgradeLevel("training_ground");

    public void AddTrainingXp(int amount)
    {
        TrainingXp += amount;
        bool leveled = false;
        while (TrainingXp >= GameConfig.TrainingXpNeeded(TrainingLevel))
        {
            TrainingXp -= GameConfig.TrainingXpNeeded(TrainingLevel);
            TrainingLevel++;
            leveled = true;
        }
        if (leveled)
            Notify?.Invoke($"Training level {TrainingLevel}!", new Color(0.5f, 0.9f, 1f));
        // Disk writes only on level-up; idle training ticks every second.
        Commit(save: leveled);
    }

    public float AttackPower => 10f * (1f + 0.12f * (TrainingLevel - 1));
    public float MaxHp => 100f + 15f * (TrainingLevel - 1);

    public float PetPower(Pet pet)
    {
        var species = GameConfig.SpeciesOf(pet.Species);
        float sizeMult = GameConfig.Size(pet.Size).Mult;
        // Size boosts HP but not damage, so it counts half toward power.
        return species.PetPower * LevelMult(pet) * GameConfig.Star(pet.Stars).Mult * (1f + (sizeMult - 1f) * 0.5f);
    }

    /// <summary>Offense CP from the best loadout the player owns, so unequipping can't lower it.</summary>
    public int CombatPower()
    {
        float cp = 100f + 25f * (TrainingLevel - 1);
        cp += OwnedSkills.Select(id => GameConfig.Skill(id).Cp).OrderByDescending(c => c).Take(SkillSlots).Sum();
        cp += (Pets.Count > 0 ? Pets.Max(PetPower) : 0f) * 2f;
        return (int)Math.Round(cp);
    }

    // --- Upgrades -------------------------------------------------------------

    public int UpgradeLevel(string upgradeId) => Upgrades.GetValueOrDefault(upgradeId);
    public bool UpgradeMaxed(string upgradeId) => UpgradeLevel(upgradeId) >= GameConfig.Upgrade(upgradeId).Max;
    public int NextUpgradeCost(string upgradeId) => GameConfig.UpgradeCost(upgradeId, UpgradeLevel(upgradeId));

    public bool BuyUpgrade(string upgradeId)
    {
        if (UpgradeMaxed(upgradeId))
            return false;
        int cost = NextUpgradeCost(upgradeId);
        if (Gold < cost)
            return false;
        Gold -= cost;
        Upgrades[upgradeId] = UpgradeLevel(upgradeId) + 1;
        Notify?.Invoke($"{GameConfig.Upgrade(upgradeId).Name} upgraded to level {Upgrades[upgradeId]}!", new Color(1f, 0.85f, 0.3f));
        Commit();
        return true;
    }

    public int FenceSlots => GameConfig.BaseFenceSlots + UpgradeLevel("fence_slots");
    public int MaxStarsUnlocked => Math.Min(GameConfig.MaxStars, GameConfig.BaseMaxStars + UpgradeLevel("pet_lab"));
    public float MergeDiscount => 0.1f * UpgradeLevel("pet_lab");

    // --- Pets: fence, levels, merging, release --------------------------------

    public Pet? FindPet(int petId) => Pets.FirstOrDefault(p => p.Id == petId);

    public List<Pet> FencedPets() => Pets.Where(p => p.Role == PetRole.Fence).ToList();

    public float PetIncomePerMin(Pet pet) =>
        GameConfig.Rarity(pet.Rarity).Income * LevelMult(pet) * GameConfig.Star(pet.Stars).Mult * GameConfig.Size(pet.Size).Mult;

    public float IncomePerMin() => FencedPets().Sum(PetIncomePerMin);

    public int PetMaxLevel(Pet pet) => GameConfig.Star(pet.Stars).MaxLevel;

    /// <summary>Adds a new 1★ level 1 pet. New pets go into a free fence slot so income starts at once.</summary>
    public Pet AddPet(string species, string size)
    {
        var pet = new Pet
        {
            Id = _nextPetId++,
            Species = species,
            Rarity = GameConfig.SpeciesOf(species).Rarity,
            Size = size,
            Role = FencedPets().Count < FenceSlots ? PetRole.Fence : PetRole.None,
        };
        Pets.Add(pet);
        PetsVersion++;
        return pet;
    }

    /// <summary>Returns false if the fence is full.</summary>
    public bool SetFenced(int petId, bool fenced)
    {
        var pet = FindPet(petId);
        if (pet == null)
            return false;
        if (fenced && pet.Role != PetRole.Fence && FencedPets().Count >= FenceSlots)
            return false;
        pet.Role = fenced ? PetRole.Fence : PetRole.None;
        PetsChanged();
        return true;
    }

    /// <summary>Feeds up to <paramref name="count"/> treats, stopping at the level cap or when gold runs out.</summary>
    public int FeedTreats(int petId, int count)
    {
        var pet = FindPet(petId);
        int fed = 0;
        while (pet != null && fed < count && pet.Level < PetMaxLevel(pet) && Gold >= GameConfig.TreatCost)
        {
            Gold -= GameConfig.TreatCost;
            AddPetXp(pet, GameConfig.TreatXp);
            fed++;
        }
        if (fed > 0)
            PetsChanged();
        return fed;
    }

    /// <summary>
    /// Describes the next star-up for a pet without changing anything. Fodder is
    /// the weakest duplicates of the same species; Star Shards of the pet's
    /// rarity fill in for up to half of the required copies.
    /// </summary>
    public MergePlan PlanMerge(int petId)
    {
        var plan = new MergePlan();
        var pet = FindPet(petId);
        if (pet == null)
        {
            plan.Reason = "Pet not found";
            return plan;
        }
        plan.NextStars = pet.Stars + 1;
        if (plan.NextStars > GameConfig.MaxStars)
        {
            plan.Reason = "Already at max stars";
            return plan;
        }
        if (plan.NextStars > MaxStarsUnlocked)
        {
            plan.Reason = $"Upgrade the Pet Lab to unlock {plan.NextStars}★";
            return plan;
        }

        int need = GameConfig.Star(plan.NextStars).Copies;
        var copies = Pets.Where(p => p.Species == pet.Species && p.Id != petId).OrderBy(PetPower).ToList();
        int usedCopies = Math.Min(need, copies.Count);
        var used = copies.Take(usedCopies).ToList();

        plan.CopiesNeeded = need;
        plan.CopiesOwned = copies.Count;
        plan.ShardLimit = (int)Math.Floor(need * GameConfig.ShardReplaceFraction);
        plan.ShardsOwned = StarShards.GetValueOrDefault(pet.Rarity);
        plan.Fodder = used.Select(p => p.Id).ToList();
        plan.Shards = need - usedCopies;
        plan.Cost = (int)Math.Round(GameConfig.Star(plan.NextStars).Cost * (1f - MergeDiscount));
        // The result keeps the largest size among everything merged.
        plan.ResultSize = used.Append(pet).Select(p => GameConfig.Size(p.Size)).MaxBy(s => s.Mult)!.Name;

        if (plan.Shards > plan.ShardLimit || plan.Shards > plan.ShardsOwned)
        {
            int missing = plan.Shards - Math.Min(plan.ShardLimit, plan.ShardsOwned);
            plan.Reason = $"Need {missing} more {pet.Species} {(missing == 1 ? "copy" : "copies")}";
        }
        else if (Gold < plan.Cost)
        {
            plan.Reason = $"Need {plan.Cost} gold";
        }
        else
        {
            plan.Ok = true;
        }
        return plan;
    }

    public bool Merge(int petId)
    {
        var plan = PlanMerge(petId);
        var pet = FindPet(petId);
        if (!plan.Ok || pet == null)
            return false;
        Gold -= plan.Cost;
        StarShards[pet.Rarity] = StarShards.GetValueOrDefault(pet.Rarity) - plan.Shards;
        Pets.RemoveAll(p => plan.Fodder.Contains(p.Id));
        pet.Stars = plan.NextStars;
        pet.Size = plan.ResultSize;
        Notify?.Invoke($"{pet.Size} {pet.Species} reached {pet.Stars}★!", new Color(1f, 0.85f, 0.3f));
        PetsChanged();
        return true;
    }

    public bool ReleasePet(int petId)
    {
        var pet = FindPet(petId);
        if (pet == null)
            return false;
        Gold += GameConfig.Rarity(pet.Rarity).ReleaseGold;
        StarShards[pet.Rarity] = StarShards.GetValueOrDefault(pet.Rarity) + 1;
        Pets.Remove(pet);
        PetsChanged();
        return true;
    }

    private static float LevelMult(Pet pet) => 1f + GameConfig.PetLevelBonus * (pet.Level - 1);

    /// <summary>Levels stop at the star cap; XP beyond the cap is lost.</summary>
    private void AddPetXp(Pet pet, int amount)
    {
        int cap = PetMaxLevel(pet);
        pet.Xp += amount;
        while (pet.Level < cap && pet.Xp >= GameConfig.PetXpNeeded(pet.Level))
        {
            pet.Xp -= GameConfig.PetXpNeeded(pet.Level);
            pet.Level++;
        }
        if (pet.Level >= cap)
            pet.Xp = 0;
    }

    // --- Income ---------------------------------------------------------------

    /// <summary>Advances fence income and fence XP by <paramref name="seconds"/>. Used every frame and for time spent offline.</summary>
    public void Tick(float seconds)
    {
        var fenced = FencedPets();
        if (fenced.Count == 0)
            return;
        _incomeFrac += IncomePerMin() / 60f * seconds;
        int earned = (int)_incomeFrac;
        _fenceXpFrac += GameConfig.FenceXpPerMin / 60f * seconds;
        int xp = (int)_fenceXpFrac;
        if (earned > 0)
        {
            _incomeFrac -= earned;
            Gold += earned;
        }
        if (xp > 0)
        {
            _fenceXpFrac -= xp;
            foreach (var pet in fenced)
                AddPetXp(pet, xp);
            PetsVersion++;
        }
        if (earned > 0 || xp > 0)
            Changed?.Invoke();
    }

    /// <summary>Applies income for time away (capped) and returns the gold earned.</summary>
    public int ApplyOffline(double seconds)
    {
        double capped = Math.Clamp(seconds, 0.0, GameConfig.OfflineCapHours * 3600.0);
        int before = Gold;
        Tick((float)capped);
        return Gold - before;
    }

    // --- Kills and drops ------------------------------------------------------

    public KillDrop RecordKill(string speciesId)
    {
        Kills++;
        var drop = DropRoller.Roll(speciesId, Pity, Rng);
        Gold += drop.Gold;
        if (drop.Pet is { } petDrop)
            drop.AddedPet = AddPet(petDrop.Species, petDrop.Size);
        if (drop.SkillBox)
            drop.SkillLearned = OpenSkillBox();
        Commit();
        return drop;
    }

    // --- Persistence ----------------------------------------------------------

    private void PetsChanged()
    {
        PetsVersion++;
        Commit();
    }

    private void Commit(bool save = true)
    {
        Changed?.Invoke();
        if (save)
            SaveGame();
    }

    /// <summary>On-disk shape of the save. Snake-case names keep old GDScript saves loadable.</summary>
    private sealed class SaveData
    {
        public int Version { get; set; }
        public double SavedAt { get; set; }
        public string Gender { get; set; } = "male";
        public int Gold { get; set; }
        public int TrainingLevel { get; set; } = 1;
        public int TrainingXp { get; set; }
        public List<string> OwnedSkills { get; set; } = new();
        public List<string> EquippedSkills { get; set; } = new();
        public List<Pet> Pets { get; set; } = new();
        public Dictionary<string, int> Pity { get; set; } = new();
        public int Kills { get; set; }
        public Dictionary<string, int> Upgrades { get; set; } = new();
        public Dictionary<string, int> StarShards { get; set; } = new();
        public int NextPetId { get; set; } = 1;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        // GDScript saves could write whole numbers as 3.0; accept those for ints.
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public void SaveGame()
    {
        var data = new SaveData
        {
            Version = SaveVersion,
            SavedAt = Time.GetUnixTimeFromSystem(),
            Gender = Gender,
            Gold = Gold,
            TrainingLevel = TrainingLevel,
            TrainingXp = TrainingXp,
            OwnedSkills = OwnedSkills,
            EquippedSkills = EquippedSkills,
            Pets = Pets,
            Pity = Pity,
            Kills = Kills,
            Upgrades = Upgrades,
            StarShards = StarShards,
            NextPetId = _nextPetId,
        };
        using var file = FileAccess.Open(_savePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushWarning($"Could not save: {FileAccess.GetOpenError()}");
            return;
        }
        file.StoreString(JsonSerializer.Serialize(data, JsonOptions));
    }

    public void LoadGame()
    {
        if (!FileAccess.FileExists(_savePath))
            return;
        SaveData? data;
        try
        {
            data = JsonSerializer.Deserialize<SaveData>(FileAccess.GetFileAsString(_savePath), JsonOptions);
        }
        catch (JsonException e)
        {
            GD.PushWarning($"Save file unreadable ({e.Message}); starting fresh.");
            return;
        }
        if (data == null)
            return;

        Gender = CharacterArt.Genders.Contains(data.Gender) ? data.Gender : "male";
        Gold = data.Gold;
        TrainingLevel = data.TrainingLevel;
        TrainingXp = data.TrainingXp;
        Kills = data.Kills;
        _nextPetId = data.NextPetId;
        ReplaceAll(OwnedSkills, data.OwnedSkills);
        ReplaceAll(EquippedSkills, data.EquippedSkills);
        ReplaceAll(Pets, data.Pets);
        ReplaceAll(Pity, data.Pity);
        ReplaceAll(Upgrades, data.Upgrades);
        ReplaceAll(StarShards, data.StarShards);
        PetsVersion++;

        // Version 1 saves have no timestamp, so they earn nothing offline.
        // Clock changes can make the gap negative; ApplyOffline clamps it.
        if (data.SavedAt > 0)
            OfflineGold = ApplyOffline(Time.GetUnixTimeFromSystem() - data.SavedAt);
    }

    private static void ReplaceAll<T>(ICollection<T> target, IEnumerable<T> source)
    {
        var items = source.ToList();
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }

    public void ResetProgress()
    {
        Gender = "male";
        Gold = 0;
        TrainingLevel = 1;
        TrainingXp = 0;
        OwnedSkills.Clear();
        EquippedSkills.Clear();
        Pets.Clear();
        Pity.Clear();
        Upgrades.Clear();
        StarShards.Clear();
        Kills = 0;
        OfflineGold = 0;
        _nextPetId = 1;
        _trainFrac = 0f;
        _incomeFrac = 0f;
        _fenceXpFrac = 0f;
        PetsVersion++;
        Commit();
    }
}
