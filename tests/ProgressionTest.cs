using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble.Tests;

/// <summary>
/// First-milestone checks: character level and skill slots, lineage and class,
/// skill boxes and mastery, Rumble energy, the Ember skills, the battle pet,
/// the Chapter 1–2 quest line (with stage 1-3 as the swarm) and save migration.
/// Run: godot --headless --path . res://tests/ProgressionTest.tscn -- --test
/// </summary>
public partial class ProgressionTest : TestScene
{
    private World _village = null!;

    public override async void _Ready()
    {
        CheckLevelsAndSlots();
        CheckSkillBoxesAndMastery();
        CheckMigration();

        Data.ResetProgress();
        _village = await LoadGame();
        await CheckStoryLine();
        await CheckEmberSkills();
        await CheckBattlePet();
        Finish();
    }

    private void CheckLevelsAndSlots()
    {
        Data.ResetProgress();
        Data.ChooseStarter("flame_fist");
        Check(Data.Lineage == "ember" && Data.ClassName == "Flame Initiate", $"Flame Fist starts the Ember lineage as {Data.ClassName}");
        Check(Data.Level == 1 && Data.SkillSlots == 1 && Data.Rank == "Novice", "start at level 1, Novice, 1 skill slot");

        Data.AddXp(GameConfig.XpToNextLevel(1));
        Check(Data.Level == 2, "XP raises level");
        var slots = new[] { (24, 1), (25, 2), (59, 2), (60, 3), (100, 4), (150, 5), (200, 6) };
        Check(slots.All(s => GameConfig.SkillSlotsAt(s.Item1) == s.Item2), "skill slots unlock at levels 25, 60, 100, 150, 200");
        Check(GameConfig.RankAt(81) == "Elite" && GameConfig.RankAt(200) == "Ascended", "ranks follow the level table");

        for (int i = 0; i < 400; i++)
            Data.AddXp(1_000_000);
        Check(Data.Level == GameConfig.MaxLevel, $"level caps at {GameConfig.MaxLevel}");
        Check(Lineages.All.Length == 10 && Lineages.All.All(l => l.Classes.Length == 5), "10 lineages x 5 classes = 50 classes");
    }

    private void CheckSkillBoxesAndMastery()
    {
        Data.ResetProgress();
        Data.ChooseStarter("flame_fist");
        var pool = Data.SkillBoxPool().Select(s => s.Id).ToList();
        Check(pool.All(id => SkillCatalog.Get(id).IsStarter), "at level 1 boxes only hold starters (Ember Spark needs Lv 5)");
        while (Data.Level < 12)
            Data.AddXp(GameConfig.XpToNextLevel(Data.Level));
        pool = Data.SkillBoxPool().Select(s => s.Id).ToList();
        Check(pool.Contains("ember_spark") && pool.Contains("ember_arc") && !pool.Contains("ember_surge"),
            "at level 12 boxes add Ember Spark and Arc, not Surge (Lv 20)");
        Check(!pool.Any(id => !SkillCatalog.Get(id).Implemented), "unbuilt skills never drop");

        for (int i = 0; i < pool.Count; i++)
            Data.OpenSkillBox();
        Check(pool.All(Data.OwnedSkills.Contains), "boxes give new skills until the pool is collected");
        var (dupe, isNew) = Data.OpenSkillBox();
        Check(!isNew && Data.Mastery.GetValueOrDefault(dupe) >= GameConfig.MasteryFromDuplicateBox, "a duplicate box gives mastery XP");

        Data.Mastery.Clear();
        Data.AddMastery("flame_fist", GameConfig.MasteryXpToNext(1));
        Check(Data.MasteryRank("flame_fist") == 2 && Mathf.IsEqualApprox(Data.SkillDamageMult("flame_fist"), 1.03f), "mastery rank 2 gives +3% damage");
        Data.AddMastery("flame_fist", 20 * (2 + 3 + 4));
        Check(Data.MasteryRank("flame_fist") == 5 && Mathf.IsEqualApprox(Data.SkillCooldownMult("flame_fist"), 0.95f), "mastery rank 5 cuts cooldown 5%");
        Data.AddMastery("flame_fist", 1_000_000);
        Check(Data.MasteryRank("flame_fist") == GameConfig.MaxMasteryRank, "mastery caps at rank 20");
    }

    private void CheckMigration()
    {
        Data.ResetProgress();
        // A save from before levels: 2 bought skill-slot upgrades and 4 equipped skills, no lineage.
        string oldSave = """
            {"version":2,"saved_at":0,"gold":100,"owned_skills":["wind_slash","flame_fist","stone_breaker","thunder_dash"],
             "equipped_skills":["wind_slash","flame_fist","stone_breaker","thunder_dash"],"upgrades":{"skill_slots":2,"fence_slots":1},
             "pets":[],"pity":{},"star_shards":{},"next_pet_id":1}
            """;
        using (var file = FileAccess.Open("user://test_save.json", FileAccess.ModeFlags.Write))
            file.StoreString(oldSave);
        Data.LoadGame();
        Check(Data.Gold == 100 + 2000 + 10000, $"old skill-slot upgrades are refunded (gold {Data.Gold})");
        Check(Data.Lineage == "gale" && Data.EquippedSkills.SequenceEqual(new[] { "wind_slash" }),
            "lineage comes from the first starter; equipped skills trimmed to 1 slot");
        Check(!Data.Upgrades.ContainsKey("skill_slots") && Data.UpgradeLevel("fence_slots") == 1, "other upgrades are kept");

        Data.AddXp(500);
        Data.AddMastery("wind_slash", 77);
        Data.SaveGame();
        int level = Data.Level;
        Data.LoadGame();
        Check(Data.Level == level && Data.Mastery["wind_slash"] == 77, "level and mastery survive save/load");
    }

    private async System.Threading.Tasks.Task CheckStoryLine()
    {
        Data.ChooseStarter("flame_fist");
        _village.Hud.StarterPanel.Visible = false;

        _village.TalkToQuestGiver();
        Check(_village.Hud.Dialogue.Visible, "talking to Garrick opens dialogue");

        // Chapter 1-1: the Adventurer Trial (training).
        Data.AcceptQuest();
        while (Data.TrainingLevel < 2)
            _village.OnTrainPressed();
        Check(Data.QuestReady, "the trial completes at Training Lv 2");
        int gold = Data.Gold;
        Check(Data.TurnInQuest()?.Id == "ch1_trial" && Data.Gold == gold + 50, "hand-in pays the reward");

        // Chapter 1-2: the red-streaked slime swarm is stage 1-3.
        Data.AcceptQuest();
        Check(!Data.QuestReady, "the swarm quest waits for stage 1-3");
        var stage = await Go<StagePage>(() => Game.Instance.StartStage(StageCatalog.Get("1-3")));
        var swarm = stage.AliveMonsters;
        Check(swarm.Count == 9 && swarm.All(m => m.Corrupted), $"stage 1-3 holds 9 red-streaked slimes incl. the leader (got {swarm.Count})");
        foreach (var monster in swarm)
            monster.TakeDamage(1_000_000f, showNumber: false);
        await ProcessFrame();
        stage.Player.GlobalPosition = stage.Map.GoalPoint;
        for (int i = 0; i < 3; i++)
            await ProcessFrame();
        Check(stage.Outcome == StageOutcome.Victory && Data.StageCleared("1-3"), "clearing the swarm and reaching the flag clears 1-3");
        Check(Data.QuestReady, "clearing stage 1-3 completes the swarm quest");
        Data.TurnInQuest();
        Check(Data.CompletedChapter == 1, "Chapter 1 complete");
        _village = await Go<World>(() => Game.Instance.ShowVillage());

        // Chapter 2-1: the first Echo (5 slimes, then Garrick's crystal wakes up).
        Data.AcceptQuest();
        for (int i = 0; i < 4; i++)
            Data.RecordKill("Slime");
        Data.RecordKill("Wolf");
        Check(!Data.QuestReady && Data.QuestProgress == 4, "only Slimes count (4/5 after 4 Slimes and a Wolf)");
        Data.RecordKill("Slime");
        Check(Data.QuestReady, "5th Slime completes the quest");
        int echoes = Data.Pets.Count(p => p.Species == "Slime");
        Data.TurnInQuest();
        Check(Data.Pets.Count(p => p.Species == "Slime") == echoes + 1, "hand-in gives a Slime Echo");

        // Chapter 2-2: the ranch fence (accepting checks the current state).
        Data.AcceptQuest();
        Check(Data.QuestReady, "the Echo is already in the ranch fence");
        Data.TurnInQuest();

        // Chapter 2-3: any upgrade.
        Data.AcceptQuest();
        Data.Gold += 1000;
        Data.BuyUpgrade("fence_slots");
        Check(Data.QuestReady, "buying an upgrade completes 'Mending Fences'");
        Data.TurnInQuest();

        // Chapter 2-4: battle companion, then the chapter ends with a skill box.
        Data.AcceptQuest();
        Data.SetBattlePet(Data.Pets[0].Id, true);
        Check(Data.QuestReady, "choosing a battle companion completes 'Walking Together'");
        int skillsBefore = Data.OwnedSkills.Count + Data.Mastery.Values.Sum();
        Data.TurnInQuest();
        Check(Data.OwnedSkills.Count + Data.Mastery.Values.Sum() > skillsBefore, "the chapter finale hands out a skill box");
        Check(Data.CompletedChapter == 2 && Data.CurrentQuest == null, "Chapter 2 complete; no quests left");
        GD.Print($"  info  after Chapter 2 of the story the player is level {Data.Level}");
    }

    private async System.Threading.Tasks.Task CheckEmberSkills()
    {
        // Level 20: Goblins (Lv 15) still count as a real fight for mastery.
        while (Data.Level < 20)
            Data.AddXp(GameConfig.XpToNextLevel(Data.Level));
        foreach (var id in new[] { "ember_spark", "ember_arc", "ember_surge", "ember_guard", "ember_step" })
        {
            if (!Data.OwnedSkills.Contains(id))
                Data.OwnedSkills.Add(id);
        }
        Data.SetBattlePet(Data.Pets[0].Id, false);
        var stage = await Go<StagePage>(() => Game.Instance.StartStage(StageCatalog.Get("1-1")));
        var player = stage.Player;

        foreach (var id in new[] { "ember_spark", "ember_arc", "ember_surge", "ember_guard", "ember_step" })
        {
            var skill = SkillCatalog.Get(id);
            Data.EquippedSkills.Clear();
            Data.EquippedSkills.Add(id);
            player.RefillEnergy();
            var target = stage.SpawnAt("Goblin", player.GlobalPosition + player.Facing * 50f);
            await PhysicsFrame();
            float energy = player.Energy;
            int mastery = Data.Mastery.GetValueOrDefault(id);
            bool cast = player.UseSkill(0);
            await PhysicsFrame();
            string detail = skill.Archetype switch
            {
                SkillArchetype.Surge => player.SurgeMult > 1f ? "damage buff on" : "NO BUFF",
                SkillArchetype.Guard => player.Guarding ? "guard on" : "NO GUARD",
                _ => "",
            };
            Check(cast && player.Energy < energy && Data.Mastery.GetValueOrDefault(id) > mastery && !detail.StartsWith("NO"),
                $"{skill.Name} casts, costs {skill.Cost} energy and trains mastery {detail}");
            if (IsInstanceValid(target))
                target.QueueFree();
        }

        // Not enough energy: the cast is refused, and energy regenerates.
        await Wait(1.0);
        Data.EquippedSkills.Clear();
        Data.EquippedSkills.Add("ember_spark");
        player.SetEnergy(2f);
        Check(!player.UseSkill(0), "a skill can't be cast without enough energy");
        await Wait(1.0);
        Check(player.Energy > 2f, $"energy regenerates ({player.Energy:0} after 1 s)");
    }

    private async System.Threading.Tasks.Task CheckBattlePet()
    {
        var pet = Data.Pets[0];
        Data.SetBattlePet(pet.Id, true);
        Check(Data.BattlePet == pet && !Data.FencedPets().Contains(pet), "battle pet leaves the fence (no gold while fighting)");
        var stage = await Go<StagePage>(() => Game.Instance.StartStage(StageCatalog.Get("1-1")));
        int kills = Data.Kills;
        var foe = stage.SpawnAt("Slime", stage.Player.GlobalPosition + new Vector2(60, 0));
        // The player never attacks (and is kept alive); only the pet fights.
        for (int i = 0; i < 60 * 15 && Data.Kills == kills; i++)
        {
            stage.Player.HealFull();
            await PhysicsFrame();
        }
        Check(Data.Kills > kills, "the battle pet defeats a monster on its own");
    }
}
