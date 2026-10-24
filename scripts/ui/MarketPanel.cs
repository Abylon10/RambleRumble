using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble.UI;

/// <summary>The Hearthwood Market: buy potions, tonics and skill boxes; sell materials from stages.</summary>
public partial class MarketPanel : PanelContainer
{
    private VBoxContainer _list = null!;
    private Label _gold = null!;
    private Button _buyTab = null!;
    private Button _sellTab = null!;
    private bool _selling;

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        var (box, header) = UiKit.MakeModal(this, "Hearthwood Market", new Vector2(720, 540));
        _gold = UiKit.MakeLabel("", 18, new Color(1f, 0.85f, 0.3f));
        header.AddChild(_gold);
        header.MoveChild(_gold, 1);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 10);
        box.AddChild(tabs);
        _buyTab = new Button { Text = "Buy", CustomMinimumSize = new Vector2(140, 46), ToggleMode = true, ButtonPressed = true };
        _sellTab = new Button { Text = "Sell", CustomMinimumSize = new Vector2(140, 46), ToggleMode = true };
        _buyTab.Pressed += () => ShowTab(selling: false);
        _sellTab.Pressed += () => ShowTab(selling: true);
        tabs.AddChild(_buyTab);
        tabs.AddChild(_sellTab);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_list);

        Data.Changed += Refresh;
        VisibilityChanged += Refresh;
    }

    public override void _ExitTree() => Data.Changed -= Refresh;

    public void ShowTab(bool selling)
    {
        _selling = selling;
        _buyTab.ButtonPressed = !selling;
        _sellTab.ButtonPressed = selling;
        Refresh();
    }

    public void Refresh()
    {
        if (!Visible)
            return;
        _gold.Text = $"Gold {Data.Gold}   ";
        foreach (var child in _list.GetChildren())
            child.QueueFree();
        if (_selling)
            BuildSell();
        else
            BuildBuy();
    }

    private void BuildBuy()
    {
        foreach (var item in GameConfig.Items)
        {
            string owned = item.Id == "skill_box" ? "" : $"  (have {Data.ItemCount(item.Id)}/{item.Max})";
            var row = Row($"{item.Name}{owned}\n{item.Desc}", item.Color);
            var buy = new Button
            {
                Text = $"Buy  {item.Price}g",
                CustomMinimumSize = new Vector2(150, 50),
                Disabled = Data.Gold < item.Price || (item.Id != "skill_box" && Data.ItemCount(item.Id) >= item.Max),
            };
            string id = item.Id;
            buy.Pressed += () => Data.BuyItem(id);
            row.AddChild(buy);
            _list.AddChild(row);
        }
    }

    private void BuildSell()
    {
        var owned = GameConfig.Materials.Where(m => Data.Materials.GetValueOrDefault(m.Name) > 0).ToList();
        if (owned.Count == 0)
        {
            _list.AddChild(UiKit.MakeLabel("No materials yet. Monsters and stage chests drop them.", 18, new Color(0.75f, 0.75f, 0.75f)));
            return;
        }
        foreach (var material in owned)
        {
            int count = Data.Materials[material.Name];
            var row = Row($"{material.Name}  ×{count}\n{material.SellPrice} gold each", material.Color);
            string name = material.Name;
            var one = new Button { Text = $"Sell 1", CustomMinimumSize = new Vector2(100, 50) };
            one.Pressed += () => Data.SellMaterial(name, 1);
            row.AddChild(one);
            var all = new Button { Text = $"Sell all  +{count * material.SellPrice}g", CustomMinimumSize = new Vector2(170, 50) };
            all.Pressed += () => Data.SellMaterial(name, int.MaxValue);
            row.AddChild(all);
            _list.AddChild(row);
        }
    }

    private static HBoxContainer Row(string text, Color colour)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var swatch = new ColorRect { Color = colour, CustomMinimumSize = new Vector2(28, 28), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddChild(swatch);
        var label = UiKit.MakeLabel(text, 16);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);
        return row;
    }
}
