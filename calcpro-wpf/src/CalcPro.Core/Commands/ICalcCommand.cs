using CalcPro.Core.Models;

namespace CalcPro.Core.Commands;

/// <summary>
/// Calculator command (different from WPF's ICommand!). Implements
/// Command Pattern for undo/redo. Each user action becomes one of these.
/// </summary>
public interface ICalcCommand
{
    void Execute(CalcState state);
    void Undo(CalcState state);
    string Description { get; }
}
