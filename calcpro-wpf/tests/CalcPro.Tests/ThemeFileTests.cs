using System.Globalization;
using System.Xml.Linq;
using CalcPro.Core.Services;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace CalcPro.Tests;

/// <summary>
/// Темы оформления: те же пять, что в Paint Pro. Проверяются не «красиво ли», а правила,
/// нарушение которых ломает окно:
///   1. у всех тем один и тот же набор ключей - иначе на смене какая-то кисть окажется
///      пустой, и WPF нарисует кнопку прозрачной, а текст невидимым;
///   2. текст читается на фоне своей темы и на своих кнопках;
///   3. разметка обращается к ключам темы через DynamicResource: StaticResource
///      разрешается один раз и подмену словаря не заметит;
///   4. мусор в файле настроек даёт исходную тему, а не падение.
/// Файлы XAML читаются как XML, без WPF, поэтому проверки идут и в CI на Linux.
/// </summary>
public class ThemeFileTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static string WpfDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CalcPro.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "CalcPro.Wpf");
    }

    private static XDocument Theme(string id) => XDocument.Load(Path.Combine(WpfDir(), "Resources", "Themes", id + ".xaml"));

    private static string[] Keys(XDocument doc) =>
        doc.Root!.Elements().Select(e => (string?)e.Attribute(X + "Key")).Where(k => k is not null).Select(k => k!).OrderBy(k => k).ToArray();

    private static (byte a, byte r, byte g, byte b) SolidColor(XDocument doc, string key)
    {
        var el = doc.Root!.Elements(P + "SolidColorBrush").Single(e => (string?)e.Attribute(X + "Key") == key);
        var hex = ((string)el.Attribute("Color")!).TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        var v = uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return ((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    private static double[] Over((byte a, byte r, byte g, byte b) fg, double[] bg)
    {
        var alpha = fg.a / 255.0;
        return new[] { fg.r * alpha + bg[0] * (1 - alpha), fg.g * alpha + bg[1] * (1 - alpha), fg.b * alpha + bg[2] * (1 - alpha) };
    }

    private static double[] Rgb((byte a, byte r, byte g, byte b) c) => new double[] { c.r, c.g, c.b };

    private static double Luminance(double[] c)
    {
        double Ch(double v) { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
        return 0.2126 * Ch(c[0]) + 0.7152 * Ch(c[1]) + 0.0722 * Ch(c[2]);
    }

    private static double Contrast(double[] a, double[] b)
    {
        var (x, y) = (Luminance(a), Luminance(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    [Fact]
    public void The_five_themes_are_those_of_Paint_Pro_in_the_same_order()
    {
        Assert.Equal(new[] { "Glass", "Formal", "Light", "Night", "Warm" }, ThemeCatalog.All.Select(t => t.Id));
        Assert.Equal(new[] { "Стеклянная", "Строгая", "Светлая", "Ночная", "Тёплая" }, ThemeCatalog.All.Select(t => t.Name));
        Assert.All(ThemeCatalog.All, t => Assert.False(string.IsNullOrWhiteSpace(t.Note)));
    }

    [Fact]
    public void Every_theme_has_its_own_file_and_exactly_the_same_keys()
    {
        var reference = Keys(Theme(ThemeCatalog.DefaultId));
        Assert.NotEmpty(reference);
        foreach (var t in ThemeCatalog.All) Assert.Equal(reference, Keys(Theme(t.Id)));
    }

    [Fact]
    public void Text_is_readable_on_the_background_and_on_the_keys_of_its_own_theme()
    {
        foreach (var t in ThemeCatalog.All)
        {
            var doc = Theme(t.Id);
            var baseRgb = Rgb(SolidColor(doc, "AppBase"));
            var text = Rgb(SolidColor(doc, "TextBrush"));
            Assert.True(Contrast(text, baseRgb) >= 7, $"{t.Id}: текст на фоне");
            // Кнопки цифр - стекло поверх фона; текст на нём и на самом ярком стекле.
            Assert.True(Contrast(text, Over(SolidColor(doc, "GlassBg"), baseRgb)) >= 4.5, $"{t.Id}: текст на кнопке");
            Assert.True(Contrast(text, Over(SolidColor(doc, "GlassBgStrong"), baseRgb)) >= 4.5, $"{t.Id}: текст на кнопке под мышью");
            Assert.True(Contrast(Rgb(SolidColor(doc, "DangerText")), Over(SolidColor(doc, "DangerTint"), baseRgb)) >= 4.5,
                $"{t.Id}: AC и C на красном");
            Assert.True(Contrast(Rgb(SolidColor(doc, "TextDimBrush")), baseRgb) >= 4.5, $"{t.Id}: приглушённый текст");
        }
    }

    [Fact]
    public void Markup_reaches_theme_keys_only_dynamically()
    {
        var themeKeys = Keys(Theme(ThemeCatalog.DefaultId));
        var files = Directory.GetFiles(WpfDir(), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "Themes" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToArray();
        Assert.Contains(files, f => f.EndsWith("MainWindow.xaml", StringComparison.Ordinal));
        var bad = (from f in files
                   from line in File.ReadAllLines(f).Select((text, i) => (text, no: i + 1))
                   from key in themeKeys
                   where line.text.Contains("{StaticResource " + key + "}", StringComparison.Ordinal)
                   select $"{Path.GetFileName(f)}:{line.no} {key}").ToArray();
        Assert.True(bad.Length == 0, "StaticResource к ключу темы - смена темы его не перекрасит: " + string.Join(", ", bad));
    }

    [Fact]
    public void The_application_starts_with_a_theme_dictionary_merged()
    {
        var app = File.ReadAllText(Path.Combine(WpfDir(), "App.xaml"));
        Assert.Contains("Resources/Themes/" + ThemeCatalog.DefaultId + ".xaml", app);
        Assert.DoesNotContain("Brushes.xaml", app);
    }

    [Property(MaxTest = 500)]
    public Property Any_saved_text_gives_a_known_theme_and_known_ones_survive() =>
        Prop.ForAll(Arb.From(Gen.OneOf(Gen.Elements(ThemeCatalog.All.Select(t => t.Id).ToArray()), Arb.Default.String().Generator)),
            saved =>
            {
                var got = ThemeCatalog.Normalize(saved);
                var known = ThemeCatalog.All.Any(t => t.Id == got);
                var kept = !ThemeCatalog.All.Any(t => t.Id == saved?.Trim()) || got == saved!.Trim();
                return known && kept;
            });

    [Theory]
    [InlineData(null, "Glass")]
    [InlineData("", "Glass")]
    [InlineData("light", "Glass")]
    [InlineData("Light\r\n", "Light")]
    [InlineData("  Warm ", "Warm")]
    [InlineData("Sepia", "Glass")]
    public void Settings_file_contents_are_read_forgivingly(string? saved, string expected) =>
        Assert.Equal(expected, ThemeCatalog.Normalize(saved));
}
