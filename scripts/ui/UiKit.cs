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
