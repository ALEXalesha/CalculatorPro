using Microsoft.UI.Xaml;

namespace CalcWinUI;

/// <summary>
/// Корень приложения. Создаёт окно при запуске.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
