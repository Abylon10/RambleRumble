using Godot;

namespace RambleRumble.UI;

/// <summary>Shared helpers for building the code-made UI.</summary>
public static class UiKit
{
    /// <summary>Places <paramref name="control"/> relative to an anchor point (0..1 on each axis) of its parent.</summary>
    public static void Place(Control control, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        control.AnchorLeft = control.AnchorRight = anchor.X;
        control.AnchorTop = control.AnchorBottom = anchor.Y;
        control.OffsetLeft = offset.X;
        control.OffsetTop = offset.Y;
        control.OffsetRight = offset.X + size.X;
        control.OffsetBottom = offset.Y + size.Y;
    }

    public static StyleBoxFlat PanelStyle(Color border, int width = 2)
    {
        var style = new StyleBoxFlat { BgColor = new Color(0.08f, 0.09f, 0.08f, 0.92f), BorderColor = border };
        style.SetBorderWidthAll(width);
        style.SetCornerRadiusAll(10);
        style.SetContentMarginAll(12);
        return style;
    }

    // --- The mockup's look: dark navy panels with thin gold frames -----------------------

    public static readonly Color Navy = new(0.06f, 0.08f, 0.12f, 0.94f);
    public static readonly Color Gold = new(0.80f, 0.64f, 0.34f);
    public static readonly Color Cream = new(0.96f, 0.90f, 0.76f);
    public static readonly Color Muted = new(0.70f, 0.72f, 0.76f);
    public static readonly Color Current = new(0.30f, 0.62f, 1.00f);

    public static StyleBoxFlat NavyStyle(Color? border = null, int width = 2, int radius = 6, float margin = 10f)
    {
        var style = new StyleBoxFlat { BgColor = Navy, BorderColor = border ?? Gold };
        style.SetBorderWidthAll(width);
        style.SetCornerRadiusAll(radius);
        style.SetContentMarginAll(margin);
        return style;
    }

    /// <summary>Navy buttons with gold frames; pages built in the mockup's style use this as their theme.</summary>
    public static Theme GameTheme()
    {
        var theme = new Theme { DefaultFontSize = 20 };
        theme.SetStylebox("normal", "Button", NavyStyle(Gold.Darkened(0.2f), margin: 8f));
        theme.SetStylebox("hover", "Button", NavyStyle(Gold, margin: 8f));
        theme.SetStylebox("pressed", "Button", NavyStyle(Cream, margin: 8f));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        var disabled = NavyStyle(new Color(0.35f, 0.35f, 0.38f), margin: 8f);
        disabled.BgColor = new Color(0.10f, 0.11f, 0.13f, 0.9f);
        theme.SetStylebox("disabled", "Button", disabled);
        theme.SetColor("font_color", "Button", Cream);
        theme.SetColor("font_hover_color", "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", new Color(0.5f, 0.5f, 0.52f));
        theme.SetStylebox("panel", "PanelContainer", NavyStyle());
        theme.SetColor("font_color", "Label", Cream);
        return theme;
    }

    /// <summary>The big gold call-to-action button ("Start Battle").</summary>
    public static Button GoldButton(string text, Vector2 minSize, int fontSize = 26)
    {
        var button = new Button { Text = text, CustomMinimumSize = minSize };
        var normal = new StyleBoxFlat { BgColor = new Color(0.86f, 0.64f, 0.26f), BorderColor = new Color(1f, 0.88f, 0.55f) };
        normal.SetBorderWidthAll(3);
        normal.SetCornerRadiusAll(6);
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color(0.94f, 0.72f, 0.32f);
        var disabled = (StyleBoxFlat)normal.Duplicate();
        disabled.BgColor = new Color(0.35f, 0.30f, 0.22f);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", normal);
        button.AddThemeStyleboxOverride("disabled", disabled);
        button.AddThemeColorOverride("font_color", new Color(0.22f, 0.13f, 0.05f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.22f, 0.13f, 0.05f));
        button.AddThemeColorOverride("font_pressed_color", new Color(0.22f, 0.13f, 0.05f));
        button.AddThemeFontSizeOverride("font_size", fontSize);
        return button;
    }

    /// <summary>A panel with a "✦ Title" header in the mockup style; returns the box to fill.</summary>
    public static VBoxContainer TitledPanel(Control parent, string title, out PanelContainer panel)
    {
        panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", NavyStyle());
        parent.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        box.AddChild(MakeLabel($"✦ {title}", 22, Cream));
        return box;
    }

    public static Texture2D? Texture(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

    /// <summary>Icon for gold, XP or a material (from the UI mockup), or null to draw a coloured chip instead.</summary>
    public static Texture2D? ItemIcon(string name) => Texture(name switch
    {
        "Gold" => "res://art/ui/icon_gold.png",
        "XP" => "res://art/ui/icon_exp.png",
        "Slime Gel" => "res://art/ui/icon_slime_gel.png",
        "Meadow Leaf" or "Forest Herb" => "res://art/ui/icon_leaf.png",
        "Oak Wood" => "res://art/ui/icon_wood.png",
        "Crystal Shard" => "res://art/ui/icon_crystal.png",
        _ => "",
    });

    /// <summary>A square reward/drop slot: the icon (or a coloured chip with an initial) and an optional count.</summary>
    public static Control ItemSlot(string name, string count = "", float side = 56f)
    {
        var box = new VBoxContainer { TooltipText = name };
        box.AddThemeConstantOverride("separation", 2);
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(side, side) };
        frame.AddThemeStyleboxOverride("panel", NavyStyle(Gold.Darkened(0.3f), 1, 4, 4f));
        box.AddChild(frame);
        if (ItemIcon(name) is { } icon)
        {
            frame.AddChild(new TextureRect
            {
                Texture = icon,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            });
        }
        else
        {
            var colour = name == "Skill Box" ? GameConfig.Item("skill_box").Color
                : System.Array.Exists(GameConfig.Materials, m => m.Name == name) ? GameConfig.Material(name).Color : Gold;
            var chip = MakeLabel(name[..1], (int)(side * 0.45f), colour);
            chip.HorizontalAlignment = HorizontalAlignment.Center;
            chip.VerticalAlignment = VerticalAlignment.Center;
            frame.AddChild(chip);
        }
        if (count != "")
        {
            var label = MakeLabel(count, 16, Cream);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(label);
        }
        return box;
    }

    public static Label MakeLabel(string text, int fontSize = 20, Color? color = null)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color ?? Colors.White);
        return label;
    }

    /// <summary>Turns <paramref name="panel"/> into a centered modal with a title row.</summary>
    public static (VBoxContainer Box, HBoxContainer Header) MakeModal(PanelContainer panel, string title, Vector2 size, bool closable = true)
    {
        panel.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.9f, 0.7f, 0.4f)));
        Place(panel, new Vector2(0.5f, 0.5f), -size / 2, size);
        panel.Visible = false;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        panel.AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        var titleLabel = MakeLabel(title, 26);
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(titleLabel);
        if (closable)
        {
            var close = new Button { Text = "  Close  " };
            close.Pressed += () => panel.Visible = false;
            header.AddChild(close);
        }
        return (box, header);
    }
}
