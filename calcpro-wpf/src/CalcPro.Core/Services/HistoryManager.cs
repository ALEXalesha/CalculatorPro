using System.Collections.ObjectModel;
using CalcPro.Core.Commands;
using CalcPro.Core.Models;

namespace CalcPro.Core.Services;

/// <summary>
/// Owns two distinct concepts:
///   1. <see cref="Entries"/> — completed "expr = result" calculations shown in the right panel.
///   2. Undo/Redo stacks of <see cref="ICalcCommand"/> for keystroke-level reversal.
///
/// The two are intentionally separate: pressing Ctrl+Z undoes a digit input,
/// not "undo the entire previous calculation".
/// </summary>
public sealed class HistoryManager
{
    private const int MaxEntries = 50;

    public ObservableCollection<HistoryEntry> Entries { get; } = new();

    private readonly Stack<ICalcCommand> _undoStack = new();
    private readonly Stack<ICalcCommand> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void AddEntry(HistoryEntry entry)
    {
        Entries.Insert(0, entry);
        while (Entries.Count > MaxEntries) Entries.RemoveAt(Entries.Count - 1);
    }

    public void ClearEntries() => Entries.Clear();

    /// Records a command that has already been executed. Clears the redo stack
    /// since the linear timeline diverges from any previously-undone work.
    public void Record(ICalcCommand command)
    {
        _undoStack.Push(command);
        _redoStack.Clear();
    }

    public void Undo(CalcState state)
    {
        if (_undoStack.Count == 0) return;
        var cmd = _undoStack.Pop();
        cmd.Undo(state);
        _redoStack.Push(cmd);
    }

    public void Redo(CalcState state)
    {
        if (_redoStack.Count == 0) return;
        var cmd = _redoStack.Pop();
        cmd.Execute(state);
        _undoStack.Push(cmd);
    }

    public void ClearUndoHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
