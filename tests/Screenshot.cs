using System.Linq;
using Godot;

namespace RambleRumble.Tests;

/// <summary>
/// Opens the main scene in a given state and saves a screenshot (needs a window, not --headless).
/// Run: godot --path . res://tests/Screenshot.tscn -- --test &lt;out.png&gt; [starter] [wild] [pets] [upgrades] [fence] [at=x,y] [zoom=z]
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
            world.Player.GlobalPosition = LobbyMap.FenceRect.GetCenter() + new Vector2(0, 150);
        }
        if (args.Contains("wild"))
        {
            world.Teleport(Zones.ArrivalPoint, toWilderness: true);
            foreach (var species in new[] { "Slime", "Slime", "Shroom" })
                world.SpawnMonster(species, Zones.ArrivalPoint + new Vector2((float)GD.RandRange(-260, 260), (float)GD.RandRange(-200, -80)));
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
        // "at=x,y" moves the player there and "zoom=z" zooms the camera out, for map overviews.
        foreach (var arg in args)
        {
            if (arg.StartsWith("at="))
            {
                var xy = arg[3..].Split(',');
                world.Player.GlobalPosition = new Vector2(float.Parse(xy[0]), float.Parse(xy[1]));
                world.Player.InSafeZone = true;
            }
            if (arg.StartsWith("zoom="))
                GetViewport().GetCamera2D().Zoom = Vector2.One * float.Parse(arg[5..]);
        }
        if (args.Any(a => a.StartsWith("at=")))
            GetViewport().GetCamera2D().ResetSmoothing();
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
