using CalcPro.Core.Models;

namespace CalcPro.Core.Commands;

/// <summary>
/// Appends a single digit (0–9) or a decimal dot to the current display.
/// Handles state transitions Idle/AfterOperator/AfterValue/AfterEquals → EnteringDigit.
/// </summary>
public sealed class DigitCommand : BaseCalcCommand
{
    private readonly string _digit;

    public DigitCommand(string digit) => _digit = digit;

    public override string Description => $"Digit '{_digit}'";

    protected override void Apply(CalcState state)
    {
        var isDot = _digit == ".";
        var fresh = isDot ? "0." : _digit;

        switch (state.Phase)
        {
            case CalcPhase.EnteringDigit:
                if (isDot && state.Display.Contains('.')) return;
                if (state.Display.Length >= ExpressionText.MaxInputLength) return;
                if (!isDot && state.Display == "0") state.Display = _digit;
                else if (!isDot && state.Display == "-0") state.Display = "-" + _digit;
                else state.Display += _digit;
                return;

            case CalcPhase.AfterValue:
                // "(2 + 3) 4" reads as "(2 + 3) * 4".
                state.Expression += " * ";
                break;

            case CalcPhase.AfterEquals:
                state.Expression = string.Empty;
                break;
        }

        state.Display = fresh;
        state.Phase = CalcPhase.EnteringDigit;
    }
}
