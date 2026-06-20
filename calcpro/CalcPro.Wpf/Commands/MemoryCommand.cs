using System.Globalization;
using CalcPro.Wpf.Models;

namespace CalcPro.Wpf.Commands;

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
        var current = decimal.TryParse(state.Display, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var v) ? v : 0m;

        switch (_op)
        {
            case MemoryOp.Add: state.Memory += current; break;
            case MemoryOp.Subtract: state.Memory -= current; break;
            case MemoryOp.Store: state.Memory = current; break;
            case MemoryOp.Clear: state.Memory = 0m; break;
            case MemoryOp.Recall:
                state.Display = state.Memory.ToString("0.############", CultureInfo.InvariantCulture);
                state.Phase = CalcPhase.EnteringDigit;
                break;
        }
    }

    public override void Undo(CalcState state)
    {
        base.Undo(state);
        state.Memory = _prevMemory;
    }
}
