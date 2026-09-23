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

        window.Close();
        return 0;
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
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var path = Path.Combine(folder, name);
        using (var file = File.Create(path)) encoder.Save(file);
        Console.WriteLine($"  {name}: {vm.Expression} {vm.Display}");
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
