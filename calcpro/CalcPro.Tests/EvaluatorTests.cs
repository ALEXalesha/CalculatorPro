using CalcPro.Wpf.Models;
using CalcPro.Wpf.Services;
using Xunit;

namespace CalcPro.Tests;

public class EvaluatorTests
{
    [Fact]
    public void BasicArithmetic_RespectsPrecedence()
    {
        Assert.Equal(14m, Evaluator.Evaluate("2 + 3 * 4"));
    }

    [Fact]
    public void Parens_OverridePrecedence()
    {
        Assert.Equal(20m, Evaluator.Evaluate("(2 + 3) * 4"));
    }

    [Fact]
    public void DecimalPrecision_NoFloatingPointArtifacts()
    {
        // The whole point of using decimal: this must be exactly 0.3.
        Assert.Equal(0.3m, Evaluator.Evaluate("0.1 + 0.2"));
    }

    [Fact]
    public void Division_ProducesRoundedDecimal()
    {
        var v = Evaluator.Evaluate("10 / 3");
        // Rounded to 12 digits
        Assert.Equal(3.333333333333m, v);
    }

    [Fact]
    public void Power_IsRightAssociative()
    {
        // 2^2^3 = 2^(2^3) = 2^8 = 256
        Assert.Equal(256m, Evaluator.Evaluate("2^2^3"));
    }

    [Fact]
    public void IntegerPower_PreservesDecimalPrecision()
    {
        Assert.Equal(1024m, Evaluator.Evaluate("2^10"));
    }

    [Fact]
    public void DivisionByZero_Throws()
    {
        Assert.Throws<CalcEvalException>(() => Evaluator.Evaluate("1/0"));
    }

    [Fact]
    public void UnaryMinus_Works()
    {
        Assert.Equal(-7m, Evaluator.Evaluate("-3 - 4"));
    }

    [Fact]
    public void Sqrt_OfSixteen_IsFour()
    {
        Assert.Equal(4m, Evaluator.Evaluate("sqrt(16)"));
    }

    [Fact]
    public void SinPiOver2_InRadians_IsOne()
    {
        var v = Evaluator.Evaluate("sin(pi/2)", AngleMode.Rad);
        Assert.Equal(1m, v);
    }

    [Fact]
    public void SinNinetyDegrees_IsOne()
    {
        var v = Evaluator.Evaluate("sin(90)", AngleMode.Deg);
        Assert.Equal(1m, v);
    }

    [Fact]
    public void Percent_DividesBy100()
    {
        Assert.Equal(0.5m, Evaluator.Evaluate("50%"));
    }

    [Fact]
    public void Factorial_OfFive_IsOneTwenty()
    {
        Assert.Equal(120m, Evaluator.Evaluate("5!"));
    }

    [Fact]
    public void UnaryMinusOfFactorial_IsNegativeFactorial()
    {
        // -3! parses as -(3!) because postfix ! (lbp=95) binds tighter
        // than unary minus (rbp=90). Math-correct: -(6) = -6.
        Assert.Equal(-6m, Evaluator.Evaluate("-3!"));
    }

    [Fact]
    public void FactorialOfNegativeParen_Throws()
    {
        Assert.Throws<CalcEvalException>(() => Evaluator.Evaluate("(-3)!"));
    }

    [Fact]
    public void LogTen_IsOne()
    {
        Assert.Equal(1m, Evaluator.Evaluate("log(10)"));
    }
}
