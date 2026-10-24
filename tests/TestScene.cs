using Godot;

namespace RambleRumble.Tests;

/// <summary>
/// Base for headless test scenes: prints ok/FAIL lines and quits with the
/// failure count as the exit code. Run with "-- --test" so the real save is untouched.
/// </summary>
public abstract partial class TestScene : Node
{
    private int _failures;

    protected static PlayerData Data => PlayerData.Instance;

    protected void Check(bool ok, string what)
    {
        GD.Print((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok)
            _failures++;
    }

    protected void Finish()
    {
        Data.ResetProgress();
        GD.Print(_failures == 0 ? "RESULT: PASS" : $"RESULT: {_failures} FAILURES");
        GetTree().Quit(_failures);
    }

    protected SignalAwaiter PhysicsFrame() => ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
}
