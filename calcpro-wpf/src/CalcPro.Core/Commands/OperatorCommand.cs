using CalcPro.Core.Models;
using CalcPro.Core.Services;

namespace CalcPro.Core.Commands;

/// <summary>
/// Appends a binary operator (+, -, *, /, ^) to the expression, or the postfix
/// percent ('%'), which completes the current value ("50 %" = 0.5).
/// If the user is in the middle of typing a number, it is folded in first.
/// </summary>
public sealed class OperatorCommand : BaseCalcCommand
{
    private readonly string _op;

    public OperatorCommand(string op) => _op = op;

    public override string Description => $"Operator '{_op}'";

    protected override void Apply(CalcState state)
    {
        if (_op == "%") ApplyPercent(state);
        else ApplyBinary(state);
    }

    private void ApplyBinary(CalcState state)
    {
        var spaced = " " + _op + " ";
        switch (state.Phase)
        {
            case CalcPhase.Idle:
                state.Expression = DisplayFormat.Operand(state.Display) + spaced;
                break;

            case CalcPhase.EnteringDigit:
                state.Expression += DisplayFormat.Operand(state.Display) + spaced;
                break;

            case CalcPhase.AfterOperator:
                // "(" followed by "*" has no meaning — ignore rather than build "( * ".
                if (ExpressionText.EndsWithOpenParen(state.Expression)) return;
                state.Expression = ExpressionText.ReplaceTrailingOperator(state.Expression, _op);
                return;

            case CalcPhase.AfterValue:
                state.Expression += spaced;
                break;

            case CalcPhase.AfterEquals:
                // Continue from whatever the display shows (it may have been negated
                // or passed through a function since '='). "Error" cannot be continued.
                if (!DisplayFormat.TryParse(state.Display, out var value)) return;
                state.Expression = DisplayFormat.Operand(value) + spaced;
                break;
        }
        state.Phase = CalcPhase.AfterOperator;
    }

    private static void ApplyPercent(CalcState state)
    {
        switch (state.Phase)
        {
            case CalcPhase.Idle:
            case CalcPhase.EnteringDigit:
                state.Expression += DisplayFormat.Operand(state.Display) + " %";
                break;

            case CalcPhase.AfterValue:
                state.Expression += " %";
                break;

            case CalcPhase.AfterEquals:
                if (!DisplayFormat.TryParse(state.Display, out var value)) return;
                state.Expression = DisplayFormat.Operand(value) + " %";
                break;

            default: // AfterOperator: nothing to take a percentage of.
                return;
        }
        state.Phase = CalcPhase.AfterValue;
        ExpressionText.ShowTrailingValue(state);
    }
}
