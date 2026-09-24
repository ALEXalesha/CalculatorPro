// Кадры для README: собираются программой, а не снимком экрана.
//
//   dotnet run --project calcpro-wpf\tools\CalcPro.Screenshots
//
// Настоящее окно MainWindow с настоящими стилями приложения (App.xaml) и его же
// ViewModel. Нажатия идут через те же команды, к которым привязаны кнопки. Окно
// открывается далеко за краем экрана и в кадр рисуется само через
// RenderTargetBitmap, поэтому чужое окно в снимок попасть не может: однажды снимок
// экрана по прямоугольнику окна захватил личный чат.
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CalcPro.Wpf;
using CalcPro.Wpf.ViewModels;

internal static class Program
{
    private const double Scale = 1.5;

    [STAThread]
    private static int Main()
    {
        var output = Path.Combine(FindRepo(), "docs", "screenshots");
        Directory.CreateDirectory(output);

        // Ресурсы приложения (темы, кисти, стили кнопок) живут в App.xaml; без них
        // окно не соберётся. Run() не вызывается: цикл сообщений крутим сами.
        var app = new App();
        app.InitializeComponent();

        var window = new MainWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            ShowInTaskbar = false,
            ShowActivated = false,
        };
        window.Show();
        var vm = (CalcViewModel)window.DataContext;
        Wait(600);

        // Standard: точная десятичная арифметика, скобки и история справа.
        Press(vm, "0 . 1 + 0 . 2 =");
        Press(vm, "( 2 + 3 ) * 4 =");
        Press(vm, "1 2 3 4 . 5 * 1 2 MS =");
        Save(window, output, "standard.png", vm);

        // Scientific: функции постфиксом, как на настоящем калькуляторе.
        vm.ToggleModeCommand.Execute(null);
        Press(vm, "AC 3 0 sin + 2 sqrt =");
        Save(window, output, "scientific.png", vm);

        // Пять тем Paint Pro на обычном режиме. Тема ставится только на экран:
        // ThemeService.Save не зовётся, файл настроек человека не трогается.
        vm.ToggleModeCommand.Execute(null);
        Press(vm, "AC 1 2 3 4 * 5 6 =");
        var frames = new List<BitmapSource>();
        foreach (var theme in CalcPro.Core.Services.ThemeCatalog.All)
        {
            CalcPro.Wpf.Services.ThemeService.Apply(theme.Id);
            frames.Add(Render(window));
        }
        CalcPro.Wpf.Services.ThemeService.Apply(CalcPro.Core.Services.ThemeCatalog.DefaultId);
        SaveGrid(frames, output, "themes.png");

        // Маленькое окно: минимум как у калькулятора Windows. Это же и проверка раскладки:
        // каждая видимая кнопка должна целиком стоять в окне и быть не меньше 20 пикселей,
        // иначе программа падает с кодом 1 и называет кнопку.
        // Длинный пример: в узкой истории он должен переноситься, а не обрезаться.
        Press(vm, "AC 1 2 3 4 5 6 7 8 9 * 9 8 7 6 5 4 3 2 1 + 1 1 1 1 1 1 1 1 1 * 2 2 2 2 2 2 2 2 2 - 3 3 3 3 3 3 3 3 3 =");
        window.Width = CalcPro.Core.Services.WindowLayout.MinWidth;
        window.Height = CalcPro.Core.Services.WindowLayout.MinHeight;
        Wait(300);
        var small = new List<BitmapSource>();
        var problems = new List<string>();
        small.Add(RenderChecked(window, "standard", problems));
        vm.ToggleModeCommand.Execute(null);
        small.Add(RenderChecked(window, "scientific", problems));
        vm.ToggleModeCommand.Execute(null);
        vm.ToggleHistoryCommand.Execute(null);
        small.Add(RenderChecked(window, "history", problems));
        vm.ToggleHistoryCommand.Execute(null);
        SaveGrid(small, output, "small.png");

        window.Close();
        foreach (var p in problems) Console.Error.WriteLine("  раскладка: " + p);
        return problems.Count == 0 ? 0 : 1;
    }

    /// <summary>Кадр и проверка, что все видимые кнопки целиком в окне и не мельче 20 пикселей.</summary>
    private static BitmapSource RenderChecked(Window window, string state, List<string> problems)
    {
        var frame = Render(window);
        var content = (FrameworkElement)window.Content;
        var bounds = new Rect(0, 0, content.ActualWidth, content.ActualHeight);
        var buttons = Descendants(content).OfType<System.Windows.Controls.Button>()
            .Where(b => b.IsVisible && b.ActualWidth > 0).ToList();
        foreach (var b in buttons)
        {
            var r = b.TransformToAncestor(content).TransformBounds(new Rect(0, 0, b.ActualWidth, b.ActualHeight));
            var name = $"{state} {window.Width}x{window.Height}: «{Label(b)}»";
            if (!bounds.Contains(r)) problems.Add($"{name} выходит за окно ({r})");
            if (r.Width < 20 || r.Height < 20) problems.Add($"{name} {r.Width:0}x{r.Height:0} - мельче 20 пикселей");
            // Кнопка может стоять в окне, но не влезать в свою ячейку сетки: тогда ячейка
            // её обрезает. Так было с AC, ÷ и научными клавишами - у их стилей MinHeight 42.
            var slot = System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(b);
            if (b.ActualHeight + b.Margin.Top + b.Margin.Bottom > slot.Height + 0.5 ||
                b.ActualWidth + b.Margin.Left + b.Margin.Right > slot.Width + 0.5)
                problems.Add($"{name} {b.ActualWidth:0}x{b.ActualHeight:0} не влезает в свою ячейку {slot.Width:0}x{slot.Height:0}");
        }
        // Строки истории и результат на табло не обрезаются: без переноса длинный
        // результат в истории уходил за край, а на табло в узком окне от
        // 146623988754610578 оставалось «14662398875…».
        var texts = new List<System.Windows.Controls.TextBlock>();
        if (window.FindName("HistoryPanel") is FrameworkElement history && history.IsVisible)
            texts.AddRange(Descendants(history).OfType<System.Windows.Controls.TextBlock>());
        if (window.FindName("DisplayText") is System.Windows.Controls.TextBlock display && display.IsVisible)
            texts.Add(display);
        {
            foreach (var tb in texts.Where(t => t.IsVisible && t.TextWrapping == TextWrapping.NoWrap))
            {
                var text = new FormattedText(tb.Text, System.Globalization.CultureInfo.CurrentCulture, tb.FlowDirection,
                    new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch), tb.FontSize, Brushes.Black,
                    VisualTreeHelper.GetDpi(tb).PixelsPerDip);
                // Ширина на экране: TextBlock может стоять в Viewbox, который его уменьшает.
                var shown = tb.TransformToAncestor(window).TransformBounds(new Rect(0, 0, tb.ActualWidth, tb.ActualHeight)).Width;
                var needed = tb.TransformToAncestor(window).TransformBounds(new Rect(0, 0, text.Width, 1)).Width;
                if (needed > shown + 1)
                    problems.Add($"{state} {window.Width}x{window.Height}: «{tb.Text}» обрезан ({needed:0} > {shown:0})");
            }
        }
        Console.WriteLine($"  {state} {window.Width}x{window.Height}: {buttons.Count} кнопок проверено");
        return frame;
    }

    private static string Label(System.Windows.Controls.Button b) =>
        b.Content as string ?? b.ToolTip as string ?? b.Name;

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }

    /// <summary>Нажатия через команды ViewModel: цифры, операторы и имена функций через пробел.</summary>
    private static void Press(CalcViewModel vm, string keys)
    {
        foreach (var key in keys.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            ICommand command;
            object? parameter = key;
            switch (key)
            {
                case "=": command = vm.PressEqualsCommand; parameter = null; break;
                case "AC": command = vm.PressClearCommand; parameter = null; break;
                case "(" or ")": command = vm.PressParenCommand; break;
                case "+" or "-" or "*" or "/" or "^" or "%": command = vm.PressOperatorCommand; break;
                case "MS" or "MR" or "M+" or "M-" or "MC": command = vm.PressMemoryCommand; break;
                default:
                    command = char.IsDigit(key[0]) || key == "." ? vm.PressDigitCommand : vm.PressFunctionCommand;
                    break;
            }
            command.Execute(parameter);
            Wait(30);
        }
    }

    private static void Save(Window window, string folder, string name, CalcViewModel vm)
    {
        var bitmap = Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(Path.Combine(folder, name))) encoder.Save(file);
        Console.WriteLine($"  {name}: {vm.Expression} {vm.Display}");
    }

    /// <summary>Кадры сеткой: три в ряд, с отступами, на прозрачном фоне.</summary>
    private static void SaveGrid(IReadOnlyList<BitmapSource> frames, string folder, string name)
    {
        const int columns = 3, gap = 24;
        int w = frames[0].PixelWidth, h = frames[0].PixelHeight;
        var rows = (frames.Count + columns - 1) / columns;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            for (var i = 0; i < frames.Count; i++)
                dc.DrawImage(frames[i], new Rect(i % columns * (w + gap), i / columns * (h + gap), w, h));
        }
        var grid = new RenderTargetBitmap(columns * w + (columns - 1) * gap, rows * h + (rows - 1) * gap, 96, 96, PixelFormats.Pbgra32);
        grid.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(grid));
        using (var file = File.Create(Path.Combine(folder, name))) encoder.Save(file);
        Console.WriteLine($"  {name}");
    }

    private static BitmapSource Render(Window window)
    {
        Wait(700); // анимации результата и ширины научных колонок (260-380 мс) доигрывают
        // Рисуется клиентская часть: у ActualWidth окна в размер входит рамка Windows,
        // и в первом кадре справа и снизу оставались пустые полосы. Фон окна
        // подкладывается отдельно, он задан на самом Window, а не на содержимом.
        var content = (FrameworkElement)window.Content;
        var size = new Rect(0, 0, content.ActualWidth, content.ActualHeight);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(window.Background, null, size);
            // Viewbox явно: по умолчанию кисть берёт границы вместе с тенями кнопок,
            // сжимает содержимое, и по краю кадра выходила тёмная рамка.
            var brush = new VisualBrush(content) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = size };
            dc.DrawRectangle(brush, null, size);
        }
        var bitmap = new RenderTargetBitmap((int)Math.Round(size.Width * Scale), (int)Math.Round(size.Height * Scale),
            96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    /// <summary>Крутит цикл сообщений: без этого не пройдут ни привязки, ни раскладка, ни анимации.</summary>
    private static void Wait(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static string FindRepo()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "CalcPro.sln"))) return dir.FullName;
        throw new InvalidOperationException("CalcPro.sln не найден выше " + AppContext.BaseDirectory);
    }
}
