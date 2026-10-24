using System.Collections.Generic;
using System.Diagnostics;
namespace RambleRumble;
public static class TmpProf
{
    public static readonly Dictionary<string, Stopwatch> W = new();
    public static Stopwatch S(string n) { if (!W.TryGetValue(n, out var s)) W[n] = s = new Stopwatch(); return s; }
}
