using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CalcPro.Wpf.ViewModels;

namespace CalcPro.Wpf;

/// <summary>
/// View code-behind is intentionally thin: it owns only purely-visual concerns
/// that don't belong in the ViewModel — the result animation and the
/// scientific-mode column width animation. All state mutation flows through
/// the bound CalcViewModel commands.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CalcViewModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CalcViewModel vm) return;
        switch (e.PropertyName)
        {
            case nameof(CalcViewModel.IsScientific):
                AnimateScientificColumns(vm.IsScientific);
                break;
            case nameof(CalcViewModel.ResultAnimationTrigger):
                PlayResultAnimation();
                break;
        }
    }

    private void AnimateScientificColumns(bool show)
    {
        var target = show ? 60.0 : 0.0;
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
            Color = Color.FromRgb(0x5B, 0x8D, 0xEF),
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
