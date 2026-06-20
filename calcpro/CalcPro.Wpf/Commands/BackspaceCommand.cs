using CalcPro.Wpf.Models;

namespace CalcPro.Wpf.Commands;

public sealed class BackspaceCommand : BaseCalcCommand
{
    public override string Description => "Backspace";

    protected override void Apply(CalcState state)
    {
        if (state.Phase != CalcPhase.EnteringDigit) return;
        if (state.Display.Length <= 1 ||
            (state.Display.Length == 2 && state.Display.StartsWith('-')))
        {
            state.Display = "0";
            state.Phase = CalcPhase.AfterOperator;
            return;
        }
        state.Display = state.Display[..^1];
    }
}
