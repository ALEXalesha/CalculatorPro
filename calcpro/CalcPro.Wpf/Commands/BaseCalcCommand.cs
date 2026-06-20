using CalcPro.Wpf.Models;

namespace CalcPro.Wpf.Commands;

/// <summary>
/// Convenience base — captures the fields a command typically mutates so
/// Undo() can revert them. Concrete commands fill <see cref="Capture"/>
/// before mutating state.
/// </summary>
public abstract class BaseCalcCommand : ICalcCommand
{
    private string? _prevExpression;
    private string? _prevDisplay;
    private decimal? _prevLastResult;
    private CalcPhase? _prevPhase;

    public abstract string Description { get; }

    public void Execute(CalcState state)
    {
        Capture(state);
        Apply(state);
    }

    public virtual void Undo(CalcState state)
    {
        if (_prevExpression is not null) state.Expression = _prevExpression;
        if (_prevDisplay is not null) state.Display = _prevDisplay;
        if (_prevLastResult.HasValue) state.LastResult = _prevLastResult;
        if (_prevPhase.HasValue) state.Phase = _prevPhase.Value;
    }

    protected void Capture(CalcState state)
    {
        _prevExpression = state.Expression;
        _prevDisplay = state.Display;
        _prevLastResult = state.LastResult;
        _prevPhase = state.Phase;
    }

    protected abstract void Apply(CalcState state);
}
