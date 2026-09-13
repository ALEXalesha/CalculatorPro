using System.Globalization;
using CalcPro.Core.Models;
using CalcPro.Core.Services;
using Xunit;

namespace CalcPro.Tests;

/// <summary>Table of known answers, one row per behaviour worth pinning.</summary>
public class EvaluatorExampleTests
{
    [Theory]
    [InlineData("0", "0")]
    [InlineData("2+3", "5")]
    [InlineData("2+3*4", "14")]
    [InlineData("(2+3)*4", "20")]
    [InlineData("10-4-3", "3")]
    [InlineData("100/10/5", "2")]
    [InlineData("2^10", "1024")]
    [InlineData("2^3^2", "512")]
    [InlineData("2^-1", "0.5")]
    [InlineData("2^-2", "0.25")]
    [InlineData("-2^2", "-4")]
    [InlineData("(-2)^2", "4")]
    [InlineData("(-2)^3", "-8")]
    [InlineData("0^0", "1")]
    [InlineData("0^2", "0")]
    [InlineData("0^0.5", "0")]
    [InlineData("4^0.5", "2")]
    [InlineData("0.1+0.2", "0.3")]
    [InlineData("0.1*3", "0.3")]
    [InlineData("1-0.9", "0.1")]
    [InlineData("10/3", "3.333333333333")]
    [InlineData("2/3", "0.666666666667")]
    [InlineData("1/8", "0.125")]
    [InlineData("50%", "0.5")]
    [InlineData("200*10%", "20")]
    [InlineData("50%%", "0.005")]
    [InlineData("0!", "1")]
    [InlineData("1!", "1")]
    [InlineData("5!", "120")]
    [InlineData("3!!", "720")]
    [InlineData("-3!", "-6")]
    [InlineData("2^3!", "64")]
    [InlineData("27!", "10888869450418352160768000000")]
    [InlineData("sqrt(16)", "4")]
    [InlineData("sqrt(2)", "1.414213562373")]
    [InlineData("sqr(-3)", "9")]
    [InlineData("abs(-7.5)", "7.5")]
    [InlineData("inv(4)", "0.25")]
    [InlineData("log(1000)", "3")]
    [InlineData("ln(1)", "0")]
    [InlineData("exp(0)", "1")]
    [InlineData("SQRT(9)", "3")]
    [InlineData("2×3÷4", "1.5")]
    [InlineData("  7  ", "7")]
    [InlineData(".5+.5", "1")]
    [InlineData("5.", "5")]
    [InlineData("0.1234567890123456+0", "0.123456789012")]
    public void Evaluates(string expression, string expected) =>
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), Evaluator.Evaluate(expression));

    [Theory]
    [InlineData("sin(0)", 0)]
    [InlineData("sin(30)", 0.5)]
    [InlineData("sin(90)", 1)]
    [InlineData("sin(180)", 0)]
    [InlineData("cos(60)", 0.5)]
    [InlineData("cos(90)", 0)]
    [InlineData("tan(45)", 1)]
    [InlineData("asin(1)", 90)]
    [InlineData("acos(0)", 90)]
    [InlineData("atan(1)", 45)]
    public void Trigonometry_InDegrees(string expression, double expected) =>
        Assert.Equal((decimal)expected, Evaluator.Evaluate(expression, AngleMode.Deg));

    [Theory]
    [InlineData("sin(pi/2)", 1)]
    [InlineData("cos(pi)", -1)]
    [InlineData("tan(0)", 0)]
    [InlineData("atan(1)*4", 3.14159265359)]
    public void Trigonometry_InRadians(string expression, double expected) =>
        Assert.Equal((decimal)expected, Math.Round(Evaluator.Evaluate(expression, AngleMode.Rad), 11));

    [Theory]
    [InlineData("1/0")]
    [InlineData("5/(2-2)")]
    [InlineData("inv(0)")]
    [InlineData("sqrt(-1)")]
    [InlineData("ln(0)")]
    [InlineData("ln(-1)")]
    [InlineData("log(0)")]
    [InlineData("asin(2)")]
    [InlineData("acos(-1.5)")]
    [InlineData("tan(90)")]
    [InlineData("tan(270)")]
    [InlineData("0^-1")]
    [InlineData("0^-0.5")]
    [InlineData("(-8)^0.5")]
    [InlineData("2.5!")]
    [InlineData("(-1)!")]
    [InlineData("28!")]
    [InlineData("exp(100)")]
    [InlineData("10^28*10")]
    [InlineData("99999999999999^9")]
    [InlineData("79228162514264337593543950335+1")]
    [InlineData("1/0.0000000000001^3")]
    public void MathErrors_AreReportedNotCrashed(string expression) =>
        Assert.Throws<CalcEvalException>(() => Evaluator.Evaluate(expression));
}
