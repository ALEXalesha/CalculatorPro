using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using CalcPro.Calculator;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace CalcPro;

public sealed partial class MainWindow : Window
{
    // ---- Calculator state ----
    private readonly Engine _engine = new();
    private readonly CalcState _state = new();
    private readonly Input _input;

    // ---- Backdrops (held alive so the system doesn't garbage them) ----
    private MicaController? _mica;
    private DesktopAcrylicController? _acrylic;
    private SystemBackdropConfiguration? _backdropCfg;

    // ---- Win32: manual drag for frameless window ----
    // SetTitleBar(elem) needs an OS title bar to anchor the drag region. We removed
    // it via SetBorderAndTitleBar(true, false) to hide system buttons, so we drag
    // manually by sending WM_NCLBUTTONDOWN with HTCAPTION when the user grabs the
    // custom titlebar strip. The OS takes over the move loop from there.
    private const uint WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 2;
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // ---- Layouts: same as JS STD_LAYOUT / SCI_LAYOUT / FRAC_LAYOUT ----
    private record KeyDef(string K, string Type);

    private static readonly KeyDef[] StdLayout =
    {
        new("⌫","del"), new("AC","util"), new("%","util"), new("÷","op"),
        new("7","digit"), new("8","digit"), new("9","digit"), new("×","op"),
        new("4","digit"), new("5","digit"), new("6","digit"), new("−","op"),
        new("1","digit"), new("2","digit"), new("3","digit"), new("+","op"),
        new("+/-","util"), new("0","digit"), new(".","digit"), new("=","eq"),
    };
    private static readonly KeyDef[] SciLayout =
    {
        new("2nd","sci"), new("π","sci"), new("e","sci"), new("⌫","del"),
        new("(","sci"), new(")","sci"), new("|x|","sci"), new("AC","util"),
        new("sin","sci"), new("cos","sci"), new("tan","sci"), new("cot","sci"),
        new("sinh","sci"), new("cosh","sci"), new("tanh","sci"), new("%","util"),
        new("x²","sci"), new("x³","sci"), new("xʸ","sci"), new("log","sci"),
        new("√","sci"), new("³√","sci"), new("ʸ√x","sci"), new("ln","sci"),
        new("1/x","sci"), new("eˣ","sci"), new("10ˣ","sci"), new("n!","sci"),
        new("7","digit"), new("8","digit"), new("9","digit"), new("÷","op"),
        new("4","digit"), new("5","digit"), new("6","digit"), new("×","op"),
        new("1","digit"), new("2","digit"), new("3","digit"), new("−","op"),
        new("+/-","util"), new("0","digit"), new(".","digit"), new("+","op"),
        new("EE","sci"), new("a⁄b","sci"), new("Rad","sci"), new("=","eq"),
    };
    private static readonly KeyDef[] FracLayout =
    {
        new("a/b","sci"), new("Mix","sci"), new("⌫","del"), new("AC","util"),
        new("↑","sci"), new("↓","sci"), new("%","util"), new("÷","op"),
        new("←","sci"), new("→","sci"), new("Simp","sci"), new("×","op"),
        new("7","digit"), new("8","digit"), new("9","digit"), new("−","op"),
        new("4","digit"), new("5","digit"), new("6","digit"), new("+","op"),
        new("1","digit"), new("2","digit"), new("3","digit"), new("=","eq"),
        new("+/-","util"), new("0","digit"), new(".","digit"), new("D⇄F","sci"),
    };

    // Alt-key flip for 2nd
    private static readonly Dictionary<string, string> AltKeys = new()
    {
        ["sin"] = "asin", ["cos"] = "acos", ["tan"] = "atan",
        ["sinh"] = "asinh", ["cosh"] = "acosh", ["tanh"] = "atanh",
        ["ln"] = "eˣ", ["log"] = "10ˣ",
        ["eˣ"] = "ln", ["10ˣ"] = "log",
        ["√"] = "x²", ["x²"] = "√",
        ["³√"] = "x³", ["x³"] = "³√",
    };

    public MainWindow()
    {
        InitializeComponent();
        _input = new Input(_engine, _state, OnStateChanged, OnEvaluated);

        // Load persisted history + memory
        var (history, memory) = HistoryStore.Load();
        _state.CalcHistory = history;
        _state.Memory = memory;

        ConfigureWindow();
        TrySetBackdrop();
        // Explicitly set Standard mode on startup. This wires keypad layout,
        // hides AnglePill/MemRow, and runs the first UpdateDisplay()
        SetMode(CalcMode.Standard);
        UpdateTabHighlight();

        // Keyboard hookup at window level
        RootGrid.PreviewKeyDown += OnPreviewKeyDown;
    }

    // ----------------------------------------------------------------
    // Window setup: frameless-ish, custom titlebar drag region, size
    // ----------------------------------------------------------------
    private void ConfigureWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var wndId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(wndId);

        // Initial size matches the Electron version: 320x640 logical
        appWindow.Resize(new SizeInt32(360, 720));
        appWindow.Title = "Calc Pro";

        // Drop the OS chrome entirely — we render our own min/close buttons.
        // SetBorderAndTitleBar(true, false) keeps the resize edge but removes
        // the system titlebar (and its built-in min/max/close).
        if (appWindow.Presenter is OverlappedPresenter op)
        {
            op.SetBorderAndTitleBar(true, false);
            op.IsMaximizable = false;   // calculator is fixed-shape
            op.IsResizable = true;
        }
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var tb = appWindow.TitleBar;
            tb.ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
        }
    }

    private void TrySetBackdrop()
    {
        if (MicaController.IsSupported())
        {
            _backdropCfg = new SystemBackdropConfiguration { IsInputActive = true, Theme = SystemBackdropTheme.Dark };
            Activated += (_, e) => _backdropCfg.IsInputActive = e.WindowActivationState != WindowActivationState.Deactivated;
            Closed += (_, _) => { _mica?.Dispose(); _mica = null; };
            _mica = new MicaController { Kind = MicaKind.BaseAlt };
            _mica.AddSystemBackdropTarget(WinRT.CastExtensions.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>(this));
            _mica.SetSystemBackdropConfiguration(_backdropCfg);
            return;
        }
        if (DesktopAcrylicController.IsSupported())
        {
            _backdropCfg = new SystemBackdropConfiguration { IsInputActive = true, Theme = SystemBackdropTheme.Dark };
            Activated += (_, e) => _backdropCfg.IsInputActive = e.WindowActivationState != WindowActivationState.Deactivated;
            Closed += (_, _) => { _acrylic?.Dispose(); _acrylic = null; };
            _acrylic = new DesktopAcrylicController();
            _acrylic.AddSystemBackdropTarget(WinRT.CastExtensions.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>(this));
            _acrylic.SetSystemBackdropConfiguration(_backdropCfg);
        }
        // If neither — fall back to a dark solid background from XAML resource.
    }

    // ----------------------------------------------------------------
    // Keypad rendering
    // ----------------------------------------------------------------
    private void RenderKeys()
    {
        KeyPad.Children.Clear();
        KeyPad.RowDefinitions.Clear();
        KeyPad.ColumnDefinitions.Clear();

        var layout = _state.Mode switch
        {
            CalcMode.Scientific => SciLayout,
            CalcMode.Fractions  => FracLayout,
            _                   => StdLayout,
        };
        int cols = 4;
        int rows = (layout.Length + cols - 1) / cols;
        for (int c = 0; c < cols; c++)
            KeyPad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int r = 0; r < rows; r++)
            KeyPad.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < layout.Length; i++)
        {
            var def = layout[i];
            // 2nd toggle flip
            var label = def.K;
            if (_state.Mode == CalcMode.Scientific && _state.AltSet && AltKeys.TryGetValue(def.K, out var alt))
                label = alt;
            if (def.K == "Rad")
                label = _state.Angle.ToString().ToUpperInvariant();

            var btn = new Button
            {
                Content = label,
                Tag = def.K, // original key — what we send to PressKey
                Style = StyleFor(def.Type),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            btn.Click += OnKey_Click;

            Grid.SetRow(btn, i / cols);
            Grid.SetColumn(btn, i % cols);
            KeyPad.Children.Add(btn);
        }
    }

    private Style StyleFor(string type) => (Style)Application.Current.Resources[type switch
    {
        "digit" => "DigitButton",
        "op"    => "OpButton",
        "util"  => "UtilButton",
        "del"   => "DelButton",
        "eq"    => "EqButton",
        "sci"   => "SciButton",
        _       => "GlassButton",
    }];

    // ----------------------------------------------------------------
    // Display refresh
    // ----------------------------------------------------------------
    private void UpdateDisplay()
    {
        if (_state.Mode == CalcMode.Fractions)
        {
            HistoryLine.Text = _state.HasError ? "" :
                (_state.FracResult is not null ? Fractions.PlainText(_state) : " ");
            if (_state.HasError) MainResult.Text = _state.Display;
            else if (_state.FracResult is not null)
            {
                MainResult.Text = _state.FractionMode && _state.FracResultFrac is { } f && f.D != 1
                    ? FormatMixed(f) + "  =  " + Format.Number(_state.FracResult.Value)
                    : Format.Number(_state.FracResult.Value);
            }
            else
            {
                MainResult.Text = Fractions.PlainText(_state);
                if (string.IsNullOrEmpty(MainResult.Text)) MainResult.Text = "·";
            }
            FractionLine.Visibility = Visibility.Collapsed;
        }
        else
        {
            bool showExpr = _state.Expression.Length > 0 && !_state.JustEvaluated && !_state.HasError;
            HistoryLine.Text = string.IsNullOrEmpty(_state.HistoryLine) ? " " : _state.HistoryLine;
            MainResult.Text = _state.HasError
                ? _state.Display
                : (showExpr ? Format.ExpressionToDisplay(_state.Expression) : _state.Display);

            if (_state.FractionMode && !_state.HasError && _state.Result is not null)
            {
                var f = Format.FormatFraction(_state.Result.Value);
                if (f is not null)
                {
                    FractionLine.Text = "= " + f;
                    FractionLine.Visibility = Visibility.Visible;
                }
                else FractionLine.Visibility = Visibility.Collapsed;
            }
            else FractionLine.Visibility = Visibility.Collapsed;
        }

        // Shrink font for long strings
        var len = MainResult.Text.Length;
        MainResult.FontSize = len > 22 ? 28 : len > 16 ? 36 : len > 12 ? 44 : len > 9 ? 50 : 56;
    }

    private static string FormatMixed(Fraction f)
    {
        long whole = f.N / f.D;
        long num = Math.Abs(f.N - whole * f.D);
        var sign = f.N < 0 ? "−" : "";
        if (whole != 0) return $"{sign}{Math.Abs(whole)} {num}/{f.D}";
        return $"{sign}{num}/{f.D}";
    }

    private void OnStateChanged() => UpdateDisplay();
    private void OnEvaluated() { /* hook for a result-animation later */ }

    // ----------------------------------------------------------------
    // Manual window drag for the frameless titlebar.
    // Wired up from MainWindow.xaml via AppTitleBar.PointerPressed.
    // ----------------------------------------------------------------
    private void AppTitleBar_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Ignore touch/pen so resize gestures and tap-to-focus still work cleanly.
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse) return;
        var props = e.GetCurrentPoint((UIElement)sender).Properties;
        if (!props.IsLeftButtonPressed) return;

        // Hand off to the OS move loop. ReleaseCapture is needed because WinUI
        // still holds the pointer until we let go.
        var hwnd = WindowNative.GetWindowHandle(this);
        ReleaseCapture();
        SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        e.Handled = true;
    }

    // ----------------------------------------------------------------
    // Button events
    // ----------------------------------------------------------------
    private void OnKey_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        var orig = (string)btn.Tag;
        // If 2nd is active and this is a flipped key, send the flipped name
        var send = _state.Mode == CalcMode.Scientific && _state.AltSet && AltKeys.TryGetValue(orig, out var alt)
            ? alt : orig;
        _input.PressKey(send);
        if (orig == "2nd" || orig == "Rad") RenderKeys();
    }

    private void TabStandard_Click(object _, RoutedEventArgs __)   => SetMode(CalcMode.Standard);
    private void TabScientific_Click(object _, RoutedEventArgs __) => SetMode(CalcMode.Scientific);
    private void TabFraction_Click(object _, RoutedEventArgs __)   => SetMode(CalcMode.Fractions);
    private void AnglePill_Click(object _, RoutedEventArgs __)
    {
        _state.Angle = _state.Angle switch
        {
            AngleMode.Deg  => AngleMode.Rad,
            AngleMode.Rad  => AngleMode.Grad,
            _              => AngleMode.Deg,
        };
        _engine.Angle = _state.Angle;
        AnglePill.Content = _state.Angle.ToString().ToUpperInvariant();
        RenderKeys();
    }

    private void SetMode(CalcMode mode)
    {
        if (mode == CalcMode.Fractions) { _state.FracResetAll(); _state.FractionMode = true; }
        else { _state.FractionMode = false; }
        _state.Mode = mode;
        AnglePill.Visibility = mode == CalcMode.Scientific ? Visibility.Visible : Visibility.Collapsed;
        MemRow.Visibility    = mode == CalcMode.Scientific ? Visibility.Visible : Visibility.Collapsed;
        UpdateTabHighlight();
        RenderKeys();
        UpdateDisplay();
    }

    /// <summary>
    /// Visually mark the active mode tab. WinUI buttons don't have a "selected" state
    /// out of the box, so we tweak Background + FontWeight directly.
    /// </summary>
    private void UpdateTabHighlight()
    {
        var dim   = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SciBg"];
        var bright = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["GlassBgStrong"];
        foreach (var (btn, mode) in new[] {
            (TabStandard,   CalcMode.Standard),
            (TabScientific, CalcMode.Scientific),
            (TabFraction,   CalcMode.Fractions),
        })
        {
            bool active = _state.Mode == mode;
            btn.Background = active ? bright : dim;
            btn.FontWeight = active ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
    }

    // ---- Memory ----
    private void MemAdd_Click(object _, RoutedEventArgs __)    => _input.MemAdd();
    private void MemSub_Click(object _, RoutedEventArgs __)    => _input.MemSub();
    private void MemStore_Click(object _, RoutedEventArgs __)  => _input.MemStore();
    private void MemRecall_Click(object _, RoutedEventArgs __) => _input.MemRecall();
    private void MemClear_Click(object _, RoutedEventArgs __)  => _input.MemClear();

    // ---- Window controls ----
    private void BtnMinimize_Click(object _, RoutedEventArgs __)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var wndId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var aw = AppWindow.GetFromWindowId(wndId);
        if (aw.Presenter is OverlappedPresenter op) op.Minimize();
    }
    private void BtnClose_Click(object _, RoutedEventArgs __) => Close();
    private void BtnHistory_Click(object _, RoutedEventArgs __)
    {
        // For MVP show history as a simple ContentDialog. Could be slid panel later.
        var flyout = new MenuFlyout();
        if (_state.CalcHistory.Count == 0)
            flyout.Items.Add(new MenuFlyoutItem { Text = "No calculations yet", IsEnabled = false });
        foreach (var h in _state.CalcHistory)
        {
            var item = new MenuFlyoutItem { Text = $"{h.Expr} = {Format.Number(h.Result)}" };
            var entry = h;
            item.Click += (_, _) =>
            {
                var v = Format.Number(entry.Result);
                _state.Expression = v;
                _state.Display = v;
                _state.Result = entry.Result;
                _state.JustEvaluated = false;
                _state.HasError = false;
                _state.HistoryLine = "";
                UpdateDisplay();
            };
            flyout.Items.Add(item);
        }
        flyout.ShowAt(BtnHistory);
    }

    // ----------------------------------------------------------------
    // Keyboard mapping — same table as KEY_MAP in JS
    // ----------------------------------------------------------------
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Ctrl+H toggles history
        if (e.Key == VirtualKey.H && IsCtrlDown())
        {
            BtnHistory_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }
        if (e.Key == VirtualKey.F1)
        {
            SetMode(_state.Mode == CalcMode.Scientific ? CalcMode.Standard : CalcMode.Scientific);
            e.Handled = true;
            return;
        }
        if (_state.Mode == CalcMode.Fractions)
        {
            string? fk = e.Key switch
            {
                VirtualKey.Up    => "↑",
                VirtualKey.Down  => "↓",
                VirtualKey.Left  => "←",
                VirtualKey.Right => "→",
                _                => null,
            };
            if (fk is not null)
            {
                _input.PressKey(fk);
                e.Handled = true;
                return;
            }
        }

        string? mapped = e.Key switch
        {
            VirtualKey.Number0 or VirtualKey.NumberPad0 => "0",
            VirtualKey.Number1 or VirtualKey.NumberPad1 => "1",
            VirtualKey.Number2 or VirtualKey.NumberPad2 => "2",
            VirtualKey.Number3 or VirtualKey.NumberPad3 => "3",
            VirtualKey.Number4 or VirtualKey.NumberPad4 => "4",
            VirtualKey.Number5 or VirtualKey.NumberPad5 => "5",
            VirtualKey.Number6 or VirtualKey.NumberPad6 => "6",
            VirtualKey.Number7 or VirtualKey.NumberPad7 => "7",
            VirtualKey.Number8 or VirtualKey.NumberPad8 => "8",
            VirtualKey.Number9 or VirtualKey.NumberPad9 => "9",
            VirtualKey.Decimal => ".",
            VirtualKey.Add      => "+",
            VirtualKey.Subtract => "−",
            VirtualKey.Multiply => "×",
            VirtualKey.Divide   => "÷",
            VirtualKey.Enter    => "=",
            VirtualKey.Back     => "⌫",
            VirtualKey.Delete   => "AC",
            VirtualKey.Escape   => "AC",
            _ => null,
        };
        // Shift+= handled separately because '=' has no clean VirtualKey
        if (mapped is null && e.Key == (VirtualKey)187 /* OEM_PLUS */)
            mapped = IsShiftDown() ? "+" : "=";
        if (mapped is null && e.Key == (VirtualKey)189 /* OEM_MINUS */) mapped = "−";

        if (mapped is null) return;
        _input.PressKey(mapped);
        e.Handled = true;
    }

    private bool IsCtrlDown()
    {
        var s = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        return s.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    }
    private bool IsShiftDown()
    {
        var s = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        return s.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    }
}
