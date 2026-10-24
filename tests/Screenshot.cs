using System.Linq;
using Godot;

namespace RambleRumble.Tests;

/// <summary>
/// Opens the main scene in a given state and saves a screenshot (needs a window, not --headless).
/// Run: godot --path . res://tests/Screenshot.tscn -- --test &lt;out.png&gt; [starter] [wild] [pets] [upgrades] [fence]
/// </summary>
public partial class Screenshot : Node
{
    public override async void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        string output = args[1];
        var data = PlayerData.Instance;
        data.ResetProgress();
        var world = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<World>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, world);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);

        if (args.Contains("starter"))
        {
            data.ChooseStarter("wind_slash");
            world.Hud.StarterPanel.Visible = false;
        }
        if (args.Contains("pets") || args.Contains("upgrades") || args.Contains("fence"))
        {
            SeedPets(data);
            world.Player.GlobalPosition = new Vector2(-340, 150);
        }
        if (args.Contains("wild"))
        {
            world.Teleport(World.WildSpawn, toWilderness: true);
            for (int i = 0; i < 6; i++)
                world.SpawnMonster();
        }
        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        if (args.Contains("pets"))
        {
            world.Hud.Open(world.Hud.PetsPanel);
            world.Hud.PetsPanel.SelectedId = 1;
            world.Hud.PetsPanel.Refresh();
        }
        if (args.Contains("upgrades"))
            world.Hud.Open(world.Hud.UpgradesPanel);
        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        GetViewport().GetTexture().GetImage().SavePng(output);
        data.ResetProgress();
        GetTree().Quit();
    }

    private static void SeedPets(PlayerData data)
    {
        data.Gold = 3200;
        var specs = new (string Species, string Size, int Stars)[]
        {
            ("Goblin", "Giant", 2), ("Wolf", "Big", 1), ("Wolf", "Normal", 1), ("Slime", "Titan", 1),
            ("Slime", "Normal", 1), ("Slime", "Tiny", 1), ("Wolf", "Tiny", 1),
        };
        foreach (var (species, size, stars) in specs)
        {
            var pet = data.AddPet(species, size);
            pet.Level = 4;
            pet.Xp = 15;
            pet.Stars = stars;
            data.SetFenced(pet.Id, false);
        }
        foreach (int id in new[] { 1, 2, 4 })
            data.SetFenced(id, true);
        data.StarShards["Uncommon"] = 1;
    }
}
