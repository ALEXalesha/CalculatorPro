using CalcPro.Wpf.Models;

namespace CalcPro.Wpf.Commands;

/// <summary>
/// Appends a binary operator (+, -, *, /, ^) to the expression.
/// If user is in the middle of typing a number, that number is folded into
/// the expression first.
/// </summary>
public sealed class OperatorCommand : BaseCalcCommand
{
    private readonly string _op;

    public OperatorCommand(string op) => _op = op;

    public override string Description => $"Operator '{_op}'";

    protected override void Apply(CalcState state)
    {
        switch (state.Phase)
        {
            case CalcPhase.Idle:
                // Allow chaining from last result: "ans + ..."
                if (state.LastResult.HasValue)
                {
                    state.Expression = FormatDecimal(state.LastResult.Value) + " " + _op + " ";
                }
                else
                {
                    state.Expression = state.Display + " " + _op + " ";
                }
                state.Phase = CalcPhase.AfterOperator;
                break;

            case CalcPhase.EnteringDigit:
                state.Expression += state.Display + " " + _op + " ";
                state.Phase = CalcPhase.AfterOperator;
                break;

            case CalcPhase.AfterOperator:
                // Replace the trailing operator
                state.Expression = ReplaceTrailingOperator(state.Expression, _op);
                break;

            case CalcPhase.AfterEquals:
                state.Expression = FormatDecimal(state.LastResult ?? 0m) + " " + _op + " ";
                state.Phase = CalcPhase.AfterOperator;
                break;
        }
    }

    private static string ReplaceTrailingOperator(string expr, string newOp)
    {
        var trimmed = expr.TrimEnd();
        if (trimmed.Length > 0 && "+-*/^".Contains(trimmed[^1]))
            trimmed = trimmed[..^1];
        return trimmed.TrimEnd() + " " + newOp + " ";
    }

    private static string FormatDecimal(decimal v) =>
        v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
