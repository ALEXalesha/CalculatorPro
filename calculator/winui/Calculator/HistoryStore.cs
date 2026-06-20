using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CalcPro.Calculator;

/// <summary>
/// JSON-on-disk replacement for the JS localStorage calls. Stored at
/// %LOCALAPPDATA%\CalcPro\state.json so unpackaged exe works the same as packaged.
/// </summary>
public static class HistoryStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CalcPro");
    private static readonly string FilePath = Path.Combine(Dir, "state.json");

    private record Persisted(List<HistoryEntry> History, double Memory);

    public static (List<HistoryEntry> history, double memory) Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return (new(), 0);
            var json = File.ReadAllText(FilePath);
            var p = JsonSerializer.Deserialize<Persisted>(json);
            return p is null ? (new(), 0) : (p.History ?? new(), p.Memory);
        }
        catch { return (new(), 0); }
    }

    public static void Save(List<HistoryEntry> history, double memory)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            // Cap at 50 entries like the JS version
            var trimmed = history.Count > 50 ? history.GetRange(0, 50) : history;
            var json = JsonSerializer.Serialize(new Persisted(trimmed, memory));
            File.WriteAllText(FilePath, json);
        }
        catch { /* swallow — persistence is best-effort */ }
    }
}
