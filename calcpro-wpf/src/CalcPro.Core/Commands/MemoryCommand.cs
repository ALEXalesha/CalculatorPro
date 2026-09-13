using CalcPro.Core.Models;
using CalcPro.Core.Services;

namespace CalcPro.Core.Commands;

public enum MemoryOp { Add, Subtract, Store, Recall, Clear }

public sealed class MemoryCommand : BaseCalcCommand
{
    private readonly MemoryOp _op;
    private decimal _prevMemory;

    public MemoryCommand(MemoryOp op) => _op = op;

    public override string Description => $"M{_op}";

    protected override void Apply(CalcState state)
    {
        _prevMemory = state.Memory;

        if (_op == MemoryOp.Clear) { state.Memory = 0m; return; }
        if (_op == MemoryOp.Recall) { ExpressionText.EnterValue(state, state.Memory); return; }

        // "Error" on the display is not a number; memory stays untouched.
        if (!DisplayFormat.TryParse(state.Display, out var current)) return;
        try
        {
            state.Memory = _op switch
            {
                MemoryOp.Add => state.Memory + current,
                MemoryOp.Subtract => state.Memory - current,
                _ => current
            };
        }
        catch (OverflowException)
        {
            // Memory saturates silently rather than crashing the app.
        }
    }

    public override void Undo(CalcState state)
    {
        base.Undo(state);
        state.Memory = _prevMemory;
    }
}
