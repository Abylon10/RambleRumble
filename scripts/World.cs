using System;
using System.Collections.Generic;
using Godot;
using RambleRumble.UI;

namespace RambleRumble;

/// <summary>
/// Hearthwood Village, the walkable hub: the Training Square, the ranch and
/// its Echo fence, the Upgrade Station, Garrick, and the gate that opens the
/// chapter maps (the stages themselves load on their own pages).
/// </summary>
public partial class World : Node2D
{
    private const float Wall = 40f;
    private const float PortalRadius = 46f;
    private const float TalkRadius = 90f;
    private static readonly Color VillageColor = new(0.9f, 0.75f, 0.5f);

    /// <summary>Start beside the gate (coming back from the maps) instead of the spawn point.</summary>
    public bool ArriveAtGate { get; init; }

    public Player Player { get; private set; } = null!;
    public Hud Hud { get; private set; } = null!;

    private bool _inTraining;
    private bool _nearGiver;
    private int _shownTrainingLevel;

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        SetupInput();

        AddChild(new LobbyMap());
        AddChild(new FenceDisplay { Area = LobbyMap.FenceRect });
        BuildWalls(LobbyMap.Bounds);

        BuildRectZone(LobbyMap.TrainingGround, SetTraining);
        BuildCircleZone(LobbyMap.Portal, PortalRadius, entered => { if (entered) Defer(OpenGate); });
        BuildCircleZone(LobbyMap.UpgradeStation, LobbyMap.StationRadius, entered => { if (entered) Hud.Open(Hud.UpgradesPanel); });
        BuildStationMessage(LobbyMap.Guild, "Rambler Guild: closed for now. It reopens under Kael in Chapter 5.");
        BuildStationMessage(LobbyMap.CosmeticArea, "Cosmetics & Titles: coming later.");
        BuildCircleZone(LobbyMap.Market, LobbyMap.StationRadius, entered => { if (entered) Hud.Open(Hud.MarketPanel); });
        BuildCircleZone(LobbyMap.EventArea, LobbyMap.StationRadius, entered => { if (entered) ShowQuestBoard(); });
        BuildCircleZone(LobbyMap.QuestGiver, TalkRadius, entered =>
        {
            _nearGiver = entered;
            Hud.SetTalkAvailable(entered && Data.HasStarter);
        });
        AddChild(new QuestGiverNpc { Position = LobbyMap.QuestGiver });

        // The village is safe: no monsters, no damage.
        Player = new Player { Position = ArriveAtGate ? LobbyMap.ReturnPoint : LobbyMap.SpawnPoint, InSafeZone = true };
        AddChild(Player);
        AddChild(new BattlePetNode { Player = Player, Position = Player.Position });

        Hud = new Hud { Player = Player };
        Hud.TrainPressed += OnTrainPressed;
        Hud.TalkPressed += TalkToQuestGiver;
        AddChild(Hud);
        Hud.SetArea("Hearthwood Village", VillageColor);
        _shownTrainingLevel = Data.TrainingLevel;
    }

    public override void _Process(double delta)
    {
        if (_inTraining)
            Data.Train(GameConfig.TrainIdleXpPerSec * (float)delta);
        if (Data.TrainingLevel > _shownTrainingLevel)
            Player.PlayOneShot("victory");
        _shownTrainingLevel = Data.TrainingLevel;
        if (_nearGiver && Input.IsActionJustPressed("talk"))
            TalkToQuestGiver();
    }

    // --- Setup ----------------------------------------------------------------

    /// <summary>Keyboard bindings for desktop play; every page calls this.</summary>
    public static void SetupInput()
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
            ["skill_5"] = new[] { Key.O },
            ["skill_6"] = new[] { Key.P },
            ["talk"] = new[] { Key.E },
            ["use_potion"] = new[] { Key.Q },
            ["use_tonic"] = new[] { Key.R },
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

    // --- Village --------------------------------------------------------------

    /// <summary>The gate leads to the latest chapter map you've unlocked.</summary>
    public void OpenGate()
    {
        if (!Data.HasStarter)
            return;
        Game.Instance?.ShowChapterMap(Data.HighestUnlockedChapter());
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

    // --- Story ----------------------------------------------------------------

    /// <summary>Garrick: offers the current quest, reminds, or takes the hand-in.</summary>
    public void TalkToQuestGiver()
    {
        if (!Data.HasStarter)
            return;
        var quest = Data.CurrentQuest;
        if (quest == null)
        {
            Hud.Dialogue.Say(Quests.GiverName, "That's all I can ask of you for now. Rest, train, and look after your Echoes. " +
                "The forest won't wait forever; Chapter 3 is coming.");
        }
        else if (!Data.QuestAccepted)
        {
            Hud.Dialogue.Say($"{Quests.GiverName}  •  {quest.Title}", quest.Intro, "Accept", () =>
            {
                Data.AcceptQuest();
                Hud.Dialogue.Visible = false;
                Hud.Toast($"New quest: {quest.Title}", new Color(1f, 0.9f, 0.4f));
            });
        }
        else if (Data.QuestReady)
        {
            Hud.Dialogue.Say($"{Quests.GiverName}  •  {quest.Title}", quest.Outro, $"Complete (+{quest.Gold}g, +{quest.Xp} XP)", () =>
            {
                Data.TurnInQuest();
                TalkToQuestGiver(); // straight on to the next quest's offer
            });
        }
        else
        {
            Hud.Dialogue.Say(Quests.GiverName, $"How goes it? Remember: {quest.Objective.ToLower()}.");
        }
    }

    private void ShowQuestBoard()
    {
        var quest = Data.CurrentQuest;
        string text = quest == null ? "Quest Board: no new quests right now."
            : !Data.QuestAccepted ? $"Quest Board: {Quests.GiverName} has a task for you: {quest.Title}."
            : $"Quest Board: {quest.Title}: {quest.Objective}.";
        Hud.Toast(text, new Color(0.9f, 0.85f, 0.7f));
    }
}
