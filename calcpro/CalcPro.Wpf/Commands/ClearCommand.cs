using CalcPro.Wpf.Models;

namespace CalcPro.Wpf.Commands;

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
/// </summary>
public sealed class ClearEntryCommand : BaseCalcCommand
{
    public override string Description => "C";

    protected override void Apply(CalcState state)
    {
        state.Display = "0";
        if (state.Phase == CalcPhase.EnteringDigit)
            state.Phase = CalcPhase.AfterOperator;
    }
}
