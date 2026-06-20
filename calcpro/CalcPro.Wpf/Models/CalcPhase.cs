namespace CalcPro.Wpf.Models;

/// <summary>
/// Explicit state-machine for user input. Replaces ad-hoc booleans like
/// justEvaluated / nextClearsDisplay.
/// </summary>
public enum CalcPhase
{
    /// Initial state and after AC. Display shows "0".
    Idle,

    /// User is typing digits into the current operand.
    EnteringDigit,

    /// User just typed an operator. Next digit replaces buffer, not appends.
    AfterOperator,

    /// User just pressed '='. Any digit starts a new expression.
    AfterEquals
}
