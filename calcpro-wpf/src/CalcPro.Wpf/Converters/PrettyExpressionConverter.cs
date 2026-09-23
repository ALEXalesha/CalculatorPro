using System.Globalization;
using System.Windows.Data;
using CalcPro.Core.Services;

namespace CalcPro.Wpf.Converters;

/// <summary>Expression text with the operators as the buttons show them: × ÷ −.</summary>
public sealed class PrettyExpressionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s ? DisplayFormat.Pretty(s) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
