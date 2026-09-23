using System.Windows;
using CalcPro.Wpf.Services;

namespace CalcPro.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Тема ставится до первого окна: иначе оно моргнёт «Стеклянной» и только потом
        // перекрасится в выбранную.
        ThemeService.ApplySaved();
    }
}
