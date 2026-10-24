using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble.Tests;

/// <summary>
/// Plays the real main scene headless: picks a starter, trains, travels to the
/// Wilderness, kills monsters with every skill, and checks rewards persist.
/// Run: godot --headless --path . res://tests/GameplaySmoke.tscn -- --test
/// </summary>
public partial class GameplaySmoke : TestScene
{
    public override async void _Ready()
    {
        Data.ResetProgress();
        var world = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<World>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, world);
        for (int i = 0; i < 3; i++)
            await PhysicsFrame();
        var player = world.Player;

        foreach (var gender in CharacterArt.Genders)
        {
            var frames = CharacterArt.FramesFor(gender);
            Check(new[] { "idle", "walk", "run", "attack", "hurt", "victory" }.All(a => frames.HasAnimation(a) && frames.GetFrameCount(a) > 0),
                $"{gender} sprite animations all load");
        }
        Data.SetGender("female");
        await PhysicsFrame();
        Check(Data.Gender == "female", "gender switches to female");

        Data.ChooseStarter("flame_fist");
        Check(Data.HasStarter, "starter skill chosen");

        int levelBefore = Data.TrainingLevel;
        for (int i = 0; i < 30; i++)
            world.OnTrainPressed();
        Check(Data.TrainingLevel > levelBefore, $"training raises level (now {Data.TrainingLevel})");

        world.Teleport(Zones.ArrivalPoint, toWilderness: true);
        await PhysicsFrame();
        Check(world.InWilderness && !player.InSafeZone, "portal moves player into the Wilderness");
        Check(world.CurrentZone?.Id == "A", "players arrive in Starter Plains");

        // Learn every starter and reach level 100 (4 skill slots) so four buttons are used.
        foreach (var id in new[] { "wind_slash", "stone_breaker", "thunder_dash" })
            Data.OwnedSkills.Add(id);
        while (Data.Level < 100)
            Data.AddXp(GameConfig.XpToNextLevel(Data.Level));
        foreach (var id in new[] { "wind_slash", "stone_breaker", "thunder_dash" })
            Data.ToggleEquip(id);
        Check(Data.EquippedSkills.Count == 4, "4 skills equipped at level 100");

        int killsBefore = Data.Kills;
        int goldBefore = Data.Gold;
        for (int wave = 0; wave < 8; wave++)
        {
            for (int i = 0; i < 3; i++)
                world.SpawnMonster("Slime", player.GlobalPosition + new Vector2(50, 0).Rotated(i * 2f));
            for (int t = 0; t < 120; t++)
            {
                player.BasicAttack();
                for (int slot = 0; slot < 4; slot++)
                    player.UseSkill(slot);
                await PhysicsFrame();
            }
        }
        Check(Data.Kills > killsBefore, $"monsters die and kills are recorded ({Data.Kills - killsBefore})");
        Check(Data.Gold > goldBefore, $"gold drops awarded ({Data.Gold - goldBefore})");
        await Wait(3.5); // let cooldowns finish
        world.Teleport(Zones.ArrivalPoint, toWilderness: true); // dashes may have carried us through the gate
        await ProcessFrame();
        player.RefillEnergy();
        Check(player.UseSkill(3) && player.CooldownRatio(Data.EquippedSkills[3], 3f) > 0f, "4th skill slot fires (cooldown running)");

        // Force a guaranteed pet roll via pity to check pet records.
        Data.Pity["Rare"] = 24;
        Data.RecordKill("Goblin");
        Check(Data.Pets.Any(p => p.Species == "Goblin"), "pity guarantees a Rare pet at 25 kills");

        int savedGold = Data.Gold;
        int savedPets = Data.Pets.Count;
        Data.SaveGame();
        Data.LoadGame();
        Check(Data.Gold == savedGold && Data.Pets.Count == savedPets, "save/load round-trips gold and pets");

        await CheckWorld(world, player);

        player.TakeDamage(99_999f);
        for (int i = 0; i < 3; i++)
            await PhysicsFrame();
        Check(!world.InWilderness, "defeat sends player back to the Lobby");

        // Walking into the Upgrade Station opens the Upgrades panel.
        world.Hud.StarterPanel.Visible = false;
        player.GlobalPosition = LobbyMap.UpgradeStation;
        for (int i = 0; i < 3; i++)
            await PhysicsFrame();
        Check(world.Hud.UpgradesPanel.Visible, "Upgrade Station opens the Upgrades panel");

        Finish();
    }

    private async System.Threading.Tasks.Task CheckWorld(World world, Player player)
    {
        bool cellsMatch = Zones.All.All(z => z.Cells.All(c => Zones.At(c.GetCenter()) == z));
        Check(cellsMatch && Zones.All.Length == 7, "7 zones, every cell maps back to its zone");
        bool speciesExist = Zones.All.SelectMany(z => z.Spawns).All(s => GameConfig.Species.Any(sp => sp.Name == s.Species));
        Check(speciesExist, "every zone spawns known species");

        var river = Zones.ById("C");
        player.GlobalPosition = river.Cells[0].GetCenter();
        await ProcessFrame();
        await ProcessFrame();
        Check(world.CurrentZone == river && world.Hud.AreaName == "River Area", "walking into the River Area updates the zone and HUD");

        // Sealed regions push the player back out.
        var highlands = Zones.ById("D");
        player.GlobalPosition = highlands.Cells[0].GetCenter();
        await ProcessFrame();
        await ProcessFrame();
        Check(!highlands.Contains(player.GlobalPosition), "sealed Broken Highlands bounces the player back");

        // Monsters near the campfire could hit the player, so clear them for a clean heal check.
        foreach (var monster in world.Monsters.GetChildren().OfType<Monster>().Where(m => m.GlobalPosition.DistanceTo(Zones.Campfires[0]) < 700f))
            monster.QueueFree();
        await Wait(0.5); // let any dash invulnerability wear off
        player.TakeDamage(30f);
        float hurt = player.Hp;
        player.GlobalPosition = Zones.Campfires[0];
        for (int i = 0; i < 30; i++)
            await PhysicsFrame();
        Check(player.Resting && player.Hp > hurt, $"campfire heals ({hurt:0} -> {player.Hp:0} HP)");

        // Let the spawners run: zones respawn every 3-6 s (the Ruins boss every 45 s).
        player.GlobalPosition = Zones.ArrivalPoint;
        for (int i = 0; i < 60 * 7; i++)
            await PhysicsFrame();
        var perZone = world.Monsters.GetChildren().OfType<Monster>().GroupBy(m => m.ZoneId).ToDictionary(g => g.Key, g => g.Count());
        Check(Zones.All.All(z => perZone.GetValueOrDefault(z.Id) > 0),
            "every zone spawns monsters (" + string.Join(", ", Zones.All.Select(z => $"{z.Id}:{perZone.GetValueOrDefault(z.Id)}")) + ")");
        Check(Zones.All.All(z => perZone.GetValueOrDefault(z.Id) <= z.Cap), "no zone goes over its cap");
    }
}
