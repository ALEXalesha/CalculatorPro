using CalcPro.Wpf.Models;

namespace CalcPro.Wpf.Commands;

/// <summary>
/// Appends a single digit (0–9) or a decimal dot to the current display.
/// Handles state transitions Idle/AfterOperator/AfterEquals → EnteringDigit.
/// </summary>
public sealed class DigitCommand : BaseCalcCommand
{
    private readonly string _digit;

    public DigitCommand(string digit) => _digit = digit;

    public override string Description => $"Digit '{_digit}'";

    protected override void Apply(CalcState state)
    {
        var isDot = _digit == ".";

        switch (state.Phase)
        {
            case CalcPhase.Idle:
                state.Display = isDot ? "0." : _digit;
                state.Phase = CalcPhase.EnteringDigit;
                break;

            case CalcPhase.EnteringDigit:
                if (isDot && state.Display.Contains('.')) return;
                if (state.Display == "0" && !isDot) state.Display = _digit;
                else state.Display += _digit;
                break;

            case CalcPhase.AfterOperator:
                state.Display = isDot ? "0." : _digit;
                state.Phase = CalcPhase.EnteringDigit;
                break;

            case CalcPhase.AfterEquals:
                state.Expression = string.Empty;
                state.Display = isDot ? "0." : _digit;
                state.Phase = CalcPhase.EnteringDigit;
                break;
        }
    }
}
