using System.Collections.Generic;
using System.Linq;
using Godot;
using RambleRumble.UI;

namespace RambleRumble;

/// <summary>
/// A chapter's stage map, laid out like the UI mockup: the painted chapter map
/// with a marker on each of its 10 stage circles, a row of stage cards
/// underneath, and difficulty tabs. Tapping a stage opens its preparation
/// panel; Start Battle plays it on its own page.
/// </summary>
public partial class ChapterMapPage : CanvasLayer
{
    private const float TopBar = 72f;
    private const float CardsHeight = 204f;

    public int Chapter { get; init; } = 1;
    public ChapterMapDef Def => StageCatalog.Chapter(Chapter);
    public StageDef? Selected { get; private set; }
    public StagePrepPanel Prep { get; private set; } = null!;

    private TextureRect _map = null!;
    private Texture2D? _mapArt;
    private readonly List<StageNodeButton> _nodes = new();
    private readonly List<StageCard> _cards = new();
    private Label _toast = null!;
    private float _toastTime;

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        World.SetupInput();
        var root = new Control { Theme = UiKit.GameTheme() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var backdrop = new ColorRect { Color = new Color(0.05f, 0.07f, 0.11f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(backdrop);

        _mapArt = UiKit.Texture(Def.MapArt);
        _map = new TextureRect
        {
            Texture = _mapArt,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        _map.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _map.OffsetTop = TopBar + 6;
        _map.OffsetBottom = -CardsHeight - 6;
        root.AddChild(_map);
        foreach (var stage in Def.Stages)
        {
            var node = new StageNodeButton { Stage = stage };
            node.Pressed += () => Select(stage);
            root.AddChild(node);
            _nodes.Add(node);
        }
        _map.Resized += PlaceNodes;
        BuildChapterArrows(root);

        BuildTopBar(root);
        BuildCards(root);
        _toast = UiKit.MakeLabel("", 22, UiKit.Cream);
        _toast.HorizontalAlignment = HorizontalAlignment.Center;
        _toast.AddThemeColorOverride("font_outline_color", Colors.Black);
        _toast.AddThemeConstantOverride("outline_size", 8);
        UiKit.Place(_toast, new Vector2(0.5f, 0f), new Vector2(-400, TopBar + 14), new Vector2(800, 40));
        root.AddChild(_toast);

        Prep = new StagePrepPanel { MapArt = _mapArt };
        Prep.StartPressed += _ => StartSelected();
        root.AddChild(Prep);
        PlaceNodes();

        // Highlight the next stage to play.
        Select(Def.Stages.FirstOrDefault(s => Data.IsStageUnlocked(s) && !Data.StageCleared(s.Id)) ?? Def.Stages[0], open: false);
    }

    public override void _Process(double delta)
    {
        if (_toastTime > 0f && (_toastTime -= (float)delta) <= 0f)
            _toast.Text = "";
    }

    /// <summary>Starts the selected stage if it's unlocked.</summary>
    public bool StartSelected()
    {
        if (Selected == null || !Data.IsStageUnlocked(Selected))
            return false;
        Game.Instance?.StartStage(Selected);
        return true;
    }

    /// <summary>Highlights a stage's marker and card and (by default) opens its preparation panel.</summary>
    public void Select(StageDef stage, bool open = true)
    {
        Selected = stage;
        foreach (var node in _nodes)
            node.SetSelected(node.Stage == stage);
        foreach (var card in _cards)
            card.SetSelected(card.Stage == stage);
        if (open)
            Prep.Show(stage, Def.Route[stage.Number - 1]);
    }

    private void Toast(string text)
    {
        _toast.Text = text;
        _toastTime = 2.5f;
    }

    /// <summary>Puts each marker on its painted circle wherever the map picture ends up on screen.</summary>
    private void PlaceNodes()
    {
        if (_mapArt == null)
            return;
        var picture = _mapArt.GetSize();
        float scale = Mathf.Min(_map.Size.X / picture.X, _map.Size.Y / picture.Y);
        var origin = _map.Position + (_map.Size - picture * scale) / 2f;
        for (int k = 0; k < _nodes.Count; k++)
            _nodes[k].Position = origin + Def.Route[k] * picture * scale - _nodes[k].Size / 2f;
    }

    private void BuildChapterArrows(Control root)
    {
        foreach (int step in new[] { -1, 1 })
        {
            int target = Chapter + step;
            if (target < 1 || target > StageCatalog.Chapters.Length)
                continue;
            var arrow = new Button { Text = step < 0 ? "❮" : "❯", CustomMinimumSize = new Vector2(52, 90) };
            arrow.AddThemeFontSizeOverride("font_size", 34);
            UiKit.Place(arrow, new Vector2(step < 0 ? 0 : 1, 0.5f), new Vector2(step < 0 ? 12 : -64, -120), new Vector2(52, 90));
            arrow.Pressed += () =>
            {
                if (Data.IsChapterUnlocked(target))
                    Game.Instance?.ShowChapterMap(target);
                else
                    Toast($"Clear Stage {target - 1}-10 to reach Chapter {target}.");
            };
            root.AddChild(arrow);
        }
    }

    private void BuildTopBar(Control root)
    {
        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", UiKit.NavyStyle(radius: 0, margin: 8f));
        bar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        bar.OffsetBottom = TopBar;
        root.AddChild(bar);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        bar.AddChild(row);

        var back = new Button { Text = "◀  Back", CustomMinimumSize = new Vector2(120, 52) };
        back.Pressed += () => Game.Instance?.ShowWorldMap();
        row.AddChild(back);

        var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 0);
        row.AddChild(titles);
        titles.AddChild(UiKit.MakeLabel($"Chapter {Chapter} - {Def.Region}", 26, UiKit.Cream));
        int earned = Def.Stages.Sum(s => Data.StageStars.GetValueOrDefault(s.Id));
        titles.AddChild(UiKit.MakeLabel($"{Def.Blurb}   ★ {earned}/{Def.Stages.Length * 3}", 14, UiKit.Muted));

        // Only Normal exists so far; the harder modes are shown locked, as in the mockup.
        foreach (var (mode, open) in new[] { ("Normal", true), ("🔒 Hard", false), ("🔒 Nightmare", false) })
        {
            var tab = new Button { Text = mode, CustomMinimumSize = new Vector2(124, 52) };
            if (open)
                tab.AddThemeStyleboxOverride("normal", UiKit.NavyStyle(UiKit.Current, 2, margin: 8f));
            else
                tab.Pressed += () => Toast($"{mode[2..]} mode opens after you clear the chapter on Normal (coming later).");
            row.AddChild(tab);
        }
    }

    private void BuildCards(Control root)
    {
        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 8);
        grid.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        grid.OffsetTop = -CardsHeight;
        grid.OffsetBottom = -8;
        grid.OffsetLeft = 16;
        grid.OffsetRight = -16;
        root.AddChild(grid);
        foreach (var stage in Def.Stages)
        {
            var card = new StageCard { Stage = stage, MapArt = _mapArt, MapPoint = Def.Route[stage.Number - 1] };
            card.Pressed += () => Select(stage);
            grid.AddChild(card);
            _cards.Add(card);
        }
    }
}
