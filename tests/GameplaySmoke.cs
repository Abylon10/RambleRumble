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

        world.Teleport(World.WildSpawn, toWilderness: true);
        await PhysicsFrame();
        Check(world.InWilderness && !player.InSafeZone, "portal moves player into the Wilderness");

        // Learn every skill and buy both extra slots so all four buttons are used.
        foreach (var id in new[] { "wind_slash", "stone_breaker", "thunder_dash" })
            Data.OwnedSkills.Add(id);
        Data.Gold = 100_000;
        Data.BuyUpgrade("skill_slots");
        Data.BuyUpgrade("skill_slots");
        foreach (var id in new[] { "wind_slash", "stone_breaker", "thunder_dash" })
            Data.ToggleEquip(id);
        Check(Data.EquippedSkills.Count == 4, "4 skills equipped after buying 2 skill slots");

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
        player.UseSkill(3);
        Check(player.CooldownRatio(Data.EquippedSkills[3], 3f) > 0f, "4th skill slot fires (cooldown running)");

        // Force a guaranteed pet roll via pity to check pet records.
        Data.Pity["Rare"] = 24;
        Data.RecordKill("Goblin");
        Check(Data.Pets.Any(p => p.Species == "Goblin"), "pity guarantees a Rare pet at 25 kills");

        int savedGold = Data.Gold;
        int savedPets = Data.Pets.Count;
        Data.SaveGame();
        Data.LoadGame();
        Check(Data.Gold == savedGold && Data.Pets.Count == savedPets, "save/load round-trips gold and pets");

        player.TakeDamage(99_999f);
        for (int i = 0; i < 3; i++)
            await PhysicsFrame();
        Check(!world.InWilderness, "defeat sends player back to the Lobby");

        Finish();
    }
}
