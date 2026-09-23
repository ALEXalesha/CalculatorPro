using System.Globalization;

namespace CalcPro.Core.Services;

/// <summary>
/// Single place that converts between decimals and the strings shown on the
/// display or spliced into the expression. Invariant culture everywhere: the
/// engine speaks "1234.5", never "1 234,5".
/// </summary>
public static class DisplayFormat
{
    public const int Digits = 12;
    public const string ErrorText = "Error";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// Rounds to 12 decimal places and drops trailing zeros: 2.50 → "2.5", 3.0 → "3".
    public static string Format(decimal value)
    {
        var rounded = Math.Round(value, Digits, MidpointRounding.ToEven);
        if (rounded == 0m) return "0"; // also normalises decimal negative zero
        return rounded == decimal.Truncate(rounded)
            ? rounded.ToString("0", Inv)
            : rounded.ToString("0.############", Inv);
    }

    public static bool TryParse(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Inv, out value);

    /// Formats a value for splicing into an expression. Negatives get parentheses
    /// so "2 - (-5)" never turns into the harder to read "2 - -5".
    public static string Operand(decimal value)
    {
        var s = Format(value);
        return s.StartsWith('-') ? "(" + s + ")" : s;
    }

    /// Expression text as the buttons label it: "12 * 3 / (-4)" → "12 × 3 ÷ (−4)".
    /// The engine keeps ASCII operators; this is for the screen only. Character for
    /// character, and the tokenizer reads ×, ÷ and − too, so the text stays valid input.
    public static string Pretty(string expression) =>
        string.Create(expression.Length, expression, static (span, src) =>
        {
            for (var i = 0; i < src.Length; i++)
                span[i] = src[i] switch { '*' => '×', '/' => '÷', '-' => '−', var c => c };
        });

    /// Same as <see cref="Operand(decimal)"/> but for the raw display text.
    public static string Operand(string display) =>
        TryParse(display, out var v) ? Operand(v) : "0";
}
