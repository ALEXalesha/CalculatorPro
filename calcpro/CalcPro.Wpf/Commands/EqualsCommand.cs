using System.Globalization;
using CalcPro.Wpf.Models;
using CalcPro.Wpf.Services;

namespace CalcPro.Wpf.Commands;

/// <summary>
/// Finalizes the expression, evaluates it, and stores the history entry.
/// The history-entry add is captured for undo too.
/// </summary>
public sealed class EqualsCommand : BaseCalcCommand
{
    private readonly HistoryManager _history;
    private HistoryEntry? _addedEntry;

    public EqualsCommand(HistoryManager history) => _history = history;

    public override string Description => "Equals";

    protected override void Apply(CalcState state)
    {
        // Pressing = right after = is a no-op (Windows Calc actually repeats the
        // last op; we keep it simple — easier to reason about, easier to undo).
        if (state.Phase == CalcPhase.AfterEquals) return;

        var expressionToEval = state.Phase == CalcPhase.EnteringDigit || state.Phase == CalcPhase.Idle
            ? state.Expression + state.Display
            : state.Expression.TrimEnd().TrimEnd('+', '-', '*', '/', '^', '=').TrimEnd();

        if (string.IsNullOrWhiteSpace(expressionToEval))
            expressionToEval = state.Display;

        try
        {
            var result = Evaluator.Evaluate(expressionToEval, state.AngleMode);
            var resultStr = FormatResult(result);
            var displayed = expressionToEval;

            _addedEntry = new HistoryEntry(displayed, resultStr, DateTime.Now);
            _history.AddEntry(_addedEntry);

            state.LastResult = result;
            state.Expression = displayed + " =";
            state.Display = resultStr;
            state.Phase = CalcPhase.AfterEquals;
        }
        catch (Exception ex) when (ex is CalcParseException or CalcEvalException)
        {
            state.Display = "Error";
            state.Phase = CalcPhase.AfterEquals;
        }
    }

    public override void Undo(CalcState state)
    {
        base.Undo(state);
        if (_addedEntry is not null && _history.Entries.Count > 0 &&
            _history.Entries[0] == _addedEntry)
        {
            _history.Entries.RemoveAt(0);
        }
    }

    public static string FormatResult(decimal value)
    {
        var rounded = Math.Round(value, 12, MidpointRounding.ToEven);
        if (rounded == decimal.Truncate(rounded))
            return rounded.ToString("0", CultureInfo.InvariantCulture);
        return rounded.ToString("0.############", CultureInfo.InvariantCulture);
    }
}
