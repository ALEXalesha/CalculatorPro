using System.Numerics;
using System.Text.Json;
using CalcPro.Core.Services;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace CalcPro.Tests;

/// <summary>
/// Режим «Программист» (1.6.0). Примеры - из test-vectors/programmer.json: тот же файл
/// проверяет и Calc Pro Glass, так что при одних нажатиях обе версии показывают одно.
/// Ожидаемое в файле посчитано отдельно (Python, маска 2^64), а не этим кодом.
/// </summary>
public class ProgrammerCalculatorTests
{
    public sealed record Vector(string Name, string[] Keys, string? Hex, string? Dec, string? Oct, string? Bin,
        string? Display, string? Expression, string? Error);

    private static string VectorsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CalcPro.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.Parent!.FullName, "test-vectors", "programmer.json");
    }

    public static IEnumerable<object[]> Vectors()
    {
        var json = File.ReadAllText(VectorsPath());
        var list = JsonSerializer.Deserialize<Vector[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        return list.Select(v => new object[] { v.Name, v });
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Shared_example(string name, Vector v)
    {
        var calc = new ProgrammerCalculator();
        foreach (var k in v.Keys) calc.Press(k);
        Assert.True(v.Error == calc.Error, $"{name}: ошибка «{calc.Error}» вместо «{v.Error}»");
        if (v.Dec is not null)
        {
            Assert.Equal(v.Hex, ProgrammerCalculator.Group(ProgrammerCalculator.Format(calc.Value, NumberBase.Hex), NumberBase.Hex));
            Assert.Equal(v.Dec, ProgrammerCalculator.Group(ProgrammerCalculator.Format(calc.Value, NumberBase.Dec), NumberBase.Dec));
            Assert.Equal(v.Oct, ProgrammerCalculator.Group(ProgrammerCalculator.Format(calc.Value, NumberBase.Oct), NumberBase.Oct));
            Assert.Equal(v.Bin, ProgrammerCalculator.Group(ProgrammerCalculator.Format(calc.Value, NumberBase.Bin), NumberBase.Bin));
        }
        if (v.Display is not null) Assert.Equal(v.Display, calc.Display);
        if (v.Expression is not null) Assert.Equal(v.Expression, calc.Expression);
    }

    [Fact]
    public void There_are_enough_shared_examples() => Assert.True(Vectors().Count() >= 40);

    // ───────── свойства на случайных числах ─────────

    private static readonly NumberBase[] AllBases = { NumberBase.Bin, NumberBase.Oct, NumberBase.Dec, NumberBase.Hex };

    [Property(MaxTest = 3000)]
    public bool Every_number_reads_back_from_every_base(long v) =>
        AllBases.All(b => ProgrammerCalculator.TryParse(ProgrammerCalculator.Format(v, b), b, out var back) && back == v
                          && ProgrammerCalculator.TryParse(ProgrammerCalculator.Group(ProgrammerCalculator.Format(v, b), b), b, out var grouped)
                          && grouped == v);

    [Property(MaxTest = 2000)]
    public bool Typing_a_number_digit_by_digit_gives_that_number(long v)
    {
        if (v < 0) return true; // отрицательные в DEC набираются через ±, это отдельный пример
        foreach (var b in AllBases)
        {
            var calc = new ProgrammerCalculator();
            calc.SetBase(b);
            foreach (var c in ProgrammerCalculator.Format(v, b)) calc.Press(c.ToString());
            if (calc.Value != v) return false;
        }
        return true;
    }

    /// <summary>Эталон - BigInteger, обрезанный до 64 бит со знаком.</summary>
    private static long Wrap(BigInteger x)
    {
        var m = BigInteger.One << 64;
        var u = ((x % m) + m) % m;
        return u >= BigInteger.One << 63 ? (long)(u - m) : (long)u;
    }

    [Property(MaxTest = 3000)]
    public bool Arithmetic_wraps_like_64_bit_integers(long a, long b)
    {
        bool Check(string op, long expected) => ProgrammerCalculator.TryApply(op, a, b, out var r) && r == expected;
        return Check("+", Wrap((BigInteger)a + b))
            && Check("−", Wrap((BigInteger)a - b))
            && Check("×", Wrap((BigInteger)a * b))
            && Check("AND", a & b) && Check("OR", a | b) && Check("XOR", a ^ b)
            && (b == 0
                ? !ProgrammerCalculator.TryApply("÷", a, b, out _) && !ProgrammerCalculator.TryApply("Mod", a, b, out _)
                : Check("÷", Wrap(BigInteger.Divide(a, b))) && Check("Mod", Wrap(BigInteger.Remainder(a, b))));
    }

    [Property(MaxTest = 2000)]
    public bool Shifts_use_the_low_six_bits_of_the_count(long a, int n)
    {
        var s = n & 63;
        return ProgrammerCalculator.TryApply("<<", a, n, out var l) && l == a << s
            && ProgrammerCalculator.TryApply(">>", a, n, out var r) && r == a >> s;
    }

    [Property(MaxTest = 1000)]
    public bool Switching_bases_never_changes_the_value(long v, byte pick)
    {
        var calc = new ProgrammerCalculator();
        calc.SetBase(NumberBase.Hex);
        foreach (var c in ProgrammerCalculator.Format(v, NumberBase.Hex)) calc.Press(c.ToString());
        calc.SetBase(AllBases[pick % 4]);
        calc.SetBase(AllBases[(pick / 4) % 4]);
        return calc.Value == v;
    }

    [Theory]
    [InlineData("12", NumberBase.Bin)]
    [InlineData("8", NumberBase.Oct)]
    [InlineData("G", NumberBase.Hex)]
    [InlineData("", NumberBase.Dec)]
    [InlineData("-", NumberBase.Dec)]
    [InlineData("-5", NumberBase.Hex)]
    [InlineData("9223372036854775808", NumberBase.Dec)]
    [InlineData("10000000000000000", NumberBase.Hex)]
    public void Not_a_number_in_that_base(string text, NumberBase b) =>
        Assert.False(ProgrammerCalculator.TryParse(text, b, out _));

    [Fact]
    public void The_smallest_number_reads_in_decimal() =>
        Assert.True(ProgrammerCalculator.TryParse("-9223372036854775808", NumberBase.Dec, out var v) && v == long.MinValue);
}
