using System.Globalization;
using System.Xml.Linq;
using CalcPro.Core.Services;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace CalcPro.Tests;

/// <summary>
/// Маленькое окно (1.4.0): минимум как у калькулятора Windows, 320x500. Проверяется
/// правило WindowLayout и то, что разметка окна с ним согласна. Как всё уложилось в
/// настоящем окне минимального размера, проверяет программа кадров
/// (tools/CalcPro.Screenshots): она падает, если какая-то кнопка вышла за окно.
/// </summary>
public class WindowLayoutTests
{
    private static XElement MainWindow()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CalcPro.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return XDocument.Load(Path.Combine(dir!.FullName, "src", "CalcPro.Wpf", "MainWindow.xaml")).Root!;
    }

    private static double Attr(XElement el, string name) =>
        double.Parse((string)el.Attribute(name)!, CultureInfo.InvariantCulture);

    [Fact]
    public void The_window_minimum_is_that_of_the_Windows_calculator_and_the_markup_agrees()
    {
        Assert.Equal((320d, 500d), (WindowLayout.MinWidth, WindowLayout.MinHeight));
        var window = MainWindow();
        Assert.Equal(WindowLayout.MinWidth, Attr(window, "MinWidth"));
        Assert.Equal(WindowLayout.MinHeight, Attr(window, "MinHeight"));
        Assert.Equal(WindowLayout.DefaultWidth, Attr(window, "Width"));
        Assert.Equal(WindowLayout.DefaultHeight, Attr(window, "Height"));
    }

    [Fact]
    public void No_column_of_the_markup_demands_more_width_than_the_minimum_window()
    {
        // Была MinWidth="380" у колонки клавиатуры: окно уже 420 не сжималось.
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var widths = MainWindow().Descendants(p + "ColumnDefinition")
            .Select(c => (string?)c.Attribute("MinWidth")).Where(w => w is not null).ToArray();
        Assert.Empty(widths);
    }

    [Fact]
    public void The_default_window_has_the_full_layout_with_history_beside()
    {
        var l = WindowLayout.For(WindowLayout.DefaultWidth, WindowLayout.DefaultHeight, historyOpen: true);
        Assert.Equal(new WindowLayout.Layout(HistoryBeside: true, Narrow: false, ShowTitle: true, Compact: false,
            ShowKeypad: true, ShowHistory: true), l);
    }

    [Fact]
    public void The_minimum_window_is_narrow_and_compact_and_history_takes_the_place_of_the_keypad()
    {
        var closed = WindowLayout.For(WindowLayout.MinWidth, WindowLayout.MinHeight, historyOpen: false);
        Assert.True(closed is { HistoryBeside: false, Narrow: true, ShowTitle: false, Compact: true, ShowKeypad: true, ShowHistory: false });
        var open = WindowLayout.For(WindowLayout.MinWidth, WindowLayout.MinHeight, historyOpen: true);
        Assert.True(open is { ShowKeypad: false, ShowHistory: true });
    }

    [Fact]
    public void The_thresholds_lie_between_the_minimum_and_the_default_size()
    {
        Assert.InRange(WindowLayout.HistoryBesideFrom, WindowLayout.MinWidth + 1, WindowLayout.DefaultWidth);
        Assert.InRange(WindowLayout.TitleFrom, WindowLayout.MinWidth + 1, WindowLayout.HistoryBesideFrom);
        Assert.InRange(WindowLayout.CompactBelow, WindowLayout.MinHeight + 1, WindowLayout.DefaultHeight);
    }

    [Property(MaxTest = 1000)]
    public Property Something_is_always_shown_and_history_is_shown_exactly_when_open() =>
        Prop.ForAll(Arb.From(Gen.Choose(0, 4000)), Arb.From(Gen.Choose(0, 3000)), Arb.Default.Bool(), (w, h, open) =>
        {
            var l = WindowLayout.For(w, h, open);
            return (l.ShowKeypad || l.ShowHistory)
                && l.ShowHistory == open
                // В узком окне клавиатура и история никогда не делят место.
                && (l.HistoryBeside || !(l.ShowKeypad && l.ShowHistory))
                && l.ShowTitle == !l.Narrow;
        });

    [Theory]
    [InlineData(double.NaN, double.NaN)]
    [InlineData(-5, -5)]
    public void Nonsense_sizes_give_the_smallest_layout_rather_than_an_exception(double w, double h)
    {
        var l = WindowLayout.For(w, h, historyOpen: false);
        Assert.True(l is { HistoryBeside: false, Compact: true, ShowKeypad: true });
    }

    // --- История при смене ширины ---

    [Fact]
    public void Narrowing_closes_the_history_column_and_widening_brings_it_back()
    {
        var (open, reopen) = WindowLayout.AfterResize(wasBeside: true, isBeside: false, open: true, reopenWhenWide: false);
        Assert.Equal((false, true), (open, reopen));
        (open, reopen) = WindowLayout.AfterResize(wasBeside: false, isBeside: true, open, reopen);
        Assert.Equal((true, false), (open, reopen));
    }

    [Fact]
    public void A_history_closed_by_hand_stays_closed_after_widening()
    {
        Assert.Equal((false, false), WindowLayout.AfterResize(wasBeside: false, isBeside: true, open: false, reopenWhenWide: false));
        Assert.Equal((false, false), WindowLayout.AfterResize(wasBeside: true, isBeside: false, open: false, reopenWhenWide: false));
    }

    [Fact]
    public void A_history_opened_by_hand_in_a_narrow_window_is_not_closed_by_the_rule()
    {
        Assert.Equal((true, false), WindowLayout.AfterResize(wasBeside: false, isBeside: false, open: true, reopenWhenWide: true));
        Assert.Equal((true, false), WindowLayout.AfterResize(wasBeside: false, isBeside: true, open: true, reopenWhenWide: false));
    }

    [Property(MaxTest = 500)]
    public Property Any_sequence_of_widths_never_leaves_the_history_open_in_a_window_just_narrowed() =>
        Prop.ForAll(Arb.From(Gen.ListOf(Gen.Elements(true, false))), steps =>
        {
            // true - окно расширили до «истории рядом», false - сузили. Кнопок никто не жмёт.
            bool beside = true, open = true, reopen = false;
            foreach (var next in steps)
            {
                var wasBeside = beside;
                (open, reopen) = WindowLayout.AfterResize(wasBeside, next, open, reopen);
                beside = next;
                if (wasBeside && !beside && open) return false;
                // Без нажатий история открыта ровно тогда, когда окно широкое: как было в начале.
                if (open != beside) return false;
            }
            return true;
        });
}
