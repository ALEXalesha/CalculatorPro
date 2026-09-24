using CalcPro.Core.Services;
using FsCheck;
using FsCheck.Xunit;
using Xunit;
using static CalcPro.Core.Services.WindowPlacement;

namespace CalcPro.Tests;

/// <summary>
/// Размер и место окна между запусками (1.5.0). Главное - не «запомнило ли», а «что бы
/// ни лежало в файле, окно откроется там, где его видно и можно взять за заголовок».
/// Те же случаи проверяет test/window-state.test.js у Electron-калькуляторов.
/// </summary>
public class WindowPlacementTests
{
    private static readonly Area FullHd = new(0, 0, 1920, 1040);
    private static readonly Area Right = new(1920, 0, 2560, 1400);

    private static Placement R(Placement? saved, params Area[] screens) =>
        Restore(saved, screens, WindowLayout.DefaultWidth, WindowLayout.DefaultHeight, WindowLayout.MinWidth, WindowLayout.MinHeight);

    [Fact]
    public void Nothing_saved_gives_the_default_size_centred() =>
        Assert.Equal(new Placement(null, null, 780, 720, false), R(null, FullHd));

    [Fact]
    public void A_window_saved_on_screen_opens_exactly_where_it_was()
    {
        var saved = new Placement(100, 200, 400, 600, false);
        Assert.Equal(saved, R(saved, FullHd));
    }

    [Fact]
    public void The_second_monitor_unplugged_centres_the_window_and_keeps_its_size()
    {
        var saved = new Placement(2500, 300, 500, 700, false);
        Assert.Equal(saved, R(saved, FullHd, Right));
        Assert.Equal(new Placement(null, null, 500, 700, false), R(saved, FullHd));
    }

    [Fact]
    public void A_window_partly_past_the_edge_is_pulled_back_whole()
    {
        Assert.Equal(new Placement(1520, 340, 400, 700, false), R(new Placement(1800, 900, 400, 700, false), FullHd));
    }

    [Fact]
    public void A_window_with_its_title_bar_above_the_screen_is_not_trusted() =>
        Assert.Null(R(new Placement(100, -700, 400, 700, false), FullHd).Left);

    [Fact]
    public void On_a_screen_lower_than_the_window_the_title_bar_stays_on_it()
    {
        // Та же ошибка, что fast-check нашёл в window-state.js: прижатое низом окно
        // уводило заголовок выше экрана.
        var low = new Area(0, 0, 1024, 460);
        var got = R(new Placement(0, 300, 320, 500, false), low);
        Assert.Equal(0, got.Top);
    }

    [Fact]
    public void Sizes_are_raised_to_the_minimum_and_cut_to_the_screen()
    {
        Assert.Equal(new Placement(null, null, 320, 500, false), R(new Placement(null, null, 10, 10, false), FullHd));
        Assert.Equal(new Placement(0, 0, 1920, 1040, true), R(new Placement(0, 0, 9000, 9000, true), FullHd));
    }

    // --- Свойства на случайных экранах и случайных сохранённых значениях ---

    private static Gen<Area[]> ScreensGen =>
        from list in Gen.NonEmptyListOf(
            from y in Gen.Choose(-3000, 3000)
            from w in Gen.Choose(640, 5000)
            from h in Gen.Choose(480, 3000)
            select (y, w, h))
        from x0 in Gen.Choose(-5000, 5000)
        select Lay(list.Take(4).ToArray(), x0);

    // Экраны в ряд без перекрытий, как настоящие мониторы.
    private static Area[] Lay((int y, int w, int h)[] list, int x0)
    {
        var x = x0;
        return list.Select(s => { var a = new Area(x, s.y, s.w, s.h); x += s.w; return a; }).ToArray();
    }

    private static Gen<Placement> SavedGen =>
        from left in Gen.OneOf(Gen.Constant<double?>(null), Gen.Choose(-20000, 20000).Select(v => (double?)v), Gen.Constant<double?>(double.NaN))
        from top in Gen.OneOf(Gen.Constant<double?>(null), Gen.Choose(-20000, 20000).Select(v => (double?)v), Gen.Constant<double?>(double.PositiveInfinity))
        from w in Gen.OneOf(Gen.Choose(-100, 10000).Select(v => (double)v), Gen.Constant(double.NaN))
        from h in Gen.OneOf(Gen.Choose(-100, 10000).Select(v => (double)v), Gen.Constant(double.NegativeInfinity))
        from max in Arb.Default.Bool().Generator
        select new Placement(left, top, w, h, max);

    [Property(MaxTest = 3000)]
    public Property Whatever_was_saved_the_window_is_sane_and_its_title_bar_on_a_screen() =>
        Prop.ForAll(SavedGen.ToArbitrary(), ScreensGen.ToArbitrary(), (saved, screens) =>
        {
            var p = R(saved, screens);
            var sizeOk = p.Width >= WindowLayout.MinWidth && p.Height >= WindowLayout.MinHeight
                         && double.IsFinite(p.Width) && double.IsFinite(p.Height);
            if (p.Left is null || p.Top is null) return sizeOk && p.Left is null && p.Top is null;
            var titleOnScreen = screens.Any(a => p.Left >= a.X && p.Top >= a.Y && p.Left < a.X + a.Width && p.Top + GripHeight <= a.Y + a.Height);
            return sizeOk && titleOnScreen;
        });

    [Property(MaxTest = 3000)]
    public Property Restoring_what_was_restored_changes_nothing() =>
        Prop.ForAll(SavedGen.ToArbitrary(), ScreensGen.ToArbitrary(), (saved, screens) =>
        {
            var once = R(saved, screens);
            return once.Left is null || R(once, screens) == once;
        });

    // --- Файл ---

    [Property(MaxTest = 1000)]
    public Property Format_then_parse_gives_back_the_same_placement() =>
        Prop.ForAll(SavedGen.Where(p => double.IsFinite(p.Width) && double.IsFinite(p.Height)
                                        && (p.Left is null || double.IsFinite(p.Left.Value))
                                        && (p.Top is null || double.IsFinite(p.Top.Value))).ToArbitrary(),
            p => Parse(Format(p)) == p);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("100;200;300")]
    [InlineData("100;200;300;400;maybe")]
    [InlineData("a;b;c;d;max")]
    [InlineData("100;200;;400;normal")]
    [InlineData("1e999;0;300;400;normal")]
    public void Garbage_in_the_file_reads_as_nothing(string? text) => Assert.Null(Parse(text));

    [Fact]
    public void A_file_written_in_another_culture_still_reads()
    {
        var saved = new Placement(10.5, -20.25, 400, 600, true);
        var ru = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
        var before = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = ru; // запятая как разделитель дробной части
            Assert.Equal(saved, Parse(Format(saved)));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = before; }
    }
}
