using System.Linq;
using Godot;

namespace RambleRumble.Tests;

/// <summary>
/// Measures frame rate on each kind of page (needs a window, not --headless).
/// Run: godot --path . res://tests/PerfProbe.tscn -- --test
/// </summary>
public partial class PerfProbe : TestScene
{
    public override async void _Ready()
    {
        Data.ResetProgress();
        Data.ChooseStarter("wind_slash");
        Data.DebugUnlockWorld(20);
        Data.DebugClearStages();
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);

        var village = await LoadGame();
        village.Hud.StarterPanel.Visible = false;
        await Measure("Village");
        await Go<ChapterMapPage>(() => Game.Instance.ShowChapterMap(1));
        await Measure("Chapter 1 map");
        foreach (var id in new[] { "1-1", "1-8", "2-5", "2-10" })
        {
            var stage = await Go<StagePage>(() => Game.Instance.StartStage(StageCatalog.Get(id)));
            stage.Player.InSafeZone = true; // keep the probe alive
            stage.Player.GlobalPosition = stage.Map.RoadPoint(0.5f);
            await Measure($"Stage {id}");
        }
        GetTree().Quit();
    }

    private async System.Threading.Tasks.Task Measure(string label)
    {
        await Wait(1.0);
        int frames = 0;
        double start = Time.GetTicksMsec(), last = start, worst = 0;
        while (Time.GetTicksMsec() - start < 2500)
        {
            await ProcessFrame();
            double now = Time.GetTicksMsec();
            worst = System.Math.Max(worst, now - last);
            last = now;
            frames++;
        }
        double seconds = (Time.GetTicksMsec() - start) / 1000.0;
        GD.Print($"{label,-16} fps {frames / seconds,6:0.0}  worst {worst,4:0}ms  draw calls {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),5}  " +
                 $"primitives {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),7}  nodes {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}");
    }
}
