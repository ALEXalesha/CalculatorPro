using CalcPro.Core.Models;
using CalcPro.Core.Services;

namespace CalcPro.Core.Commands;

/// <summary>
/// Finalizes the expression (dropping dangling operators, closing open parens),
/// evaluates it, and stores the history entry. The history-entry add is
/// captured for undo too.
/// </summary>
public sealed class EqualsCommand : BaseCalcCommand
{
    private readonly HistoryManager _history;
    private HistoryEntry? _addedEntry;

    public EqualsCommand(HistoryManager history) => _history = history;

    public override string Description => "Equals";

    protected override void Apply(CalcState state)
    {
        _addedEntry = null;

        // Pressing = right after = is a no-op (Windows Calc actually repeats the
        // last op; we keep it simple — easier to reason about, easier to undo).
        if (state.Phase == CalcPhase.AfterEquals) return;

        var expression = BuildExpression(state);

        try
        {
            var result = Evaluator.Evaluate(expression, state.AngleMode);
            var resultText = DisplayFormat.Format(result);

            _addedEntry = new HistoryEntry(expression, resultText, DateTime.Now);
            _history.AddEntry(_addedEntry);

            state.LastResult = result;
            state.Display = resultText;
        }
        catch (Exception ex) when (ex is CalcParseException or CalcEvalException)
        {
            state.Display = DisplayFormat.ErrorText;
        }

        state.Expression = expression + " =";
        state.Phase = CalcPhase.AfterEquals;
    }

    /// The exact string '=' evaluates in the current state.
    public static string BuildExpression(CalcState state)
    {
        var expr = state.Phase switch
        {
            CalcPhase.Idle or CalcPhase.EnteringDigit =>
                state.Expression + DisplayFormat.Operand(state.Display),
            CalcPhase.AfterOperator => ExpressionText.TrimDangling(state.Expression),
            _ => state.Expression.TrimEnd()
        };

        if (string.IsNullOrWhiteSpace(expr))
            expr = DisplayFormat.Operand(state.Display);

        return ExpressionText.CloseParens(expr);
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
}
