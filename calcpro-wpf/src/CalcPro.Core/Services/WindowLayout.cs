namespace CalcPro.Core.Services;

/// <summary>
/// Раскладка окна по его размеру. Минимум как у калькулятора Windows (320x500): там в
/// узком окне журнал открывается вместо клавиатуры, а не рядом с ней, - здесь так же.
/// Сами размеры элементов ставит MainWindow; здесь только правило, без WPF, чтобы его
/// проверяли обычные тесты.
/// </summary>
public static class WindowLayout
{
    /// <summary>Минимум окна - как у калькулятора Windows (AppMinWindowWidth/Height).</summary>
    public const double MinWidth = 320;
    public const double MinHeight = 500;

    /// <summary>Размер при первом запуске: клавиатура и история рядом.</summary>
    public const double DefaultWidth = 780;
    public const double DefaultHeight = 720;

    /// <summary>
    /// Уже этого история рядом не помещается: клавиатуре нужно около 380, истории 270,
    /// плюс поля окна и рамка Windows.
    /// </summary>
    public const double HistoryBesideFrom = 720;

    /// <summary>Уже этого окно узкое: поля меньше, кнопки памяти плотнее.</summary>
    public const double NarrowBelow = 420;

    /// <summary>Ниже этого табло, шапка и отступы ужимаются, чтобы семи рядам клавиш хватило высоты.</summary>
    public const double CompactBelow = 640;

    /// <summary>Что показывать при данном размере окна и состоянии истории.</summary>
    /// <param name="HistoryBeside">История - колонкой справа (иначе вместо клавиатуры).</param>
    /// <param name="Narrow">Узкое окно: поля меньше, кнопки памяти плотнее.</param>
    /// <param name="Compact">Низкое окно: табло и отступы меньше.</param>
    public sealed record Layout(bool HistoryBeside, bool Narrow, bool Compact, bool ShowKeypad, bool ShowHistory);

    /// <summary>
    /// Раскладка для окна <paramref name="width"/> x <paramref name="height"/> (внешний
    /// размер, с рамкой). Клавиатура видна всегда, кроме узкого окна с открытой историей:
    /// тогда история на её месте, а назад ведёт кнопка в заголовке истории.
    /// </summary>
    public static Layout For(double width, double height, bool historyOpen)
    {
        if (double.IsNaN(width) || width < 0) width = 0;
        if (double.IsNaN(height) || height < 0) height = 0;
        var beside = width >= HistoryBesideFrom;
        return new Layout(
            HistoryBeside: beside,
            Narrow: width < NarrowBelow,
            Compact: height < CompactBelow,
            ShowKeypad: beside || !historyOpen,
            ShowHistory: historyOpen);
    }

    /// <summary>
    /// История при смене ширины. Колонка истории открыта по умолчанию, и без этого правила
    /// узкое окно встречало бы человека журналом вместо клавиатуры. Поэтому при сужении
    /// открытая колонка закрывается и запоминается, а при расширении возвращается. Историю,
    /// открытую в узком окне кнопкой, правило не трогает.
    /// </summary>
    /// <returns>Открыта ли история теперь и надо ли вернуть её при расширении.</returns>
    public static (bool Open, bool ReopenWhenWide) AfterResize(bool wasBeside, bool isBeside, bool open, bool reopenWhenWide)
    {
        if (wasBeside && !isBeside && open) return (false, true);
        if (!wasBeside && isBeside) return (open || reopenWhenWide, false);
        return (open, reopenWhenWide && !open);
    }
}
