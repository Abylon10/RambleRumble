using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RambleRumble.UI;

namespace RambleRumble;

/// <summary>
/// Puts the game world together: the Lobby and Wilderness maps, their walls,
/// walk-in stations, portals and campfires, per-zone monster spawning, the
/// player and the HUD.
/// </summary>
public partial class World : Node2D
{
    private const float Wall = 40f;
    private const float PortalRadius = 46f;
    private const float CampfireHeal = 15f;

    public Player Player { get; private set; } = null!;
    public Hud Hud { get; private set; } = null!;
    public Node2D Monsters { get; private set; } = null!;
    public bool InWilderness { get; private set; }
    /// <summary>The Wilderness zone the player is in, or null in the Lobby.</summary>
    public ZoneInfo? CurrentZone { get; private set; }

    private bool _inTraining;
    private int _campfiresTouching;
    private int _shownTrainingLevel;
    private readonly Dictionary<string, float> _spawnTimers = new();
    private readonly RandomNumberGenerator _rng = new();

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        _rng.Randomize();
        SetupInput();

        AddChild(new LobbyMap());
        AddChild(new WildernessMap());
        AddChild(new FenceDisplay { Area = LobbyMap.FenceRect });
        BuildWalls(LobbyMap.Bounds);
        BuildWalls(Zones.WildRect);

        BuildRectZone(LobbyMap.TrainingGround, SetTraining);
        BuildCircleZone(LobbyMap.Portal, PortalRadius, entered => { if (entered) Defer(() => Teleport(Zones.ArrivalPoint, toWilderness: true)); });
        BuildCircleZone(Zones.Gate, PortalRadius, entered => { if (entered) Defer(() => Teleport(LobbyMap.ReturnPoint, toWilderness: false)); });
        BuildCircleZone(LobbyMap.UpgradeStation, LobbyMap.StationRadius, entered => { if (entered) Hud.Open(Hud.UpgradesPanel); });
        BuildStationMessage(LobbyMap.RaidBoard, "Raid Board: raids on other players' bases arrive in Phase 4.");
        BuildStationMessage(LobbyMap.CosmeticArea, "Cosmetics & Titles: coming in Phase 6.");
        BuildStationMessage(LobbyMap.Leaderboard, "Leaderboard: arrives with online play in Phase 5.");
        BuildStationMessage(LobbyMap.EventArea, "Event Area: swarm events arrive in Phase 3.");
        foreach (var fire in Zones.Campfires)
            BuildCircleZone(fire, Zones.CampfireRadius, entered => _campfiresTouching += entered ? 1 : -1);

        Monsters = new Node2D();
        AddChild(Monsters);

        Player = new Player { Position = LobbyMap.SpawnPoint };
        Player.Defeated += OnPlayerDefeated;
        AddChild(Player);

        Hud = new Hud { Player = Player };
        Hud.TrainPressed += OnTrainPressed;
        AddChild(Hud);
        _shownTrainingLevel = Data.TrainingLevel;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_inTraining)
            Data.Train(GameConfig.TrainIdleXpPerSec * dt);
        if (Data.TrainingLevel > _shownTrainingLevel)
            Player.PlayOneShot("victory");
        _shownTrainingLevel = Data.TrainingLevel;

        Player.Resting = _campfiresTouching > 0;
        if (Player.Resting)
            Player.Heal(CampfireHeal * dt);

        if (InWilderness)
            TrackZone();
        foreach (var zone in Zones.All)
            TickSpawner(zone, dt);
    }

    // --- Setup ----------------------------------------------------------------

    private static void SetupInput()
    {
        var keys = new Dictionary<string, Key[]>
        {
            ["move_left"] = new[] { Key.A, Key.Left },
            ["move_right"] = new[] { Key.D, Key.Right },
            ["move_up"] = new[] { Key.W, Key.Up },
            ["move_down"] = new[] { Key.S, Key.Down },
            ["attack"] = new[] { Key.J, Key.Space },
            ["skill_1"] = new[] { Key.K },
            ["skill_2"] = new[] { Key.L },
            ["skill_3"] = new[] { Key.U },
            ["skill_4"] = new[] { Key.I },
        };
        foreach (var (action, actionKeys) in keys)
        {
            if (InputMap.HasAction(action))
                continue;
            InputMap.AddAction(action);
            foreach (var key in actionKeys)
                InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
        }
    }

    private void BuildWalls(Rect2 rect)
    {
        var body = new StaticBody2D { CollisionLayer = 4, CollisionMask = 0 };
        Rect2[] walls =
        {
            new(rect.Position.X - Wall, rect.Position.Y - Wall, rect.Size.X + Wall * 2, Wall),
            new(rect.Position.X - Wall, rect.End.Y, rect.Size.X + Wall * 2, Wall),
            new(rect.Position.X - Wall, rect.Position.Y, Wall, rect.Size.Y),
            new(rect.End.X, rect.Position.Y, Wall, rect.Size.Y),
        };
        foreach (var wall in walls)
            body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = wall.Size }, Position = wall.GetCenter() });
        AddChild(body);
    }

    /// <summary>An area that reports the player entering (true) and leaving (false).</summary>
    private void BuildZone(Vector2 position, Shape2D shape, Action<bool> onChange)
    {
        var area = new Area2D { CollisionLayer = 0, CollisionMask = 1, Position = position };
        area.AddChild(new CollisionShape2D { Shape = shape });
        area.BodyEntered += body => { if (body is Player) onChange(true); };
        area.BodyExited += body => { if (body is Player) onChange(false); };
        AddChild(area);
    }

    private void BuildRectZone(Rect2 rect, Action<bool> onChange) =>
        BuildZone(rect.GetCenter(), new RectangleShape2D { Size = rect.Size }, onChange);

    private void BuildCircleZone(Vector2 center, float radius, Action<bool> onChange) =>
        BuildZone(center, new CircleShape2D { Radius = radius }, onChange);

    private void BuildStationMessage(Vector2 at, string message) =>
        BuildCircleZone(at, LobbyMap.StationRadius, entered => { if (entered) Hud.Toast(message, new Color(0.9f, 0.85f, 0.7f)); });

    /// <summary>Physics state can't change inside area callbacks, so run after them.</summary>
    private static void Defer(Action action) => Callable.From(action).CallDeferred();

    // --- Gameplay -------------------------------------------------------------

    public void Teleport(Vector2 destination, bool toWilderness)
    {
        Player.GlobalPosition = destination;
        Player.InSafeZone = !toWilderness;
        InWilderness = toWilderness;
        CurrentZone = null;
        Hud.SetArea("Lobby", new Color(0.9f, 0.75f, 0.5f));
        if (toWilderness)
            TrackZone();
        GetViewport().GetCamera2D()?.ResetSmoothing();
    }

    private void TrackZone()
    {
        var zone = Zones.At(Player.GlobalPosition);
        if (zone == null || zone == CurrentZone)
            return;
        CurrentZone = zone;
        Hud.SetArea(zone.Name, zone.TierColor);
        Hud.Toast($"Entering {zone.Title}", zone.TierColor);
    }

    private void SetTraining(bool active)
    {
        _inTraining = active;
        Hud.SetTrainingAvailable(active);
    }

    public void OnTrainPressed()
    {
        Data.Train(GameConfig.TrainTapXp);
        Fx.Spawn(this, LobbyMap.TrainingGround.GetCenter(), FxKind.Ring, new Color(0.5f, 0.9f, 1f), radius: 50f, duration: 0.25f);
    }

    private void TickSpawner(ZoneInfo zone, float dt)
    {
        float left = _spawnTimers.GetValueOrDefault(zone.Id) - dt;
        if (left > 0f)
        {
            _spawnTimers[zone.Id] = left;
            return;
        }
        _spawnTimers[zone.Id] = zone.Respawn;
        int alive = Monsters.GetChildren().OfType<Monster>().Count(m => m.ZoneId == zone.Id && !m.IsQueuedForDeletion());
        if (alive < zone.Cap)
            SpawnMonster(PickSpecies(zone), RandomPointIn(zone), zone.Id);
    }

    public Monster SpawnMonster(string speciesId, Vector2 at, string zoneId = "")
    {
        var monster = new Monster();
        monster.Setup(speciesId, at, Player, zoneId);
        monster.Died += OnMonsterDied;
        Monsters.AddChild(monster);
        return monster;
    }

    /// <summary>A random point in the zone, away from the player and the gate when possible.</summary>
    private Vector2 RandomPointIn(ZoneInfo zone)
    {
        var position = Vector2.Zero;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var cell = zone.Cells[_rng.RandiRange(0, zone.Cells.Length - 1)].Grow(-80);
            position = new Vector2(_rng.RandfRange(cell.Position.X, cell.End.X), _rng.RandfRange(cell.Position.Y, cell.End.Y));
            bool clear = position.DistanceTo(Player.GlobalPosition) > 350f
                && position.DistanceTo(Zones.ArrivalPoint) > 300f
                && Zones.Campfires.All(f => position.DistanceTo(f) > Zones.CampfireRadius + 60f);
            if (clear)
                break;
        }
        return position;
    }

    private string PickSpecies(ZoneInfo zone)
    {
        int roll = _rng.RandiRange(1, zone.Spawns.Sum(s => s.Weight));
        foreach (var (species, weight) in zone.Spawns)
        {
            roll -= weight;
            if (roll <= 0)
                return species;
        }
        return zone.Spawns[0].Species;
    }

    private void OnMonsterDied(Monster monster)
    {
        var drop = Data.RecordKill(monster.SpeciesId);
        Fx.FloatingText(this, monster.GlobalPosition, $"+{drop.Gold} gold", new Color(1f, 0.85f, 0.3f), 0.9f);
        if (drop.AddedPet is { } pet)
        {
            string note = drop.PityHit ? "  (pity!)" : "";
            Hud.Toast($"New pet: {pet.Size} {pet.Species} ({pet.Rarity}){note}", GameConfig.RarityColor(pet.Rarity));
        }
        if (drop.SkillBox)
        {
            if (drop.SkillLearned != "")
            {
                var skill = GameConfig.Skill(drop.SkillLearned);
                Hud.Toast($"Skill box: learned {skill.Name}!", skill.Color);
            }
            else
            {
                Hud.Toast($"Skill box: duplicate, +{GameConfig.SkillBoxDuplicateGold} gold", new Color(1f, 0.85f, 0.3f));
            }
        }
    }

    private void OnPlayerDefeated()
    {
        Hud.Toast("Defeated! Back to the Lobby.", new Color(1f, 0.4f, 0.4f));
        Defer(() => Teleport(LobbyMap.ReturnPoint, toWilderness: false));
        Player.HealFull();
    }
}
