using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CalcWinUI;

/// <summary>
/// Связывает CalculatorEngine с UI. Один обработчик OnButtonClick читает
/// Tag и диспатчит в соответствующий метод движка, потом перерисовывает экран.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly CalculatorEngine _engine = new();

    private static readonly Color OrangeColor = Color.FromArgb(255, 255, 159, 10);
    private static readonly Brush ActiveOpBackground = new SolidColorBrush(Colors.White);
    private static readonly Brush DefaultOpBackground = new SolidColorBrush(OrangeColor);
    private static readonly Brush ActiveOpForeground = new SolidColorBrush(OrangeColor);
    private static readonly Brush DefaultOpForeground = new SolidColorBrush(Colors.White);

    public MainPage()
    {
        InitializeComponent();
        Render();
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;

        // Форматы Tag:
        //   "d:7"   — цифра 7 (или "d:," — десятичная)
        //   "op:+"  — оператор + (включая "=")
        //   "clear" / "bksp" / "neg" / "pct"
        if (tag.StartsWith("d:"))
        {
            _engine.InputDigit(tag[2..]);
        }
        else if (tag.StartsWith("op:"))
        {
            _engine.InputOperator(tag[3..]);
        }
        else
        {
            switch (tag)
            {
                case "clear": _engine.ClearEntry(); break;
                case "bksp":  _engine.Backspace();  break;
                case "neg":   _engine.Negate();     break;
                case "pct":   _engine.ApplyPercent(); break;
            }
        }

        Render();
    }

    private void Render()
    {
        ExpressionText.Text = _engine.LastExpression;
        ResultText.Text     = _engine.CurrentExpr;
        ClearBtn.Content    = _engine.ClearLabel;

        // Подсветка активного оператора (когда currentExpr заканчивается на " op ").
        var op = _engine.HighlightedOperator;
        SetOpVisual(OpDiv, op == "/");
        SetOpVisual(OpMul, op == "*");
        SetOpVisual(OpSub, op == "-");
        SetOpVisual(OpAdd, op == "+");

        // Адаптивный размер шрифта для длинных результатов.
        var len = _engine.CurrentExpr.Length;
        ResultText.FontSize = len > 16 ? 44 : len > 11 ? 56 : 72;
    }

    private static void SetOpVisual(Button btn, bool active)
    {
        btn.Background = active ? ActiveOpBackground : DefaultOpBackground;
        btn.Foreground = active ? ActiveOpForeground : DefaultOpForeground;
    }
}
