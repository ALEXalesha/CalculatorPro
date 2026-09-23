using System.IO;
using System.Windows;
using CalcPro.Core.Services;

namespace CalcPro.Wpf.Services;

/// <summary>
/// Смена темы: подмена словаря Resources/Themes/*.xaml среди ресурсов приложения. Всё
/// оформление обращается к его ключам через DynamicResource, поэтому окно
/// перекрашивается сразу, без перезапуска. Выбор хранится в %APPDATA%\CalcPro\theme.txt.
/// Так же устроено в Paint Pro (Services/ThemeService.cs).
/// </summary>
public static class ThemeService
{
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CalcPro", "theme.txt");

    /// <summary>Тема, стоящая сейчас.</summary>
    public static string Current { get; private set; } = ThemeCatalog.DefaultId;

    /// <summary>Поставить тему на экран. Ничего не записывает: это делает <see cref="Save"/>.</summary>
    public static string Apply(string? id)
    {
        var theme = ThemeCatalog.Normalize(id);
        var app = Application.Current;
        if (app is not null)
        {
            var merged = app.Resources.MergedDictionaries;
            var fresh = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/CalcPro;component/Resources/Themes/{theme}.xaml")
            };
            // Словарь темы ищется по пути, а не по индексу: перед ним стоят словари WPF-UI.
            var index = -1;
            for (var i = 0; i < merged.Count; i++)
                if (merged[i].Source?.OriginalString.Contains("/Themes/", StringComparison.Ordinal) == true
                    && merged[i].Source.OriginalString.Contains("Resources", StringComparison.Ordinal))
                    index = i;
            if (index >= 0) merged[index] = fresh;
            else merged.Add(fresh);
        }
        Current = theme;
        return theme;
    }

    /// <summary>Запомнить выбор. Не вышло записать - не беда: тема вернётся к исходной.</summary>
    public static void Save(string id)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, ThemeCatalog.Normalize(id));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Прочитать запомненный выбор. Нет файла или мусор внутри - исходная тема.</summary>
    public static string Load()
    {
        try
        {
            return File.Exists(SettingsPath) ? ThemeCatalog.Normalize(File.ReadAllText(SettingsPath)) : ThemeCatalog.DefaultId;
        }
        catch (IOException) { return ThemeCatalog.DefaultId; }
        catch (UnauthorizedAccessException) { return ThemeCatalog.DefaultId; }
    }

    /// <summary>Поставить запомненную тему при запуске, до первого окна.</summary>
    public static string ApplySaved() => Apply(Load());
}
