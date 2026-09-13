using CalcPro.Core.Models;
using CalcPro.Core.Services;

namespace CalcPro.Core.Commands;

/// <summary>
/// '(' opens a group (with implicit '*' after a number or a closed group);
/// ')' closes one, but only when there is something open and a value to close.
/// </summary>
public sealed class ParenCommand : BaseCalcCommand
{
    private readonly string _paren;

    public ParenCommand(string paren) => _paren = paren;

    public override string Description => $"Paren '{_paren}'";

    protected override void Apply(CalcState state)
    {
        if (_paren == "(") Open(state);
        else Close(state);
    }

    private static void Open(CalcState state)
    {
        if (ExpressionText.OpenParens(state.Expression) >= ExpressionText.MaxOpenParens) return;

        switch (state.Phase)
        {
            case CalcPhase.Idle:
            case CalcPhase.AfterEquals:
                state.Expression = "(";
                break;
            case CalcPhase.EnteringDigit:
                state.Expression += DisplayFormat.Operand(state.Display) + " * (";
                break;
            case CalcPhase.AfterOperator:
                state.Expression += "(";
                break;
            case CalcPhase.AfterValue:
                state.Expression += " * (";
                break;
        }
        state.Display = "0";
        state.Phase = CalcPhase.AfterOperator;
    }

    private static void Close(CalcState state)
    {
        if (ExpressionText.OpenParens(state.Expression) <= 0) return;

        switch (state.Phase)
        {
            case CalcPhase.EnteringDigit:
                state.Expression += DisplayFormat.Operand(state.Display) + ")";
                break;
            case CalcPhase.AfterValue:
                state.Expression += ")";
                break;
            default:
                // Right after an operator or '(' there is no value to close.
                return;
        }
        state.Phase = CalcPhase.AfterValue;
        ExpressionText.ShowTrailingValue(state);
    }
}
