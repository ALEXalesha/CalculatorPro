using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;
using CalcPro.Core.Services;
using CalcPro.Wpf.Services;
using CalcPro.Core.Models;
using CalcPro.Wpf.ViewModels;

namespace CalcPro.Wpf;

/// <summary>
/// View code-behind is intentionally thin: it owns only purely-visual concerns
/// that don't belong in the ViewModel — the result animation, the
/// scientific-mode column width animation, the layout for small windows and the
/// theme menu. All calculator state mutation flows through the bound
/// CalcViewModel commands.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Неявный стиль сетки (MinHeight 24) не действует на клавиши со своим стилем:
        // AC, C, ⌫, операции, «=» и научные брали MinHeight 42 у GlassButton и в низком
        // окне не влезали в ряд - ряд срезал их снизу.
        foreach (var key in KeysGrid.Children.OfType<Button>().Concat(ProgKeysGrid.Children.OfType<Button>()))
            key.MinHeight = 24;
        RestorePlacement();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Окно открывается в том размере и на том месте, где его закрыли (правило -
    /// WindowPlacement: окно на отключённом мониторе встанет по центру, больше экрана -
    /// ужмётся). Файл не читается, если WindowPlacementService.FilePath = null.
    /// </summary>
    private void RestorePlacement()
    {
        var saved = WindowPlacementService.Load();
        if (saved is null) return;
        var p = WindowPlacement.Restore(saved, WindowPlacementService.Screens(),
            WindowLayout.DefaultWidth, WindowLayout.DefaultHeight, WindowLayout.MinWidth, WindowLayout.MinHeight);
        Width = p.Width;
        Height = p.Height;
        if (p.Left is { } left && p.Top is { } top)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        if (p.Maximized) WindowState = WindowState.Maximized;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel) return;
        // У развёрнутого или свёрнутого окна запоминаются границы, к которым оно вернётся.
        var b = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (!b.IsEmpty)
            WindowPlacementService.Save(new WindowPlacement.Placement(b.Left, b.Top, b.Width, b.Height, WindowState == WindowState.Maximized));
    }

    /// <summary>
    /// Меню тем под кнопкой-палитрой: пять тем Paint Pro, галочка у текущей. Выбор
    /// перекрашивает окно сразу и запоминается до следующего запуска.
    /// </summary>
    private void OnThemeButtonClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = ThemeButton, Placement = PlacementMode.Bottom };
        foreach (var theme in ThemeCatalog.All)
        {
            var item = new MenuItem
            {
                Header = theme.Name,
                ToolTip = theme.Note,
                IsCheckable = false,
                IsChecked = theme.Id == ThemeService.Current,
                Tag = theme.Id,
            };
            item.Click += (_, _) =>
            {
                var id = ThemeService.Apply((string)item.Tag);
                ThemeService.Save(id);
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    /// <summary>
    /// Меню режимов под кнопкой режима: обычный, научный, «Программист» (1.6.0), галочка
    /// у текущего. F1 по-прежнему переключает обычный и научный.
    /// </summary>
    private void OnModeButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CalcViewModel vm) return;
        var menu = new ContextMenu { PlacementTarget = ModeButton, Placement = PlacementMode.Bottom };
        foreach (var (mode, note) in new[]
                 {
                     (CalcMode.Standard, "Обычный калькулятор"),
                     (CalcMode.Scientific, "Функции, степени, скобки"),
                     (CalcMode.Programmer, "HEX, DEC, OCT, BIN и битовые операции"),
                 })
        {
            var item = new MenuItem { Header = mode.ToString(), ToolTip = note, IsChecked = vm.Mode == mode };
            item.Click += (_, _) => vm.Mode = mode;
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CalcViewModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
        ApplyLayout();
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyLayout();
        UpdateCornerClip();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnCloseClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    /// <summary>Радиус углов окна: чуть больше системных 8 px Windows 11.</summary>
    public const double CornerRadiusPx = 14;

    /// <summary>
    /// Углы окна срезаются по скруглённому прямоугольнику. У развёрнутого окна углы
    /// прямые, как у всех развёрнутых окон.
    /// </summary>
    private void UpdateCornerClip()
    {
        var r = WindowState == WindowState.Maximized ? 0 : CornerRadiusPx;
        var w = WindowRoot.ActualWidth;
        var h = WindowRoot.ActualHeight;
        WindowRoot.Clip = w > 0 && h > 0 ? new RectangleGeometry(new Rect(0, 0, w, h), r, r) : null;
        WindowEdge.CornerRadius = new CornerRadius(r);
        WindowEdge.Visibility = WindowState == WindowState.Maximized ? Visibility.Collapsed : Visibility.Visible;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateCornerClip();
    }

    /// <summary>
    /// Размер развёрнутого окна. Окно без системной рамки (прозрачное, ради скруглённых
    /// углов) Windows разворачивает на весь монитор, поверх панели задач; здесь ему
    /// назначается рабочая область его монитора - как у обычных окон.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is System.Windows.Interop.HwndSource source)
            source.AddHook(WndProc);
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg != WM_GETMINMAXINFO) return IntPtr.Zero;
        var monitor = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
        var info = new MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return IntPtr.Zero;
        var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<MinMaxInfo>(lParam);
        // Координаты развёрнутого окна - относительно своего монитора.
        mmi.MaxPosition = new Point32(info.Work.Left - info.Monitor.Left, info.Work.Top - info.Monitor.Top);
        mmi.MaxSize = new Point32(info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
        System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, false);
        return IntPtr.Zero; // остальное (минимум окна) WPF заполнит сам
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Point32 { public int X, Y; public Point32(int x, int y) { X = x; Y = y; } }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MinMaxInfo { public Point32 Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Rect32 { public int Left, Top, Right, Bottom; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public Rect32 Monitor; public Rect32 Work; public uint Flags; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    // Окно создаётся в размере по умолчанию, где история рядом.
    private bool _wasBeside = true;
    private bool _reopenHistoryWhenWide;

    /// <summary>
    /// Раскладка под размер окна по правилу WindowLayout: в узком окне история на месте
    /// клавиатуры (как в калькуляторе Windows), в низком - табло, шапка и отступы меньше.
    /// </summary>
    internal void ApplyLayout()
    {
        if (ActualWidth <= 0) return; // до первой раскладки ширина ещё 0, это не «узкое окно»
        var vm = DataContext as CalcViewModel;
        var beside = ActualWidth >= WindowLayout.HistoryBesideFrom;
        if (vm is not null && beside != _wasBeside)
        {
            var (open, reopen) = WindowLayout.AfterResize(_wasBeside, beside, vm.IsHistoryVisible, _reopenHistoryWhenWide);
            _wasBeside = beside;
            _reopenHistoryWhenWide = reopen;
            // Смена флага сама вызовет ApplyLayout через PropertyChanged.
            if (open != vm.IsHistoryVisible) { vm.IsHistoryVisible = open; return; }
        }

        var historyOpen = vm?.IsHistoryVisible == true;
        var layout = WindowLayout.For(ActualWidth, ActualHeight, historyOpen);

        CalcPanel.Visibility = layout.ShowKeypad ? Visibility.Visible : Visibility.Collapsed;
        CalcPanel.Margin = layout.HistoryBeside && historyOpen ? new Thickness(0, 0, 7, 0) : new Thickness(0);
        HistoryPanel.Visibility = layout.ShowHistory ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(HistoryPanel, layout.HistoryBeside ? 1 : 0);
        HistoryPanel.Width = layout.HistoryBeside ? 270 : double.NaN;
        HistoryPanel.Margin = layout.HistoryBeside ? new Thickness(7, 0, 0, 0) : new Thickness(0);
        HistoryBackButton.Visibility = layout.HistoryBeside ? Visibility.Collapsed : Visibility.Visible;

        CalcInner.Margin = new Thickness(layout.Narrow ? 12 : 18);
        HeaderGrid.Margin = new Thickness(0, 0, 0, layout.Compact ? 8 : 14);
        DisplayPanel.Padding = layout.Compact ? new Thickness(14, 8, 14, 8) : new Thickness(18, 16, 18, 16);
        DisplayText.FontSize = layout.Compact ? 34 : 46;
        DisplayBox.Height = Math.Ceiling(DisplayText.FontSize * 1.35); // строка шрифта Light
        MemoryRow.Margin = layout.Compact ? new Thickness(0, 8, 0, 8) : new Thickness(0, 12, 0, 12);

        // Кнопки-«таблетки» в шапке и в ряду памяти: по умолчанию высотой 42, в
        // низком окне 30; в узком ряду памяти подписи M+ и M- иначе обрезались полями.
        var pillHeight = layout.Compact ? 30.0 : 42.0;
        foreach (var pill in new[] { ThemeButton, AngleButton, ModeButton, HistoryBackButton })
            pill.MinHeight = pillHeight;
        foreach (var pill in MemoryRow.Children.OfType<Button>())
        {
            pill.MinHeight = pillHeight;
            pill.Padding = layout.Narrow ? new Thickness(2, 4, 2, 4) : new Thickness(10, 4, 10, 4);
        }

        // «Программист»: в низком окне строки систем и зазоры между клавишами меньше, иначе
        // семи рядам клавиш под четырьмя строками систем оставалось по 19 пикселей.
        foreach (var row in BaseRows.Children.OfType<FrameworkElement>())
            row.MinHeight = layout.Compact ? 20 : 26;
        BaseRows.Margin = layout.Compact ? new Thickness(0, 6, 0, 4) : new Thickness(0, 10, 0, 8);
        foreach (var key in ProgKeysGrid.Children.OfType<Button>())
            key.Margin = new Thickness(layout.Compact ? 2 : 4);

        if ((DataContext as CalcViewModel)?.IsScientific == true)
        {
            // Без анимации: ширина научных колонок просто следует за окном.
            foreach (var col in new[] { SciColA, SciColB })
            {
                col.BeginAnimation(ColumnDefinition.WidthProperty, null);
                col.Width = new GridLength(ScientificColumnWidth());
            }
        }
    }

    /// <summary>
    /// Научные колонки по 60, но не шире шестой части клавиатуры: в узком окне при 60
    /// на четыре обычные колонки оставалось по 25 пикселей.
    /// </summary>
    private double ScientificColumnWidth() =>
        KeysGrid.ActualWidth > 0 ? Math.Min(60, Math.Floor(KeysGrid.ActualWidth / 6)) : 60;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CalcViewModel vm) return;
        switch (e.PropertyName)
        {
            case nameof(CalcViewModel.IsScientific):
                AnimateScientificColumns(vm.IsScientific);
                break;
            case nameof(CalcViewModel.IsHistoryVisible):
                ApplyLayout();
                break;
            case nameof(CalcViewModel.ResultAnimationTrigger):
                PlayResultAnimation();
                break;
        }
    }

    private void AnimateScientificColumns(bool show)
    {
        var target = show ? ScientificColumnWidth() : 0.0;
        AnimateColumnWidth(SciColA, target);
        AnimateColumnWidth(SciColB, target);
    }

    private static void AnimateColumnWidth(ColumnDefinition col, double pixels)
    {
        var anim = new GridLengthAnimation
        {
            From = col.Width,
            To = new GridLength(pixels),
            Duration = TimeSpan.FromMilliseconds(260),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        col.BeginAnimation(ColumnDefinition.WidthProperty, anim);
    }

    private void PlayResultAnimation()
    {
        DisplayTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation
            {
                From = 18,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

        var glow = new System.Windows.Media.Effects.DropShadowEffect
        {
            // Вспышка цветом акцента текущей темы, а не всегда синим «Стеклянной».
            Color = (TryFindResource("AccentBrush") as SolidColorBrush)?.Color ?? Color.FromRgb(0x5B, 0x8D, 0xEF),
            BlurRadius = 30,
            ShadowDepth = 0,
            Opacity = 0.9
        };
        DisplayText.Effect = glow;
        var fade = new DoubleAnimation
        {
            From = 30,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(380),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, fade);
    }
}

/// <summary>
/// WPF doesn't ship a DoubleAnimation for GridLength out of the box — interpolate manually.
/// </summary>
public sealed class GridLengthAnimation : AnimationTimeline
{
    public override Type TargetPropertyType => typeof(GridLength);

    public static readonly DependencyProperty FromProperty =
        DependencyProperty.Register(nameof(From), typeof(GridLength), typeof(GridLengthAnimation));
    public static readonly DependencyProperty ToProperty =
        DependencyProperty.Register(nameof(To), typeof(GridLength), typeof(GridLengthAnimation));
    public static readonly DependencyProperty EasingFunctionProperty =
        DependencyProperty.Register(nameof(EasingFunction), typeof(IEasingFunction), typeof(GridLengthAnimation));

    public GridLength From { get => (GridLength)GetValue(FromProperty); set => SetValue(FromProperty, value); }
    public GridLength To { get => (GridLength)GetValue(ToProperty); set => SetValue(ToProperty, value); }
    public IEasingFunction? EasingFunction
    {
        get => (IEasingFunction?)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock clock)
    {
        var progress = clock.CurrentProgress ?? 0.0;
        if (EasingFunction is not null) progress = EasingFunction.Ease(progress);
        var from = From.Value;
        var to = To.Value;
        return new GridLength(from + (to - from) * progress);
    }
}
