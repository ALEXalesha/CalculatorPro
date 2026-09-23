namespace CalcPro.Core.Services;

/// <summary>
/// Темы оформления: те же пять, что в Paint Pro («Вид → Тема»), в том же порядке и под
/// теми же именами. Здесь только список и правило выбора, без WPF: подменой словарей
/// занимается ThemeService в CalcPro.Wpf, а это можно проверить обычными тестами.
/// </summary>
public static class ThemeCatalog
{
    public sealed record ThemeInfo(string Id, string Name, string Note);

    /// <summary>Порядок здесь - это порядок в меню.</summary>
    public static readonly IReadOnlyList<ThemeInfo> All = new[]
    {
        new ThemeInfo("Glass",  "Стеклянная", "Цветной градиент и стекло"),
        new ThemeInfo("Formal", "Строгая",    "Ровный тёмный фон, приглушённый синий"),
        new ThemeInfo("Light",  "Светлая",    "Тёмный текст на светлом"),
        new ThemeInfo("Night",  "Ночная",     "Почти чёрный фон для тёмной комнаты"),
        new ThemeInfo("Warm",   "Тёплая",     "Охра и кофе вместо синевы"),
    };

    public const string DefaultId = "Glass";

    /// <summary>
    /// Известная тема или исходная. Имя приходит из файла настроек, а его могли поправить
    /// руками или оставить от другой версии: окно без оформления - не окно.
    /// </summary>
    public static string Normalize(string? id)
    {
        var trimmed = id?.Trim();
        return All.Any(t => t.Id == trimmed) ? trimmed! : DefaultId;
    }
}
