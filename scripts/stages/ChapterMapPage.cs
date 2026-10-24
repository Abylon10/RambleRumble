using System.Collections.Generic;
using System.Linq;
using Godot;
using RambleRumble.UI;

namespace RambleRumble;

/// <summary>
/// A chapter's stage map in the Alien Creeps style: a road winding across the
/// region past its landmarks, with a circle for each of the 10 stages. Tap a
/// stage to see its details and start it on its own page.
/// </summary>
public partial class ChapterMapPage : CanvasLayer
{
    private const float TopBar = 76f;

    public int Chapter { get; init; } = 1;
    public ChapterMapDef Def => StageCatalog.Chapter(Chapter);
    public StageDef? Selected { get; private set; }

    private Control _mapArea = null!;
    private readonly List<StageNodeButton> _nodes = new();
    private PanelContainer _info = null!;
    private Label _infoTitle = null!;
    private Label _infoBody = null!;
    private Button _startButton = null!;

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        World.SetupInput();
        var root = new Control { Theme = new Theme { DefaultFontSize = 20 } };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _mapArea = new ChapterMapArt { Def = Def };
        _mapArea.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _mapArea.OffsetTop = TopBar;
        root.AddChild(_mapArea);
        _mapArea.Resized += PlaceNodes;

        foreach (var stage in Def.Stages)
        {
            var node = new StageNodeButton { Stage = stage };
            node.Pressed += () => Select(stage);
            _mapArea.AddChild(node);
            _nodes.Add(node);
        }

        BuildTopBar(root);
        BuildInfo(root);
        PlaceNodes();

        // Open on the next stage to play.
        Select(Def.Stages.FirstOrDefault(s => Data.IsStageUnlocked(s) && !Data.StageCleared(s.Id)) ?? Def.Stages[0]);
    }

    /// <summary>Starts the selected stage if it's unlocked.</summary>
    public bool StartSelected()
    {
        if (Selected == null || !Data.IsStageUnlocked(Selected))
            return false;
        Game.Instance?.StartStage(Selected);
        return true;
    }

    public void Select(StageDef stage)
    {
        Selected = stage;
        foreach (var node in _nodes)
            node.SetSelected(node.Stage == stage);

        bool unlocked = Data.IsStageUnlocked(stage);
        int stars = Data.StageStars.GetValueOrDefault(stage.Id);
        var enemies = stage.Waves.SelectMany(w => w.Enemies).GroupBy(e => e.Species)
            .Select(g => $"{g.Key} ×{g.Sum(e => e.Count)}").ToList();
        if (stage.Waves.Any(w => w.Leader != ""))
            enemies.Add("Swarm Leader");
        if (stage.IsBoss)
            enemies.Add($"BOSS {stage.Boss}");

        _infoTitle.Text = $"{(stage.IsBoss ? "BOSS  " : "")}Stage {stage.Title}   " +
            (stars > 0 ? new string('★', stars) + new string('☆', 3 - stars) : "");
        _infoTitle.AddThemeColorOverride("font_color", stage.IsBoss ? new Color(1f, 0.5f, 0.45f) : new Color(1f, 0.9f, 0.6f));
        string treasure = Data.ChestOpened(stage.Id, PlayerData.TreasureBit) ? "found" : "hidden";
        string puzzle = Data.ChestOpened(stage.Id, PlayerData.PuzzleBit) ? "solved" : stage.Puzzle switch
        {
            PuzzleKind.RunePlates => "Rune Plates",
            PuzzleKind.Braziers => "Braziers",
            PuzzleKind.Levers => "Levers",
            _ => "Echo Crystals",
        };
        int cp = Data.CombatPower();
        _infoBody.Text =
            (stage.Story != "" ? $"{stage.Story}\n" : "") +
            $"{stage.Landmark}  •  Recommended CP {stage.RecommendedCp} (yours {cp})  •  Treasure: {treasure}  •  Puzzle: {puzzle}\n" +
            $"Main enemies: {string.Join(", ", enemies)}\n" +
            $"Possible drops: gold, Echoes, {string.Join(", ", stage.PossibleMaterials)}" +
            $"\nFirst clear: {stage.Gold} gold, {stage.Xp} XP{(stage.SkillBoxFirstClear ? ", skill box" : "")}   •   Replays: half";
        _infoBody.AddThemeColorOverride("font_color", cp >= stage.RecommendedCp ? new Color(0.9f, 0.9f, 0.88f) : new Color(1f, 0.75f, 0.65f));
        _startButton.Disabled = !unlocked;
        _startButton.Text = unlocked ? (stars > 0 ? "Replay" : "Start") : "Locked";
        _info.Visible = true;
    }

    private void PlaceNodes()
    {
        var size = _mapArea.Size;
        for (int k = 0; k < _nodes.Count; k++)
        {
            var at = Def.Route[k] * size;
            _nodes[k].Position = at - _nodes[k].Size / 2f;
        }
        _mapArea.QueueRedraw();
    }

    private void BuildTopBar(Control root)
    {
        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", UiKit.PanelStyle(new Color(0.55f, 0.45f, 0.3f)));
        bar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        bar.OffsetBottom = TopBar;
        root.AddChild(bar);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        bar.AddChild(row);

        var back = new Button { Text = "◀ Hearthwood", CustomMinimumSize = new Vector2(160, 48) };
        back.Pressed += () => Game.Instance?.ShowVillage(fromGate: true);
        row.AddChild(back);

        var title = UiKit.MakeLabel($"Chapter {Chapter}  •  {Def.Region}", 26, Def.Accent);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(title);

        foreach (var chapter in StageCatalog.Chapters)
        {
            bool open = Data.IsChapterUnlocked(chapter.Number);
            var tab = new Button
            {
                Text = open ? $"Ch {chapter.Number}" : $"Ch {chapter.Number} (clear {chapter.Number - 1}-10)",
                Disabled = !open || chapter.Number == Chapter,
                CustomMinimumSize = new Vector2(80, 48),
            };
            int number = chapter.Number;
            tab.Pressed += () => Game.Instance?.ShowChapterMap(number);
            row.AddChild(tab);
        }

        int earned = Def.Stages.Sum(s => Data.StageStars.GetValueOrDefault(s.Id));
        var stats = UiKit.MakeLabel($"Lv {Data.Level}   Gold {Data.Gold}   ★ {earned}/{Def.Stages.Length * 3}", 18, new Color(1f, 0.9f, 0.55f));
        stats.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(stats);
    }

    private void BuildInfo(Control root)
    {
        _info = new PanelContainer { Visible = false };
        _info.AddThemeStyleboxOverride("panel", UiKit.PanelStyle(new Color(0.9f, 0.75f, 0.35f)));
        UiKit.Place(_info, new Vector2(0.5f, 1f), new Vector2(-420, -206), new Vector2(840, 192));
        root.AddChild(_info);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        _info.AddChild(row);
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(text);
        _infoTitle = UiKit.MakeLabel("", 22);
        text.AddChild(_infoTitle);
        _infoBody = UiKit.MakeLabel("", 15, new Color(0.9f, 0.9f, 0.88f));
        _infoBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.AddChild(_infoBody);
        var buttons = new VBoxContainer();
        row.AddChild(buttons);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(150, 36) };
        close.Pressed += () => _info.Visible = false;
        buttons.AddChild(close);
        _startButton = new Button { CustomMinimumSize = new Vector2(150, 70) };
        _startButton.AddThemeFontSizeOverride("font_size", 24);
        _startButton.Pressed += () => StartSelected();
        buttons.AddChild(_startButton);
    }
}
