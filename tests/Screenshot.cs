using System.Linq;
using Godot;

namespace RambleRumble.Tests;

/// <summary>
/// Opens the game in a given state and saves a screenshot (needs a window, not --headless).
/// Run: godot --path . res://tests/Screenshot.tscn -- --test &lt;out.png&gt; [options]
/// Options: new (character creation), garrick, pets, skills, upgrades, market,
/// world, map=1|2 [select=<id>], stage=&lt;id&gt; (add "puzzle" to stand by the puzzle, "goal" by the flag), progress (cleared stages).
/// </summary>
public partial class Screenshot : TestScene
{
    public override async void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        string output = args[1];
        string Arg(string key) => args.FirstOrDefault(a => a.StartsWith(key + "="))?.Split('=')[1] ?? "";

        Data.ResetProgress();
        if (!args.Contains("new"))
        {
            Data.ChooseStarter("flame_fist");
            Data.Gold = 640;
            Data.AddMaterial("Slime Gel", 6);
            Data.AddMaterial("Meadow Leaf", 3);
            Data.AddMaterial("Crystal Shard", 1);
            Data.Items["health_potion"] = 2;
            Data.Items["energy_tonic"] = 1;
        }
        if (args.Contains("progress"))
        {
            foreach (var (id, stars) in new[] { ("1-1", 3), ("1-2", 3), ("1-3", 2), ("1-4", 3) })
                Data.StageStars[id] = stars;
            Data.StageChests["1-1"] = 3;
        }
        var village = await LoadGame();
        village.Hud.StarterPanel.Visible = args.Contains("new");

        if (args.Contains("garrick"))
        {
            village.Player.GlobalPosition = LobbyMap.QuestGiver + new Vector2(60, 40);
            village.TalkToQuestGiver();
        }
        if (args.Contains("pets") || args.Contains("upgrades") || args.Contains("market") || args.Contains("skills"))
        {
            SeedPets();
            if (args.Contains("skills"))
            {
                Data.DebugUnlockWorld(22);
                foreach (var id in new[] { "ember_spark", "ember_arc", "ember_surge" })
                    Data.OwnedSkills.Add(id);
            }
            village.Hud.Open(args.Contains("pets") ? village.Hud.PetsPanel
                : args.Contains("upgrades") ? village.Hud.UpgradesPanel
                : args.Contains("skills") ? village.Hud.SkillsPanel
                : village.Hud.MarketPanel);
            if (args.Contains("market") && args.Contains("sell"))
                village.Hud.MarketPanel.ShowTab(selling: true);
        }

        if (args.Contains("world"))
            await Go<WorldMapPage>(() => Game.Instance.ShowWorldMap());
        if (Arg("map") != "")
        {
            if (Arg("map") == "2")
                Data.DebugClearStages(2);
            var map = await Go<ChapterMapPage>(() => Game.Instance.ShowChapterMap(int.Parse(Arg("map"))));
            if (Arg("select") != "")
                map.Select(StageCatalog.Get(Arg("select")));
        }
        if (Arg("stage") != "")
        {
            var stage = await Go<StagePage>(() => Game.Instance.StartStage(StageCatalog.Get(Arg("stage"))));
            var player = stage.Player;
            player.InSafeZone = true;
            if (args.Contains("puzzle"))
                player.GlobalPosition = Iso.TileCenter(stage.Map.PuzzleTiles[1].X, stage.Map.PuzzleTiles[1].Y) + new Vector2(-50, 10);
            else if (args.Contains("goal"))
                player.GlobalPosition = stage.Map.RoadPoint(0.86f);
            else if (args.Contains("mid"))
                player.GlobalPosition = stage.Map.RoadPoint(0.35f);
            player.Camera.ResetSmoothing();
        }
        await Wait(1.2);
        GetViewport().GetTexture().GetImage().SavePng(output);
        Data.ResetProgress();
        GetTree().Quit();
    }

    private static void SeedPets()
    {
        var specs = new (string Species, string Size, int Stars)[]
        {
            ("Goblin Scout", "Giant", 2), ("Green Wolf", "Big", 1), ("Green Wolf", "Normal", 1), ("Green Slime", "Titan", 1), ("Green Slime", "Normal", 1),
        };
        foreach (var (species, size, stars) in specs)
        {
            var pet = Data.AddPet(species, size);
            pet.Level = 4;
            pet.Stars = stars;
        }
    }
}
