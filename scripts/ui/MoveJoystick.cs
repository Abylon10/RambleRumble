using Godot;

namespace RambleRumble.UI;

/// <summary>
/// Multitouch virtual joystick. Reads raw touch events in _Input so it keeps
/// working while another finger holds an action button. (Not named
/// VirtualJoystick: Godot 4.7 has a built-in class with that name.)
/// </summary>
public partial class MoveJoystick : Control
{
    private const float KnobRadius = 34f;

    /// <summary>Stick direction, length 0..1.</summary>
    public Vector2 Vector { get; private set; }

    private int _touchIndex = -1;
    private Vector2 _knob;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Input(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventScreenTouch { Pressed: true } touch when _touchIndex == -1 && GetGlobalRect().HasPoint(touch.Position):
                _touchIndex = touch.Index;
                UpdateKnob(touch.Position);
                break;
            case InputEventScreenTouch { Pressed: false } touch when touch.Index == _touchIndex:
                _touchIndex = -1;
                Vector = Vector2.Zero;
                _knob = Vector2.Zero;
                QueueRedraw();
                break;
            case InputEventScreenDrag drag when drag.Index == _touchIndex:
                UpdateKnob(drag.Position);
                break;
        }
    }

    private void UpdateKnob(Vector2 screenPos)
    {
        float radius = Size.X * 0.5f;
        _knob = (screenPos - GetGlobalRect().GetCenter()).LimitLength(radius);
        Vector = _knob / radius;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        DrawCircle(center, Size.X * 0.5f, new Color(0, 0, 0, 0.30f));
        DrawArc(center, Size.X * 0.5f, 0f, Mathf.Tau, 48, new Color(1, 1, 1, 0.35f), 3f);
        DrawCircle(center + _knob, KnobRadius, new Color(1, 1, 1, 0.55f));
    }
}
