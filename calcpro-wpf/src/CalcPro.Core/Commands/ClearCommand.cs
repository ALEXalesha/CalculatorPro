using CalcPro.Core.Models;

namespace CalcPro.Core.Commands;

public sealed class ClearAllCommand : BaseCalcCommand
{
    public override string Description => "AC";

    protected override void Apply(CalcState state)
    {
        state.Expression = string.Empty;
        state.Display = "0";
        state.LastResult = null;
        state.Phase = CalcPhase.Idle;
    }
}

/// <summary>
/// Clear the currently-typed number, leaving the expression intact.
/// After '=' there is nothing to keep, so it starts over.
/// </summary>
public sealed class ClearEntryCommand : BaseCalcCommand
{
    public override string Description => "C";

    protected override void Apply(CalcState state)
    {
        state.Display = "0";
        switch (state.Phase)
        {
            case CalcPhase.EnteringDigit:
                state.Phase = state.Expression.Length == 0 ? CalcPhase.Idle : CalcPhase.AfterOperator;
                break;
            case CalcPhase.AfterEquals:
                state.Expression = string.Empty;
                state.Phase = CalcPhase.Idle;
                break;
        }
    }
}
