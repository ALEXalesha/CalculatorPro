using CalcPro.Core.Models;

namespace CalcPro.Core.Commands;

/// <summary>
/// Convenience base — captures the fields a command typically mutates so
/// Undo() can revert them. <see cref="Execute"/> snapshots state before
/// delegating to <see cref="Apply"/>.
/// </summary>
public abstract class BaseCalcCommand : ICalcCommand
{
    private bool _captured;
    private string _prevExpression = string.Empty;
    private string _prevDisplay = "0";
    private decimal? _prevLastResult;
    private CalcPhase _prevPhase;

    public abstract string Description { get; }

    public void Execute(CalcState state)
    {
        Capture(state);
        Apply(state);
    }

    public virtual void Undo(CalcState state)
    {
        if (!_captured) return;
        // Restore every field unconditionally: a null LastResult is a real
        // value that must come back, not "nothing to restore".
        state.Expression = _prevExpression;
        state.Display = _prevDisplay;
        state.LastResult = _prevLastResult;
        state.Phase = _prevPhase;
    }

    protected void Capture(CalcState state)
    {
        _prevExpression = state.Expression;
        _prevDisplay = state.Display;
        _prevLastResult = state.LastResult;
        _prevPhase = state.Phase;
        _captured = true;
    }

    protected abstract void Apply(CalcState state);
}
