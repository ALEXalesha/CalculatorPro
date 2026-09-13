using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CalcPro.Core.Commands;
using CalcPro.Core.Models;
using CalcPro.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalcPro.Wpf.ViewModels;

/// <summary>
/// MVVM ViewModel. All View-bound state is exposed here. View never reaches
/// into state directly; it binds and fires WPF ICommands defined below.
/// Every state change goes through an <see cref="ICalcCommand"/>, so it is undoable.
/// </summary>
public sealed partial class CalcViewModel : ObservableObject
{
    private readonly CalcState _state = new();
    private readonly HistoryManager _history = new();

    public CalcViewModel()
    {
        SyncFromState();
        _history.Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HistoryEntries));
    }

    public ObservableCollection<HistoryEntry> HistoryEntries => _history.Entries;

    [ObservableProperty] private string _display = "0";
    [ObservableProperty] private string _expression = string.Empty;
    [ObservableProperty] private bool _isMemoryActive;
    [ObservableProperty] private bool _isHistoryVisible = true;
    [ObservableProperty] private bool _isScientific;
    [ObservableProperty] private bool _isDegrees = true;
    [ObservableProperty] private bool _resultAnimationTrigger;

    public CalcMode Mode
    {
        get => _state.Mode;
        set
        {
            if (_state.Mode == value) return;
            _state.Mode = value;
            IsScientific = value == CalcMode.Scientific;
            OnPropertyChanged();
        }
    }

    public AngleMode AngleMode
    {
        get => _state.AngleMode;
        set
        {
            if (_state.AngleMode == value) return;
            _state.AngleMode = value;
            IsDegrees = value == AngleMode.Deg;
            OnPropertyChanged();
        }
    }

    // ====== Button commands ======

    [RelayCommand] private void PressDigit(string d) => Run(new DigitCommand(d));

    [RelayCommand] private void PressOperator(string op) => Run(new OperatorCommand(op));

    [RelayCommand] private void PressParen(string p) => Run(new ParenCommand(p));

    [RelayCommand]
    private void PressEquals()
    {
        Run(new EqualsCommand(_history));
        ResultAnimationTrigger = !ResultAnimationTrigger;
    }

    [RelayCommand] private void PressClear() => Run(new ClearAllCommand());

    [RelayCommand] private void PressClearEntry() => Run(new ClearEntryCommand());

    [RelayCommand] private void PressBackspace() => Run(new BackspaceCommand());

    [RelayCommand] private void PressSign() => Run(new SignCommand());

    [RelayCommand] private void PressFunction(string fn) => Run(new FunctionCommand(fn));

    [RelayCommand] private void PressConstant(string c) => Run(new ConstantCommand(c));

    [RelayCommand]
    private void PressMemory(string op)
    {
        var mop = op switch
        {
            "M+" => MemoryOp.Add,
            "M-" => MemoryOp.Subtract,
            "MS" => MemoryOp.Store,
            "MR" => MemoryOp.Recall,
            "MC" => MemoryOp.Clear,
            _ => throw new ArgumentException($"Unknown memory op {op}")
        };
        Run(new MemoryCommand(mop));
    }

    [RelayCommand]
    private void Undo()
    {
        _history.Undo(_state);
        SyncFromState();
    }

    [RelayCommand]
    private void Redo()
    {
        _history.Redo(_state);
        SyncFromState();
    }

    [RelayCommand]
    private void ToggleMode()
    {
        Mode = Mode == CalcMode.Standard ? CalcMode.Scientific : CalcMode.Standard;
    }

    [RelayCommand]
    private void ToggleAngleMode()
    {
        AngleMode = AngleMode == AngleMode.Deg ? AngleMode.Rad : AngleMode.Deg;
    }

    [RelayCommand]
    private void ToggleHistory() => IsHistoryVisible = !IsHistoryVisible;

    [RelayCommand]
    private void ClearHistory() => _history.ClearEntries();

    [RelayCommand]
    private void UseHistoryEntry(HistoryEntry? entry)
    {
        if (entry is null || !DisplayFormat.TryParse(entry.Result, out var value)) return;
        Run(new EnterValueCommand(value));
    }

    [RelayCommand]
    private void CopyResult()
    {
        try { Clipboard.SetText(Display); } catch { /* clipboard might be locked */ }
    }

    [RelayCommand]
    private void PasteResult()
    {
        try
        {
            var s = Clipboard.GetText().Trim();
            if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                Run(new EnterValueCommand(v));
        }
        catch { /* clipboard might be locked */ }
    }

    private void Run(ICalcCommand cmd)
    {
        cmd.Execute(_state);
        _history.Record(cmd);
        SyncFromState();
    }

    private void SyncFromState()
    {
        Display = _state.Display;
        Expression = _state.Expression;
        IsMemoryActive = _state.Memory != 0m;
    }
}
