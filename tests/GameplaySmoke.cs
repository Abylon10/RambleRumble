using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble.Tests;

/// <summary>
/// Plays the real game through its pages: the village (training, Upgrade
/// Station, Market), the gate to the chapter map, a stage (combat, treasure
/// chest, puzzle chest, goal flag), unlocking, defeat, potions and chapter 2.
/// Run: godot --headless --path . res://tests/GameplaySmoke.tscn -- --test
/// </summary>
public partial class GameplaySmoke : TestScene
{
    public override async void _Ready()
    {
        Data.ResetProgress();
        var village = await LoadGame();
        await CheckVillage(village);
        var map = await Go<ChapterMapPage>(village.OpenGate);
        Check(map.Chapter == 1 && map.Selected?.Id == "1-1", "the gate opens Chapter 1 with stage 1-1 selected");
        Check(!Data.IsStageUnlocked(StageCatalog.Get("1-2")), "stage 1-2 starts locked");

        var stage = await Go<StagePage>(() => map.StartSelected());
        await CheckStage(stage);

        map = await Go<ChapterMapPage>(() => Game.Instance.ShowChapterMap(1));
        map.Select(StageCatalog.Get("1-2"));
        stage = await Go<StagePage>(() => map.StartSelected());
        await CheckDefeatAndPotions(stage);

        await CheckChapterTwo();
        Finish();
    }

    private async System.Threading.Tasks.Task CheckVillage(World village)
    {
        foreach (var gender in CharacterArt.Genders)
        {
            var frames = CharacterArt.FramesFor(gender);
            Check(new[] { "idle", "walk", "run", "attack", "hurt", "victory" }.All(a => frames.HasAnimation(a) && frames.GetFrameCount(a) > 0),
                $"{gender} sprite animations all load");
        }
        Data.ChooseStarter("flame_fist");
        village.Hud.StarterPanel.Visible = false;
        Check(Data.HasStarter, "starter skill chosen");

        int levelBefore = Data.TrainingLevel;
        for (int i = 0; i < 30; i++)
            village.OnTrainPressed();
        Check(Data.TrainingLevel > levelBefore, $"training raises level (now {Data.TrainingLevel})");

        village.Player.GlobalPosition = LobbyMap.UpgradeStation;
        for (int i = 0; i < 3; i++)
            await PhysicsFrame();
        Check(village.Hud.UpgradesPanel.Visible, "Upgrade Station opens the Upgrades panel");

        village.Player.GlobalPosition = LobbyMap.Market;
        for (int i = 0; i < 3; i++)
            await PhysicsFrame();
        Check(village.Hud.MarketPanel.Visible, "walking into the Market opens it");
        Data.Gold = 200;
        Check(Data.BuyItem("health_potion") && Data.ItemCount("health_potion") == 1 && Data.Gold == 160, "buy a Health Potion for 40 gold");
        Data.AddMaterial("Slime Gel", 5);
        Check(Data.SellMaterial("Slime Gel", int.MaxValue) == 20 && !Data.Materials.ContainsKey("Slime Gel"), "sell 5 Slime Gel for 20 gold");
        village.Player.GlobalPosition = LobbyMap.SpawnPoint;
    }

    private async System.Threading.Tasks.Task CheckStage(StagePage stage)
    {
        var def = stage.Stage;
        Check(stage.Map.W == StageMapBuilder.Width && stage.Map.H == StageMapBuilder.Length, "stage is a long strip with a road");
        int expected = def.Waves.Sum(w => w.Enemies.Sum(e => e.Count) + (w.Leader != "" ? 1 : 0)) + (def.IsBoss ? 1 : 0);
        Check(stage.EnemiesLeft == expected, $"enemy groups wait along the road ({stage.EnemiesLeft}/{expected})");
        Check(stage.Map.Road.First() == stage.Map.Start && stage.Map.Road.Last() == stage.Map.Goal, "road runs from start to goal flag");
        Check(stage.Map.IsOpen(stage.Map.TreasureChest.X, stage.Map.TreasureChest.Y)
              && stage.Map.PuzzleTiles.All(t => stage.Map.IsOpen(t.X, t.Y)), "treasure and puzzle spots are reachable ground");

        // Reaching the flag early doesn't count.
        stage.Player.GlobalPosition = stage.Map.GoalPoint;
        await ProcessFrame();
        Check(stage.Outcome == StageOutcome.Playing, "the flag needs every enemy defeated first");

        // Real combat against the first group.
        var player = stage.Player;
        var first = stage.AliveMonsters.OrderBy(m => m.GlobalPosition.DistanceTo(stage.Map.StartPoint)).First();
        player.GlobalPosition = first.GlobalPosition + new Vector2(30, 10);
        int kills = Data.Kills;
        for (int t = 0; t < 60 * 8 && Data.Kills == kills; t++)
        {
            player.HealFull();
            player.BasicAttack();
            player.UseSkill(0);
            await PhysicsFrame();
        }
        Check(Data.Kills > kills, "combat kills monsters on the stage");

        // Treasure chest: open once, loot materials.
        int mats = Data.Materials.Values.Sum();
        stage.TreasureChest.TryOpen();
        Check(stage.TreasureChest.IsOpen && Data.ChestOpened(def.Id, PlayerData.TreasureBit) && Data.Materials.Values.Sum() >= mats + 2,
            "treasure chest opens and gives materials");

        // Puzzle chest stays chained until the puzzle (Rune Plates on stage 1) is solved.
        stage.PuzzleChest.TryOpen();
        Check(!stage.PuzzleChest.IsOpen && stage.PuzzleChest.Locked, "puzzle chest is chained while unsolved");
        Check(def.Puzzle == PuzzleKind.RunePlates, "stage 1 uses the Rune Plates puzzle");
        foreach (int piece in stage.Puzzle.Solution())
            stage.Puzzle.Step(piece);
        Check(stage.Puzzle.IsSolved && !stage.PuzzleChest.Locked, "solving the plates unlocks the chest");
        stage.PuzzleChest.TryOpen();
        Check(Data.ChestOpened(def.Id, PlayerData.PuzzleBit) && Data.Materials.GetValueOrDefault("Crystal Shard") >= 1,
            "puzzle chest gives a Crystal Shard");

        foreach (var monster in stage.AliveMonsters)
            monster.TakeDamage(1_000_000f, showNumber: false);
        await ProcessFrame();
        player.HealFull();
        player.GlobalPosition = stage.Map.GoalPoint;
        for (int i = 0; i < 3; i++)
            await ProcessFrame();
        Check(stage.Outcome == StageOutcome.Victory && Data.StageStars.GetValueOrDefault(def.Id) == 3, "reaching the flag clears the stage with 3 stars");
        Check(stage.Reward is { FirstClear: true } && stage.Reward.Gold == def.Gold, "first clear pays full rewards");
        Check(Data.IsStageUnlocked(StageCatalog.Get("1-2")), "clearing 1-1 unlocks 1-2");
        Check(Data.OpenStageChest(def, puzzle: false) == "", "a looted chest stays empty on replays");
    }

    private async System.Threading.Tasks.Task CheckDefeatAndPotions(StagePage stage)
    {
        var player = stage.Player;
        await Wait(0.3);
        player.TakeDamage(player.Hp * 0.8f);
        float hurt = player.Hp;
        stage.UsePotion();
        Check(player.Hp > hurt && Data.ItemCount("health_potion") == 0, $"a potion heals ({hurt:0} -> {player.Hp:0} HP)");

        player.TakeDamage(1_000_000f);
        for (int i = 0; i < 3; i++)
            await ProcessFrame();
        Check(stage.Outcome == StageOutcome.Defeat && !Data.StageCleared("1-2"), "falling in battle ends the stage as a defeat");
    }

    private async System.Threading.Tasks.Task CheckChapterTwo()
    {
        Check(!Data.IsChapterUnlocked(2), "Chapter 2 starts locked");
        Data.DebugClearStages();
        Check(Data.IsChapterUnlocked(2), "clearing 1-10 opens Chapter 2");
        var map = await Go<ChapterMapPage>(() => Game.Instance.ShowChapterMap(2));
        Check(map.Def.Region == "Verdant Forest", "Chapter 2 map is the Verdant Forest");
        map.Select(StageCatalog.Get("2-10"));
        var stage = await Go<StagePage>(() => map.StartSelected());
        Check(stage.AliveMonsters.Any(m => m.IsBoss && m.SpeciesId == "Crimson Fang"), "stage 2-10 has the Crimson Fang boss");
        Check(stage.Stage.Puzzle == PuzzleKind.Braziers, "stage 10 uses the Braziers puzzle");
    }
}
