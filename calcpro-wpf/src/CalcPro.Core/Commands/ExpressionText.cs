using CalcPro.Core.Models;
using CalcPro.Core.Services;

namespace CalcPro.Core.Commands;

/// <summary>
/// Helpers for reading the half-built expression string that commands append to.
/// The commands keep it in a canonical shape ("2 + (3 * 4) %"), which is what
/// makes these small scanners sufficient.
/// </summary>
internal static class ExpressionText
{
    /// Beyond this many unclosed '(' further '(' presses are ignored.
    public const int MaxOpenParens = 32;

    /// Longest number the user can type into the display.
    public const int MaxInputLength = 24;

    private const string BinaryOperators = "+-*/^";

    public static int OpenParens(string expr)
    {
        var balance = 0;
        foreach (var c in expr)
        {
            if (c == '(') balance++;
            else if (c == ')') balance--;
        }
        return balance;
    }

    public static bool EndsWithOpenParen(string expr) => expr.TrimEnd().EndsWith('(');

    /// Removes dangling operators and '(' so "2 * (" evaluates as "2".
    public static string TrimDangling(string expr)
    {
        var s = expr.TrimEnd();
        while (s.Length > 0 && (BinaryOperators.Contains(s[^1]) || s[^1] == '('))
            s = s[..^1].TrimEnd();
        return s;
    }

    public static string ReplaceTrailingOperator(string expr, string op)
    {
        var trimmed = expr.TrimEnd();
        if (trimmed.Length > 0 && BinaryOperators.Contains(trimmed[^1]))
            trimmed = trimmed[..^1].TrimEnd();
        return trimmed + " " + op + " ";
    }

    /// Appends the closing parens the user did not type: "(2 + (3" → "(2 + (3))".
    public static string CloseParens(string expr)
    {
        var open = OpenParens(expr);
        return open > 0 ? expr + new string(')', open) : expr;
    }

    /// Start index of the complete value at the end of <paramref name="expr"/>:
    /// a parenthesised group (with its function name, if any) or a number,
    /// optionally followed by postfix '%'. Returns -1 when there is none.
    public static int TrailingValueStart(string expr)
    {
        var i = expr.Length - 1;
        while (i >= 0 && (expr[i] == '%' || char.IsWhiteSpace(expr[i]))) i--;
        if (i < 0) return -1;

        if (expr[i] == ')')
        {
            var depth = 0;
            for (; i >= 0; i--)
            {
                if (expr[i] == ')') depth++;
                else if (expr[i] == '(' && --depth == 0) break;
            }
            if (i < 0) return -1;
            while (i > 0 && char.IsLetter(expr[i - 1])) i--;
            return i;
        }

        if (!char.IsDigit(expr[i]) && expr[i] != '.') return -1;
        while (i > 0 && (char.IsDigit(expr[i - 1]) || expr[i - 1] == '.')) i--;
        return i;
    }

    /// Best-effort value of the trailing group, used to show something meaningful
    /// on the display after ')' or '%'. Keeps the current display on failure.
    public static void ShowTrailingValue(CalcState state)
    {
        var start = TrailingValueStart(state.Expression);
        if (start < 0) return;
        try
        {
            state.Display = DisplayFormat.Format(Evaluator.Evaluate(state.Expression[start..], state.AngleMode));
        }
        catch (Exception ex) when (ex is CalcParseException or CalcEvalException)
        {
            // Leave the display as is; '=' will report the real problem.
        }
    }

    /// Puts a ready value (constant, memory, history, clipboard) into the display,
    /// inserting an implicit '*' when it follows ')' or '%'.
    public static void EnterValue(CalcState state, decimal value)
    {
        switch (state.Phase)
        {
            case CalcPhase.AfterValue:
                state.Expression += " * ";
                break;
            case CalcPhase.AfterEquals:
                state.Expression = string.Empty;
                break;
        }
        state.Display = DisplayFormat.Format(value);
        state.Phase = CalcPhase.EnteringDigit;
    }
}
