using CalcPro.Core.Models;
using CalcPro.Tests.Support;
using Xunit;

namespace CalcPro.Tests;

/// <summary>
/// Button-by-button scenarios. Keys are space separated; numbers are typed digit
/// by digit. Each row documents one user-visible behaviour.
/// </summary>
public class CommandScenarioTests
{
    [Theory]
    [InlineData("2 + 3 =", "5")]
    [InlineData("2 + 3 * 4 =", "14")]
    [InlineData("( 2 + 3 ) * 4 =", "20")]
    [InlineData("( 2 + 3 ) =", "5")]
    [InlineData("( 2 + 3 ) 4 =", "20")]
    [InlineData("2 ( 3 ) =", "6")]
    [InlineData("( ( 1 + 2 =", "3")]
    [InlineData("( 1 + 2 ) ( 3 + 4 ) =", "21")]
    [InlineData("( ) =", "0")]
    [InlineData(") ) 5 =", "5")]
    [InlineData("5 * ( =", "5")]
    [InlineData("2 ( + 3 =", "6")]
    [InlineData("* 5 =", "0")]
    [InlineData("2 + + * 3 =", "6")]
    [InlineData("50 % =", "0.5")]
    [InlineData("200 * 10 % =", "20")]
    [InlineData("25 % + 1 =", "1.25")]
    [InlineData("( 2 + 3 ) % =", "0.05")]
    [InlineData("2 + 3 = + 1 =", "6")]
    [InlineData("2 + 3 = ± + 1 =", "-4")]
    [InlineData("1 + 2 = 5 =", "5")]
    [InlineData("2 * ± 3 =", "-6")]
    [InlineData("± 7 =", "-7")]
    [InlineData("7 ± ± =", "7")]
    [InlineData("0.1 + 0.2 =", "0.3")]
    [InlineData("10 / 3 =", "3.333333333333")]
    [InlineData("2 ^ 10 =", "1024")]
    [InlineData("2 ^ 3 ^ 2 =", "512")]
    [InlineData("9 sqrt =", "3")]
    [InlineData("16 = sqrt", "4")]
    [InlineData("( 16 ) sqrt =", "4")]
    [InlineData("( 9 + 7 ) sqrt + 1 =", "5")]
    [InlineData("3 sqr sqr =", "81")]
    [InlineData("12 BS =", "1")]
    [InlineData("5 BS BS 7 =", "7")]
    [InlineData("1.5 BS BS 2 =", "12")]
    [InlineData("2 + CE 3 =", "5")]
    [InlineData("2 + 3 CE 4 =", "6")]
    [InlineData("2 + 3 = CE", "0")]
    [InlineData("3 M+ AC MR * 2 =", "6")]
    [InlineData("4 MS AC 1 M- AC MR =", "3")]
    [InlineData("4 MS 1 =", "41")]
    [InlineData("( 2 + 3 ) MR =", "0")]
    [InlineData("2 + 3 = MR", "0")]
    [InlineData("0 0 0 7 =", "7")]
    [InlineData(". 5 + . 5 =", "1")]
    [InlineData("1 . . 5 =", "1.5")]
    [InlineData("v:-2.5 * 2 =", "-5")]
    [InlineData("2 + v:-2.5 =", "-0.5")]
    public void Scenario(string keys, string expected) =>
        Assert.Equal(expected, new Calculator().Press(keys).State.Display);

    [Theory]
    [InlineData("1 / 0 =")]
    [InlineData("± 4 sqrt")]
    [InlineData("0 ln")]
    [InlineData("0 inv")]
    [InlineData("( 1 - 1 ) inv")]
    [InlineData("2 = ± sqrt")]
    [InlineData("1 0 0 exp")]
    public void MathErrors_ShowError(string keys)
    {
        var calc = new Calculator().Press(keys);
        Assert.Equal("Error", calc.State.Display);
        Assert.Equal(CalcPhase.AfterEquals, calc.State.Phase);
    }

    [Theory]
    [InlineData("1 / 0 = 5 =", "5")]
    [InlineData("1 / 0 = + 5 =", "5")]
    [InlineData("1 / 0 = AC 2 =", "2")]
    [InlineData("1 / 0 = CE 2 =", "2")]
    public void AfterError_TheCalculatorRecovers(string keys, string expected) =>
        Assert.Equal(expected, new Calculator().Press(keys).State.Display);

    [Theory]
    [InlineData("2 + 3", "2 + ")]
    [InlineData("2 + 3 =", "2 + 3 =")]
    [InlineData("( 2 + 3 )", "(2 + 3)")]
    [InlineData("( 2 + 3 ) 4", "(2 + 3) * ")]
    [InlineData("2 * ± 3 =", "2 * (-3) =")]
    [InlineData("16 = sqrt", "sqrt(16) =")]
    [InlineData("( 16 ) sqrt", "sqrt(16)")]
    [InlineData("50 %", "50 %")]
    [InlineData("( ( 1 =", "((1)) =")]
    public void ExpressionLine_ShowsWhatWillBeEvaluated(string keys, string expected) =>
        Assert.Equal(expected, new Calculator().Press(keys).State.Expression);

    [Fact]
    public void DisplayLength_IsCapped()
    {
        var calc = new Calculator().Press(new string('9', 60));
        Assert.Equal(24, calc.State.Display.Length);
    }

    [Fact]
    public void ClosingAGroup_ShowsItsValue()
    {
        var calc = new Calculator().Press("( 2 + 3 )");
        Assert.Equal("5", calc.State.Display);
    }

    [Fact]
    public void EqualsTwice_DoesNotDuplicateHistory()
    {
        var calc = new Calculator().Press("2 + 3 = = =");
        Assert.Single(calc.History.Entries);
    }

    [Fact]
    public void Undo_RemovesTheHistoryEntryOfEquals()
    {
        var calc = new Calculator().Press("2 + 3 =");
        calc.History.Undo(calc.State);
        Assert.Empty(calc.History.Entries);
        Assert.Equal("3", calc.State.Display);
        Assert.Null(calc.State.LastResult);
    }
}
