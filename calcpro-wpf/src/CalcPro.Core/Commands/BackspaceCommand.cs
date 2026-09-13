using CalcPro.Core.Models;

namespace CalcPro.Core.Commands;

/// <summary>Deletes the last typed character of the current number.</summary>
public sealed class BackspaceCommand : BaseCalcCommand
{
    public override string Description => "Backspace";

    protected override void Apply(CalcState state)
    {
        if (state.Phase != CalcPhase.EnteringDigit) return;

        var shorter = state.Display.Length > 1 ? state.Display[..^1] : string.Empty;
        if (shorter.Length == 0 || shorter == "-")
        {
            state.Display = "0";
            state.Phase = state.Expression.Length == 0 ? CalcPhase.Idle : CalcPhase.AfterOperator;
            return;
        }
        state.Display = shorter;
    }
}
