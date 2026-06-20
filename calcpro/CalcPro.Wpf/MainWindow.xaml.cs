using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using CalcPro.Wpf.ViewModels;

namespace CalcPro.Wpf;

/// <summary>
/// View code-behind is intentionally thin: it owns only purely-visual concerns
/// that don't belong in the ViewModel — bubble decoration, animation triggers,
/// and scientific-mode column width animation. All state mutation flows through
/// the bound CalcViewModel commands.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly string[] BubbleHues = { "violet", "pink", "cyan", "green" };
    private static readonly Random Rng = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += (_, _) => RebuildBubbles();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CalcViewModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
        RebuildBubbles();
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

    private void RebuildBubbles()
    {
        BubblesCanvas.Children.Clear();
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        const int count = 10;
        for (var i = 0; i < count; i++)
        {
            var hue = BubbleHues[i % BubbleHues.Length];
            var size = 70.0 + Rng.NextDouble() * 90.0;
            var leftPct = Rng.NextDouble();
            var dur = 18.0 + Rng.NextDouble() * 14.0;
            var delay = -Rng.NextDouble() * dur;
            var drift = (Rng.NextDouble() - 0.5) * 120.0;

            var ellipse = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = (Brush)FindResource($"Bubble{Capitalize(hue)}Fill"),
                IsHitTestVisible = false,
                Opacity = 0
            };
            Canvas.SetLeft(ellipse, leftPct * Math.Max(0, w - size));
            Canvas.SetTop(ellipse, h + 100);

            var translate = new TranslateTransform();
            ellipse.RenderTransform = translate;

            BubblesCanvas.Children.Add(ellipse);
            AnimateBubble(ellipse, translate, h + 100, drift, dur, delay);
        }
    }

    private static void AnimateBubble(Ellipse e, TranslateTransform t, double startY, double drift,
        double durationSec, double delaySec)
    {
        var dy = -(startY + 220);

        var yAnim = new DoubleAnimation
        {
            From = 0,
            To = dy,
            Duration = TimeSpan.FromSeconds(durationSec),
            BeginTime = TimeSpan.FromSeconds(delaySec),
            RepeatBehavior = RepeatBehavior.Forever
        };
        var xAnim = new DoubleAnimation
        {
            From = 0,
            To = drift,
            Duration = TimeSpan.FromSeconds(durationSec),
            BeginTime = TimeSpan.FromSeconds(delaySec),
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true
        };
        var op = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(durationSec),
            BeginTime = TimeSpan.FromSeconds(delaySec),
            RepeatBehavior = RepeatBehavior.Forever
        };
        op.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        op.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromPercent(0.1)));
        op.KeyFrames.Add(new LinearDoubleKeyFrame(0.7, KeyTime.FromPercent(0.85)));
        op.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));

        t.BeginAnimation(TranslateTransform.YProperty, yAnim);
        t.BeginAnimation(TranslateTransform.XProperty, xAnim);
        e.BeginAnimation(OpacityProperty, op);
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
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
