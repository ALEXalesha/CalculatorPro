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
    [ObservableProperty] private bool _isProgrammer;
    [ObservableProperty] private string _modeName = "Standard";

    // ───────── режим «Программист» (1.6.0) ─────────
    // Своё состояние, отдельное от обычного: переход между режимами ничего не теряет.
    private readonly ProgrammerCalculator _prog = new();

    [ObservableProperty] private string _progHex = "0";
    [ObservableProperty] private string _progDec = "0";
    [ObservableProperty] private string _progOct = "0";
    [ObservableProperty] private string _progBin = "0";
    [ObservableProperty] private bool _baseIsHex;
    [ObservableProperty] private bool _baseIsDec = true;
    [ObservableProperty] private bool _baseIsOct;
    [ObservableProperty] private bool _baseIsBin;
    /// <summary>Доступны цифры 8 и 9.</summary>
    [ObservableProperty] private bool _allowsDecDigits = true;
    /// <summary>Доступны цифры 2-7.</summary>
    [ObservableProperty] private bool _allowsOctDigits = true;

    /// <summary>Система счисления режима «Программист».</summary>
    public NumberBase ProgBase => _prog.Base;

    /// <summary>Клавиша режима «Программист»: цифра, операция, HEX/DEC/OCT/BIN и т.д.</summary>
    [RelayCommand]
    private void PressProg(string key)
    {
        _prog.Press(key);
        SyncProgrammer();
    }

    private void SyncProgrammer()
    {
        Display = _prog.Display;
        Expression = _prog.Expression;
        var v = _prog.Value;
        ProgHex = ProgrammerCalculator.Group(ProgrammerCalculator.Format(v, NumberBase.Hex), NumberBase.Hex);
        ProgDec = ProgrammerCalculator.Group(ProgrammerCalculator.Format(v, NumberBase.Dec), NumberBase.Dec);
        ProgOct = ProgrammerCalculator.Group(ProgrammerCalculator.Format(v, NumberBase.Oct), NumberBase.Oct);
        ProgBin = ProgrammerCalculator.Group(ProgrammerCalculator.Format(v, NumberBase.Bin), NumberBase.Bin);
        var b = _prog.Base;
        BaseIsHex = b == NumberBase.Hex;
        BaseIsDec = b == NumberBase.Dec;
        BaseIsOct = b == NumberBase.Oct;
        BaseIsBin = b == NumberBase.Bin;
        AllowsDecDigits = b >= NumberBase.Dec;
        AllowsOctDigits = b >= NumberBase.Oct;
        // Щелчок по уже выбранной строке снимает с неё отметку в самой кнопке, а значение в
        // модели то же - без явного уведомления отметка так и осталась бы снятой.
        OnPropertyChanged(nameof(BaseIsHex));
        OnPropertyChanged(nameof(BaseIsDec));
        OnPropertyChanged(nameof(BaseIsOct));
        OnPropertyChanged(nameof(BaseIsBin));
        OnPropertyChanged(nameof(ProgBase));
    }

    /// <summary>Пункт меню режимов: Standard, Scientific или Programmer.</summary>
    [RelayCommand]
    private void SetMode(string mode) =>
        Mode = Enum.TryParse<CalcMode>(mode, out var m) ? m : CalcMode.Standard;
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
            IsProgrammer = value == CalcMode.Programmer;
            ModeName = value.ToString();
            // Табло показывает то, что относится к режиму: у «Программиста» свои числа.
            if (IsProgrammer) SyncProgrammer(); else SyncFromState();
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

    [RelayCommand]
    private void PressDigit(string d)
    {
        if (IsProgrammer) { PressProg(d); return; }
        // Буквы A-F с клавиатуры - только для «Программиста».
        if (d.Length != 1 || !(char.IsAsciiDigit(d[0]) || d == ".")) return;
        Run(new DigitCommand(d));
    }

    [RelayCommand]
    private void PressOperator(string op)
    {
        if (IsProgrammer)
        {
            var key = op switch { "+" => "+", "-" => "−", "*" => "×", "/" => "÷", "%" => "Mod", _ => null };
            if (key is not null) PressProg(key);
            return;
        }
        Run(new OperatorCommand(op));
    }

    [RelayCommand]
    private void PressParen(string p)
    {
        if (IsProgrammer) { PressProg(p); return; }
        Run(new ParenCommand(p));
    }

    [RelayCommand]
    private void PressEquals()
    {
        if (IsProgrammer)
        {
            PressProg("=");
            ResultAnimationTrigger = !ResultAnimationTrigger;
            return;
        }
        Run(new EqualsCommand(_history));
        ResultAnimationTrigger = !ResultAnimationTrigger;
    }

    [RelayCommand] private void PressClear() { if (IsProgrammer) PressProg("AC"); else Run(new ClearAllCommand()); }

    [RelayCommand] private void PressClearEntry() { if (IsProgrammer) PressProg("CE"); else Run(new ClearEntryCommand()); }

    [RelayCommand] private void PressBackspace() { if (IsProgrammer) PressProg("⌫"); else Run(new BackspaceCommand()); }

    [RelayCommand] private void PressSign() { if (IsProgrammer) PressProg("±"); else Run(new SignCommand()); }

    [RelayCommand] private void PressFunction(string fn) { if (!IsProgrammer) Run(new FunctionCommand(fn)); }

    [RelayCommand] private void PressConstant(string c) { if (!IsProgrammer) Run(new ConstantCommand(c)); }

    [RelayCommand]
    private void PressMemory(string op)
    {
        if (IsProgrammer) return; // память - у обычных чисел
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
        if (IsProgrammer) return;
        _history.Undo(_state);
        SyncFromState();
    }

    [RelayCommand]
    private void Redo()
    {
        if (IsProgrammer) return;
        _history.Redo(_state);
        SyncFromState();
    }

    [RelayCommand]
    private void ToggleMode()
    {
        // F1 и прежняя кнопка: обычный ⇄ научный; «Программист» выбирается в меню.
        Mode = Mode == CalcMode.Scientific ? CalcMode.Standard : CalcMode.Scientific;
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
        if (IsProgrammer) Mode = CalcMode.Standard; // запись истории - обычное число
        if (entry is null || !DisplayFormat.TryParse(entry.Result, out var value)) return;
        Run(new EnterValueCommand(value));
    }

    [RelayCommand]
    private void CopyResult()
    {
        // В «Программисте» - число без пробелов разбивки, в текущей системе.
        var text = IsProgrammer ? ProgrammerCalculator.Format(_prog.Value, _prog.Base) : Display;
        try { Clipboard.SetText(text); } catch { /* clipboard might be locked */ }
    }

    [RelayCommand]
    private void PasteResult()
    {
        try
        {
            var s = Clipboard.GetText().Trim();
            if (IsProgrammer)
            {
                // Вставка набирает число цифра за цифрой в текущей системе.
                if (ProgrammerCalculator.TryParse(s, _prog.Base, out var n))
                {
                    PressProg("CE");
                    var digits = ProgrammerCalculator.Format(n, _prog.Base);
                    foreach (var c in digits.TrimStart('-')) PressProg(c.ToString());
                    if (digits.StartsWith('-')) PressProg("±");
                }
                return;
            }
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
