namespace CalcPro.Core.Models;

/// <summary>
/// Explicit state-machine for user input. Replaces ad-hoc booleans like
/// justEvaluated / nextClearsDisplay.
///
/// Invariants every command preserves (see CommandInvariantTests):
///   Idle          → Expression is empty.
///   AfterOperator → Expression ends with a binary operator or '('.
///   AfterValue    → Expression ends with ')' or '%'.
///   AfterEquals   → Expression ends with " =". No other phase contains '='.
/// </summary>
public enum CalcPhase
{
    /// Initial state and after AC. Display shows "0".
    Idle,

    /// User is typing digits into the current operand (the display).
    EnteringDigit,

    /// User just typed an operator or '('. Next digit replaces the display.
    AfterOperator,

    /// Expression ends with a complete value such as ')' or 'x %'.
    /// A digit typed now means implicit multiplication.
    AfterValue,

    /// User just pressed '='. Any digit starts a new expression.
    AfterEquals
}
