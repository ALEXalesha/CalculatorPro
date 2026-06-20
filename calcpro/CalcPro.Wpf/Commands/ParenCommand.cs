using CalcPro.Wpf.Models;

namespace CalcPro.Wpf.Commands;

public sealed class ParenCommand : BaseCalcCommand
{
    private readonly string _paren;

    public ParenCommand(string paren) => _paren = paren;

    public override string Description => $"Paren '{_paren}'";

    protected override void Apply(CalcState state)
    {
        if (_paren == "(")
        {
            if (state.Phase == CalcPhase.EnteringDigit)
                state.Expression += state.Display + " * ";
            state.Expression += "(";
            state.Display = "0";
            state.Phase = CalcPhase.AfterOperator;
        }
        else // ")"
        {
            if (state.Phase == CalcPhase.EnteringDigit)
            {
                state.Expression += state.Display + ")";
            }
            else
            {
                state.Expression = state.Expression.TrimEnd() + ")";
            }
            state.Phase = CalcPhase.EnteringDigit;
            state.Display = "0";
        }
    }
}
