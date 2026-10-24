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
/// The player's progress, saved on the device (the game is fully offline).
/// Registered as the autoload "PlayerData"; reach it from anywhere with
/// <c>PlayerData.Instance</c>. Every change goes through here so the UI and
/// quests stay in sync.
/// </summary>
public partial class PlayerData : Node
{
    public static PlayerData Instance { get; private set; } = null!;

    /// <summary>Raised after any change the UI should show.</summary>
    public event Action? Changed;
    /// <summary>Raised for a message the HUD should toast.</summary>
    public event Action<string, Color>? Notify;

    private const int SaveVersion = 3;
    private const float AutosaveInterval = 15f;

    /// <summary>"male" or "female"; picks the character art.</summary>
    public string Gender { get; private set; } = "male";
    /// <summary>Character level (1–200), from kill and quest XP. Separate from training.</summary>
    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }
    /// <summary>Primary class lineage, set by the starter skill ("" before choosing).</summary>
    public string Lineage { get; private set; } = "";
    public int ClassTier { get; private set; } = 1;
    /// <summary>Skill id -> total mastery XP.</summary>
    public Dictionary<string, int> Mastery { get; } = new();
    /// <summary>Index into <see cref="Quests.All"/> of the current story quest.</summary>
    public int QuestIndex { get; private set; }
    public bool QuestAccepted { get; private set; }
    public int QuestProgress { get; private set; }
    /// <summary>Highest story chapter fully completed (-1 = none, 0 = Prologue).</summary>
    public int CompletedChapter { get; private set; } = -1;
    /// <summary>Stage id -> best stars (1–3). Present means cleared.</summary>
    public Dictionary<string, int> StageStars { get; } = new();
    /// <summary>Stage id -> chests opened there (bit 1 = treasure, bit 2 = puzzle chest).</summary>
    public Dictionary<string, int> StageChests { get; } = new();
    /// <summary>Material name -> count.</summary>
    public Dictionary<string, int> Materials { get; } = new();
    /// <summary>Item id -> count (potions, tonics).</summary>
    public Dictionary<string, int> Items { get; } = new();
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

    /// <summary>Learns a starter skill; its lineage becomes the primary lineage at class Tier I.</summary>
    public void ChooseStarter(string skillId)
    {
        if (HasStarter || !SkillCatalog.StarterSkills.Contains(skillId))
            return;
        OwnedSkills.Add(skillId);
        EquippedSkills.Add(skillId);
        Lineage = SkillCatalog.Get(skillId).Lineage;
        ClassTier = 1;
        Commit();
    }

    /// <summary>Equipped skill slots come from character level (2nd at 25, ... 6th at 200).</summary>
    public int SkillSlots => GameConfig.SkillSlotsAt(Level);

    public string ClassName => Lineage == "" ? "No class" : Lineages.ClassName(Lineage, ClassTier);
    public string Rank => GameConfig.RankAt(Level);

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

    /// <summary>
    /// Skills a box can roll: built skills of your lineage up to your level, plus
    /// the other starters. A skill you already have becomes mastery XP instead.
    /// </summary>
    public List<SkillInfo> SkillBoxPool() => SkillCatalog.All
        .Where(s => s.Implemented && (s.IsStarter || (s.Lineage == Lineage && s.UnlockLevel <= Level)))
        .ToList();

    /// <summary>Opens a skill box; returns the rolled skill id and whether it was new.</summary>
    public (string SkillId, bool IsNew) OpenSkillBox()
    {
        var pool = SkillBoxPool();
        // New skills are what players want from a box, so roll among unowned ones first.
        var unowned = pool.Where(s => !OwnedSkills.Contains(s.Id)).ToList();
        var from = unowned.Count > 0 ? unowned : pool;
        var skill = from[Rng.RandiRange(0, from.Count - 1)];
        if (OwnedSkills.Contains(skill.Id))
        {
            AddMastery(skill.Id, GameConfig.MasteryFromDuplicateBox);
            return (skill.Id, false);
        }
        OwnedSkills.Add(skill.Id);
        if (EquippedSkills.Count < SkillSlots)
            EquippedSkills.Add(skill.Id);
        return (skill.Id, true);
    }

    // --- Mastery --------------------------------------------------------------

    public int MasteryRank(string skillId)
    {
        int xp = Mastery.GetValueOrDefault(skillId);
        int rank = 1;
        while (rank < GameConfig.MaxMasteryRank && xp >= GameConfig.MasteryXpToNext(rank))
        {
            xp -= GameConfig.MasteryXpToNext(rank);
            rank++;
        }
        return rank;
    }

    /// <summary>Adds mastery XP; returns true on a rank-up.</summary>
    public bool AddMastery(string skillId, int amount)
    {
        int before = MasteryRank(skillId);
        Mastery[skillId] = Mastery.GetValueOrDefault(skillId) + amount;
        int after = MasteryRank(skillId);
        if (after > before)
            Notify?.Invoke($"{SkillCatalog.Get(skillId).Name} mastery rank {after}!", SkillCatalog.Get(skillId).Color);
        return after > before;
    }

    /// <summary>+3% damage per mastery rank above 1.</summary>
    public float SkillDamageMult(string skillId) => 1f + 0.03f * (MasteryRank(skillId) - 1);

    /// <summary>-5% cooldown at mastery ranks 5, 10, 15 and 20.</summary>
    public float SkillCooldownMult(string skillId) => 1f - 0.05f * (MasteryRank(skillId) / 5);

    public float MaxEnergy => GameConfig.BaseMaxEnergy + GameConfig.EnergyPerWellLevel * UpgradeLevel("energy_well");

    // --- Character level ------------------------------------------------------

    public void AddXp(int amount)
    {
        if (Level >= GameConfig.MaxLevel)
            return;
        Xp += amount;
        int slotsBefore = SkillSlots;
        bool leveled = false;
        while (Level < GameConfig.MaxLevel && Xp >= GameConfig.XpToNextLevel(Level))
        {
            Xp -= GameConfig.XpToNextLevel(Level);
            Level++;
            leveled = true;
        }
        if (Level >= GameConfig.MaxLevel)
            Xp = 0;
        if (leveled)
        {
            Notify?.Invoke($"Level {Level}!", new Color(1f, 0.95f, 0.5f));
            if (SkillSlots > slotsBefore)
                Notify?.Invoke($"New skill slot unlocked ({SkillSlots} total)!", new Color(1f, 0.95f, 0.5f));
        }
        Commit(save: leveled);
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

    public float AttackPower => 10f * (1f + 0.12f * (TrainingLevel - 1) + 0.03f * (Level - 1));
    public float MaxHp => 100f + 15f * (TrainingLevel - 1) + 8f * (Level - 1);

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
        float cp = 100f + 25f * (TrainingLevel - 1) + 15f * (Level - 1) + 50f * (ClassTier - 1);
        cp += OwnedSkills.Select(id => SkillCatalog.Get(id).Cp * SkillDamageMult(id)).OrderByDescending(c => c).Take(SkillSlots).Sum();
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
        QuestEvent(QuestGoal.BuyUpgrade);
        Commit();
        return true;
    }

    public int FenceSlots => GameConfig.BaseFenceSlots + UpgradeLevel("fence_slots");
    public int MaxStarsUnlocked => Math.Min(GameConfig.MaxStars, GameConfig.BaseMaxStars + UpgradeLevel("pet_lab"));
    public float MergeDiscount => 0.1f * UpgradeLevel("pet_lab");

    // --- Pets: fence, levels, merging, release --------------------------------

    public Pet? FindPet(int petId) => Pets.FirstOrDefault(p => p.Id == petId);

    public List<Pet> FencedPets() => Pets.Where(p => p.Role == PetRole.Fence).ToList();

    /// <summary>The pet fighting beside the player, if any. It earns no fence gold.</summary>
    public Pet? BattlePet => Pets.FirstOrDefault(p => p.Role == PetRole.Battle);

    /// <summary>Makes a pet the battle pet (replacing any other), or rests it.</summary>
    public void SetBattlePet(int petId, bool battle)
    {
        var pet = FindPet(petId);
        if (pet == null)
            return;
        if (battle)
        {
            if (BattlePet is { } current)
                current.Role = PetRole.None;
            pet.Role = PetRole.Battle;
        }
        else if (pet.Role == PetRole.Battle)
        {
            pet.Role = PetRole.None;
        }
        PetsChanged();
    }

    /// <summary>Damage per battle-pet attack.</summary>
    public float BattlePetDamage(Pet pet) => 4f + PetPower(pet) * 0.5f;

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
            (drop.SkillLearned, drop.SkillIsNew) = OpenSkillBox();
        drop.Xp = GameConfig.KillXp(GameConfig.SpeciesOf(speciesId));
        var species = GameConfig.SpeciesOf(speciesId);
        bool boss = GameConfig.IsBoss(species.Name);
        if (boss || Rng.Randf() < GameConfig.MaterialDropChance)
        {
            drop.Material = GameConfig.MaterialFrom(speciesId);
            drop.MaterialCount = boss ? 3 : 1;
            AddMaterial(drop.Material, drop.MaterialCount);
        }
        QuestEvent(QuestGoal.Kill, speciesId);
        AddXp(drop.Xp);
        Commit();
        return drop;
    }

    // --- Stages ---------------------------------------------------------------

    public bool StageCleared(string stageId) => StageStars.ContainsKey(stageId);

    /// <summary>A chapter map opens once the previous chapter's last stage is cleared.</summary>
    public bool IsChapterUnlocked(int chapter) =>
        HasStarter && (chapter == 1 || StageCleared($"{chapter - 1}-10"));

    /// <summary>Stages open in order along each chapter's road.</summary>
    public bool IsStageUnlocked(StageDef stage) =>
        IsChapterUnlocked(stage.Chapter) && (stage.Number == 1 || StageCleared($"{stage.Chapter}-{stage.Number - 1}"));

    public int HighestUnlockedChapter() =>
        StageCatalog.Chapters.Where(c => IsChapterUnlocked(c.Number)).Select(c => c.Number).DefaultIfEmpty(1).Max();

    /// <summary>What a stage clear paid out.</summary>
    public sealed record StageReward(int Gold, int Xp, bool FirstClear, int Stars, string SkillLearned, bool SkillIsNew);

    /// <summary>Records a clear: first clears pay in full (plus any skill box), repeats pay half.</summary>
    public StageReward CompleteStage(StageDef stage, int stars)
    {
        bool first = !StageCleared(stage.Id);
        int gold = first ? stage.Gold : stage.Gold / 2;
        int xp = first ? stage.Xp : stage.Xp / 2;
        StageStars[stage.Id] = Math.Max(stars, StageStars.GetValueOrDefault(stage.Id));
        Gold += gold;
        string skill = "";
        bool isNew = false;
        if (first && stage.SkillBoxFirstClear)
            (skill, isNew) = OpenSkillBox();
        AddXp(xp);
        Commit();
        return new StageReward(gold, xp, first, stars, skill, isNew);
    }

    // --- Treasure, materials and the Market -------------------------------------

    public const int TreasureBit = 1;
    public const int PuzzleBit = 2;

    public bool ChestOpened(string stageId, int bit) => (StageChests.GetValueOrDefault(stageId) & bit) != 0;

    /// <summary>
    /// Opens a stage chest the first time (later visits find it empty). Returns
    /// a description of the loot, or "" if it was already opened.
    /// Treasure: half the stage's gold and two chapter materials. Puzzle chest:
    /// the stage's gold, three chapter materials and a Crystal Shard.
    /// </summary>
    public string OpenStageChest(StageDef stage, bool puzzle)
    {
        int bit = puzzle ? PuzzleBit : TreasureBit;
        if (ChestOpened(stage.Id, bit))
            return "";
        StageChests[stage.Id] = StageChests.GetValueOrDefault(stage.Id) | bit;
        var mats = GameConfig.ChapterMaterials(stage.Chapter);
        int gold = puzzle ? stage.Gold : stage.Gold / 2;
        Gold += gold;
        var loot = new List<string> { $"{gold} gold" };
        int count = puzzle ? 3 : 2;
        for (int n = 0; n < count; n++)
        {
            string mat = mats[Rng.RandiRange(0, mats.Length - 1)];
            AddMaterial(mat, 1);
            loot.Add(mat);
        }
        if (puzzle)
        {
            AddMaterial("Crystal Shard", 1);
            loot.Add("Crystal Shard");
        }
        Commit();
        return string.Join(", ", loot.GroupBy(l => l).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key));
    }

    public void AddMaterial(string name, int count) => Materials[name] = Materials.GetValueOrDefault(name) + count;

    /// <summary>Sells up to <paramref name="count"/> of a material; returns the gold received.</summary>
    public int SellMaterial(string name, int count)
    {
        int sold = Math.Min(count, Materials.GetValueOrDefault(name));
        if (sold <= 0)
            return 0;
        Materials[name] -= sold;
        if (Materials[name] == 0)
            Materials.Remove(name);
        int gold = sold * GameConfig.Material(name).SellPrice;
        Gold += gold;
        Commit();
        return gold;
    }

    public int ItemCount(string id) => Items.GetValueOrDefault(id);

    /// <summary>Buys one item if affordable and under the carry limit. Skill boxes open at once.</summary>
    public bool BuyItem(string id)
    {
        var item = GameConfig.Item(id);
        if (Gold < item.Price || ItemCount(id) >= item.Max)
            return false;
        Gold -= item.Price;
        if (id == "skill_box")
        {
            var (skill, isNew) = OpenSkillBox();
            Notify?.Invoke(isNew ? $"Skill box: learned {SkillCatalog.Get(skill).Name}!" : $"Skill box: {SkillCatalog.Get(skill).Name} mastery +{GameConfig.MasteryFromDuplicateBox}",
                SkillCatalog.Get(skill).Color);
        }
        else
        {
            Items[id] = ItemCount(id) + 1;
        }
        Commit();
        return true;
    }

    /// <summary>Uses up one item; returns false if none are left.</summary>
    public bool ConsumeItem(string id)
    {
        if (ItemCount(id) <= 0)
            return false;
        Items[id]--;
        Commit();
        return true;
    }

    // --- Story quests ---------------------------------------------------------

    public QuestDef? CurrentQuest => Quests.At(QuestIndex);
    public bool QuestReady => QuestAccepted && CurrentQuest is { } q && QuestProgress >= q.Count;

    public void AcceptQuest()
    {
        if (CurrentQuest == null || QuestAccepted)
            return;
        QuestAccepted = true;
        QuestProgress = 0;
        Commit();
    }

    /// <summary>Hands in the current quest if it's done; returns the finished quest.</summary>
    public QuestDef? TurnInQuest()
    {
        if (!QuestReady || CurrentQuest is not { } quest)
            return null;
        Gold += quest.Gold;
        if (quest.RewardPet != "")
        {
            AddPet(quest.RewardPet, "Normal");
            Notify?.Invoke($"A Monster Echo joins you: {quest.RewardPet}!", GameConfig.RarityColor(GameConfig.SpeciesOf(quest.RewardPet).Rarity));
        }
        if (quest.SkillBox)
        {
            var (skillId, isNew) = OpenSkillBox();
            var skill = SkillCatalog.Get(skillId);
            Notify?.Invoke(isNew ? $"Skill box: learned {skill.Name}!" : $"Skill box: {skill.Name} mastery +{GameConfig.MasteryFromDuplicateBox}",
                skill.Color);
        }
        if (Quests.EndsChapter(QuestIndex))
        {
            CompletedChapter = quest.Chapter;
            Notify?.Invoke($"{Quests.ChapterNames[quest.Chapter]} complete!", new Color(1f, 0.9f, 0.4f));
        }
        QuestIndex++;
        QuestAccepted = false;
        QuestProgress = 0;
        AddXp(quest.Xp);
        Commit();
        return quest;
    }

    /// <summary>Dev/test shortcut: clears every stage of the chapter maps with the given stars.</summary>
    public void DebugClearStages(int stars = 3)
    {
        foreach (var stage in StageCatalog.Chapters.SelectMany(c => c.Stages))
            StageStars[stage.Id] = stars;
        Commit();
    }

    /// <summary>Dev/test shortcut: marks the written story complete and raises the level.</summary>
    public void DebugUnlockWorld(int level = 60)
    {
        QuestIndex = Quests.All.Length;
        QuestAccepted = false;
        QuestProgress = 0;
        CompletedChapter = Quests.StoryChapters;
        while (Level < level)
            AddXp(GameConfig.XpToNextLevel(Level) - Xp);
        Commit();
    }

    /// <summary>Counts an event toward the active quest (kills, swarms, upgrades).</summary>
    public void QuestEvent(QuestGoal goal, string target = "")
    {
        if (!QuestAccepted || CurrentQuest is not { } quest || quest.Goal != goal || QuestReady)
            return;
        // Kill targets name a family: "Slime" counts every kind of slime.
        if (quest.Target != "" && (goal == QuestGoal.Kill ? !target.Contains(quest.Target) : quest.Target != target))
            return;
        QuestProgress++;
        if (QuestReady)
            Notify?.Invoke($"Quest complete: {quest.Title}! Return to {Quests.GiverName}.", new Color(1f, 0.9f, 0.4f));
        Commit();
    }

    /// <summary>Quests about current state (training level, fence, battle pet) track it directly.</summary>
    private void RefreshStateQuest()
    {
        if (!QuestAccepted || CurrentQuest is not { } quest)
            return;
        bool wasReady = QuestReady;
        int value = quest.Goal switch
        {
            QuestGoal.TrainingLevel => TrainingLevel,
            QuestGoal.FencePet => FencedPets().Count,
            QuestGoal.BattlePet => BattlePet != null ? 1 : 0,
            QuestGoal.ClearStage => StageCleared(quest.Target) ? 1 : 0,
            _ => QuestProgress,
        };
        QuestProgress = Math.Min(value, quest.Count);
        if (QuestReady && !wasReady)
            Notify?.Invoke($"Quest complete: {quest.Title}! Return to {Quests.GiverName}.", new Color(1f, 0.9f, 0.4f));
    }

    // --- Persistence ----------------------------------------------------------

    private void PetsChanged()
    {
        PetsVersion++;
        Commit();
    }

    private void Commit(bool save = true)
    {
        RefreshStateQuest();
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
        public int Level { get; set; } = 1;
        public int Xp { get; set; }
        public string Lineage { get; set; } = "";
        public int ClassTier { get; set; } = 1;
        public Dictionary<string, int> Mastery { get; set; } = new();
        public int QuestIndex { get; set; }
        public bool QuestAccepted { get; set; }
        public int QuestProgress { get; set; }
        public int CompletedChapter { get; set; } = -1;
        public Dictionary<string, int> StageStars { get; set; } = new();
        public Dictionary<string, int> StageChests { get; set; } = new();
        public Dictionary<string, int> Materials { get; set; } = new();
        public Dictionary<string, int> Items { get; set; } = new();
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
            Level = Level,
            Xp = Xp,
            Lineage = Lineage,
            ClassTier = ClassTier,
            Mastery = Mastery,
            QuestIndex = QuestIndex,
            QuestAccepted = QuestAccepted,
            QuestProgress = QuestProgress,
            CompletedChapter = CompletedChapter,
            StageStars = StageStars,
            StageChests = StageChests,
            Materials = Materials,
            Items = Items,
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
        ReplaceAll(Mastery, data.Mastery);
        ReplaceAll(StageStars, data.StageStars);
        ReplaceAll(StageChests, data.StageChests);
        ReplaceAll(Materials, data.Materials);
        ReplaceAll(Items, data.Items);
        Level = Math.Clamp(data.Level, 1, GameConfig.MaxLevel);
        Xp = data.Xp;
        ClassTier = Math.Clamp(data.ClassTier, 1, 5);
        QuestIndex = data.QuestIndex;
        QuestAccepted = data.QuestAccepted;
        QuestProgress = data.QuestProgress;
        CompletedChapter = data.CompletedChapter;
        Lineage = data.Lineage;
        MigrateOldSave();
        PetsVersion++;

        // Version 1 saves have no timestamp, so they earn nothing offline.
        // Clock changes can make the gap negative; ApplyOffline clamps it.
        if (data.SavedAt > 0)
            OfflineGold = ApplyOffline(Time.GetUnixTimeFromSystem() - data.SavedAt);
    }

    /// <summary>Brings saves from before levels and lineages up to date.</summary>
    private void MigrateOldSave()
    {
        OwnedSkills.RemoveAll(id => !SkillCatalog.Exists(id));
        EquippedSkills.RemoveAll(id => !OwnedSkills.Contains(id));
        if (Lineage == "" && OwnedSkills.FirstOrDefault(id => SkillCatalog.Get(id).IsStarter) is { } starter)
            Lineage = SkillCatalog.Get(starter).Lineage;
        // Skill slots now come from level: refund the old gold upgrade and keep only the slots you have.
        if (Upgrades.Remove("skill_slots", out int bought))
        {
            int refund = 0;
            for (int i = 0; i < bought; i++)
                refund += (int)Math.Round(2000 * Math.Pow(5, i));
            Gold += refund;
        }
        if (EquippedSkills.Count > SkillSlots)
            EquippedSkills.RemoveRange(SkillSlots, EquippedSkills.Count - SkillSlots);
        // The monster roster was redrawn: carry old Echoes over to their new species.
        foreach (var pet in Pets)
            if (GameConfig.RenamedSpecies.TryGetValue(pet.Species, out var renamed))
            {
                pet.Species = renamed;
                pet.Rarity = GameConfig.SpeciesOf(renamed).Rarity;
            }
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
        Level = 1;
        Xp = 0;
        Lineage = "";
        ClassTier = 1;
        Mastery.Clear();
        QuestIndex = 0;
        QuestAccepted = false;
        QuestProgress = 0;
        CompletedChapter = -1;
        StageStars.Clear();
        StageChests.Clear();
        Materials.Clear();
        Items.Clear();
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
