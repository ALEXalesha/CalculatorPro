using System.IO;
using System.Runtime.InteropServices;
using CalcPro.Core.Services;

namespace CalcPro.Wpf.Services;

/// <summary>
/// Размер и место окна между запусками: %APPDATA%\CalcPro\window.txt. Правило выбора
/// места - WindowPlacement в CalcPro.Core; здесь файл и рабочие области мониторов.
/// </summary>
public static class WindowPlacementService
{
    /// <summary>
    /// Файл настроек; null - не читать и не писать (так делает программа кадров, чтобы
    /// её окно не открывалось в размере человека и не портило ему сохранённый).
    /// </summary>
    public static string? FilePath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CalcPro", "window.txt");

    public static WindowPlacement.Placement? Load()
    {
        if (FilePath is null) return null;
        try { return File.Exists(FilePath) ? WindowPlacement.Parse(File.ReadAllText(FilePath)) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Запись через временный файл: убитый посреди записи процесс оставит старый файл.</summary>
    public static void Save(WindowPlacement.Placement placement)
    {
        if (FilePath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, WindowPlacement.Format(placement));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Не записалось - в следующий раз окно откроется по умолчанию, это не повод падать.
        }
    }

    /// <summary>
    /// Рабочие области всех мониторов (без панели задач) в единицах WPF, основной первым.
    /// Приложение понимает только системный DPI, поэтому пиксели делятся на него.
    /// </summary>
    public static IReadOnlyList<WindowPlacement.Area> Screens()
    {
        var list = new List<(WindowPlacement.Area area, bool primary)>();
        var scale = GetDpiForSystem() / 96.0;
        if (scale <= 0) scale = 1;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr _, ref Rect _, IntPtr _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                var w = info.Work;
                list.Add((new WindowPlacement.Area(w.Left / scale, w.Top / scale, (w.Right - w.Left) / scale, (w.Bottom - w.Top) / scale),
                    (info.Flags & 1) != 0));
            }
            return true;
        }, IntPtr.Zero);
        return list.OrderByDescending(m => m.primary).Select(m => m.area).ToList();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public Rect Monitor; public Rect Work; public uint Flags; }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref Rect rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
