using System;
using Godot;

namespace RambleRumble.UI;

/// <summary>Bottom-of-screen dialogue box: speaker, text, and up to one action button plus Close.</summary>
public partial class DialoguePanel : PanelContainer
{
    private Label _speaker = null!;
    private Label _text = null!;
    private Button _action = null!;
    private Action? _onAction;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", UiKit.PanelStyle(new Color(0.9f, 0.7f, 0.4f)));
        UiKit.Place(this, new Vector2(0.5f, 1f), new Vector2(-420, -250), new Vector2(840, 230));
        Visible = false;

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        AddChild(box);
        _speaker = UiKit.MakeLabel("", 22, new Color(0.95f, 0.8f, 0.5f));
        box.AddChild(_speaker);
        _text = UiKit.MakeLabel("", 18);
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _text.SizeFlagsVertical = SizeFlags.ExpandFill;
        box.AddChild(_text);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 10);
        box.AddChild(buttons);
        _action = new Button { CustomMinimumSize = new Vector2(180, 50) };
        _action.Pressed += () => _onAction?.Invoke();
        buttons.AddChild(_action);
        var close = new Button { Text = "Close", CustomMinimumSize = new Vector2(120, 50) };
        close.Pressed += () => Visible = false;
        buttons.AddChild(close);
    }

    /// <param name="actionText">Label for the action button; null hides it.</param>
    public void Say(string speaker, string text, string? actionText = null, Action? onAction = null)
    {
        _speaker.Text = speaker;
        _text.Text = text;
        _action.Visible = actionText != null;
        _action.Text = actionText ?? "";
        _onAction = onAction;
        Visible = true;
    }
}
