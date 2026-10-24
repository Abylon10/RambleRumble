using System.Collections.Generic;
using Godot;

namespace RambleRumble.UI;

/// <summary>Gold upgrades: fence slots, skill slots, training ground and pet lab.</summary>
public partial class UpgradesPanel : PanelContainer
{
    private sealed record Row(Label Title, Label Detail, Button Buy);

    private readonly Dictionary<string, Row> _rows = new();

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        var (box, _) = UiKit.MakeModal(this, "Upgrades", new Vector2(760, 480));
        foreach (var upgrade in GameConfig.Upgrades)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            var textBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var title = UiKit.MakeLabel("", 20, new Color(1f, 0.9f, 0.7f));
            var detail = UiKit.MakeLabel("", 15, new Color(0.8f, 0.8f, 0.8f));
            textBox.AddChild(title);
            textBox.AddChild(detail);
            row.AddChild(textBox);
            var button = new Button { CustomMinimumSize = new Vector2(180, 56) };
            string id = upgrade.Id;
            button.Pressed += () => Data.BuyUpgrade(id);
            row.AddChild(button);
            box.AddChild(row);
            _rows[id] = new Row(title, detail, button);
        }
        Data.Changed += Refresh;
        VisibilityChanged += Refresh;
    }

    public override void _ExitTree() => Data.Changed -= Refresh;

    public void Refresh()
    {
        if (!Visible)
            return;
        foreach (var upgrade in GameConfig.Upgrades)
        {
            var row = _rows[upgrade.Id];
            row.Title.Text = $"{upgrade.Name}   Lv {Data.UpgradeLevel(upgrade.Id)} / {upgrade.Max}";
            row.Detail.Text = $"{upgrade.Desc}\nNow: {CurrentEffect(upgrade.Id)}";
            if (Data.UpgradeMaxed(upgrade.Id))
            {
                row.Buy.Text = "Maxed";
                row.Buy.Disabled = true;
            }
            else
            {
                int cost = Data.NextUpgradeCost(upgrade.Id);
                row.Buy.Text = $"Buy  {cost}g";
                row.Buy.Disabled = Data.Gold < cost;
            }
        }
    }

    private static string CurrentEffect(string upgradeId) => upgradeId switch
    {
        "fence_slots" => $"{Data.FenceSlots} fence slots",
        "skill_slots" => $"{Data.SkillSlots} skill slots",
        "training_ground" => $"x{Data.TrainingMultiplier:0.00} training XP",
        "pet_lab" => $"up to {Data.MaxStarsUnlocked}★, -{(int)(Data.MergeDiscount * 100)}% merge cost",
        _ => "",
    };
}
