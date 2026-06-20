using CalcPro.Wpf.Models;

namespace CalcPro.Wpf.Commands;

/// <summary>Toggles the sign of the current display number (+/-).</summary>
public sealed class SignCommand : BaseCalcCommand
{
    public override string Description => "Sign";

    protected override void Apply(CalcState state)
    {
        if (state.Display == "0") return;
        state.Display = state.Display.StartsWith('-')
            ? state.Display[1..]
            : "-" + state.Display;
    }
}
