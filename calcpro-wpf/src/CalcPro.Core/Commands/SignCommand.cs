using CalcPro.Core.Models;
using CalcPro.Core.Services;

namespace CalcPro.Core.Commands;

/// <summary>Toggles the sign of the current number (+/-).</summary>
public sealed class SignCommand : BaseCalcCommand
{
    public override string Description => "Sign";

    protected override void Apply(CalcState state)
    {
        switch (state.Phase)
        {
            case CalcPhase.Idle:
            case CalcPhase.AfterOperator:
                // Start typing a negative number: "2 × ± 3" → 2 × (-3).
                state.Display = "-0";
                state.Phase = CalcPhase.EnteringDigit;
                break;

            case CalcPhase.EnteringDigit:
                state.Display = state.Display.StartsWith('-')
                    ? state.Display[1..]
                    : "-" + state.Display;
                break;

            case CalcPhase.AfterEquals:
                if (!DisplayFormat.TryParse(state.Display, out var value)) return;
                state.Display = DisplayFormat.Format(-value);
                state.LastResult = -value;
                break;

            // AfterValue: the value lives inside the expression; nothing to flip.
        }
    }
}
