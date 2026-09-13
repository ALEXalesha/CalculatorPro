using CalcPro.Core.Services;
using CalcPro.Tests.Support;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace CalcPro.Tests;

/// <summary>
/// Hypotheses about the grammar, checked against thousands of random trees.
/// </summary>
public class ParserPropertyTests
{
    private static readonly Arbitrary<AstNode> Trees = Arb.From(Gens.Ast(4));

    [Property(MaxTest = 2000)]
    public Property FullyParenthesised_RoundTrips() =>
        Prop.ForAll(Trees, ast => Parser.Parse(ExpressionPrinter.Full(ast)).Equals(ast));

    [Property(MaxTest = 3000)]
    public Property MinimallyParenthesised_RoundTrips() =>
        // The minimal printer only adds parens where precedence/associativity
        // demand them, so this pins down the whole binding-power table.
        Prop.ForAll(Trees, ast => Parser.Parse(ExpressionPrinter.Minimal(ast)).Equals(ast));

    [Property(MaxTest = 1000)]
    public Property MinimalForm_IsNeverLongerThanFullForm() =>
        Prop.ForAll(Trees, ast => ExpressionPrinter.Minimal(ast).Length <= ExpressionPrinter.Full(ast).Length);

    [Property(MaxTest = 1000)]
    public Property RedundantOuterParens_DoNotChangeTree() =>
        Prop.ForAll(Trees, Arb.From(Gen.Choose(1, 20)), (ast, n) =>
        {
            var text = new string('(', n) + ExpressionPrinter.Minimal(ast) + new string(')', n);
            return Parser.Parse(text).Equals(ast);
        });

    [Property(MaxTest = 1000)]
    public Property Whitespace_IsInsignificant() =>
        Prop.ForAll(Trees, ast =>
        {
            var text = ExpressionPrinter.Minimal(ast);
            // Spaces may go anywhere except inside numbers and identifiers.
            var spaced = string.Concat(text.Select((c, i) =>
                i > 0 && IsWordChar(text[i - 1]) && IsWordChar(c) ? c.ToString() : " " + c));
            return Parser.Parse(spaced + "   ").Equals(ast);
        });

    [Property(MaxTest = 1000)]
    public Property UnicodeOperators_AreEquivalent() =>
        Prop.ForAll(Trees, ast =>
        {
            var text = ExpressionPrinter.Minimal(ast).Replace('*', '×').Replace('/', '÷');
            return Parser.Parse(text).Equals(ast);
        });

    [Property(MaxTest = 3000)]
    public Property NoisyInput_OnlyEverThrowsParseException() =>
        Prop.ForAll(Arb.From(Gens.NoisyExpression), text =>
        {
            try { Parser.Parse(text); return true; }
            catch (CalcParseException) { return true; }
        });

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(Parser.MaxDepth - 1)]
    public void NestingBelowLimit_Parses(int depth)
    {
        var text = new string('(', depth) + "1" + new string(')', depth);
        Assert.Equal(new NumberNode(1m), Parser.Parse(text));
    }

    [Theory]
    [InlineData(Parser.MaxDepth + 1)]
    [InlineData(100_000)]
    public void DeepNesting_IsRejectedWithoutStackOverflow(int depth)
    {
        var text = new string('(', depth) + "1" + new string(')', depth);
        Assert.Throws<CalcParseException>(() => Parser.Parse(text));
    }

    [Fact]
    public void LongUnaryMinusChain_IsRejectedWithoutStackOverflow() =>
        Assert.Throws<CalcParseException>(() => Parser.Parse(new string('-', 100_000) + "1"));

    [Fact]
    public void LongFlatSum_WithinTokenLimit_Evaluates()
    {
        // 400 terms = 799 tokens: well past MaxDepth, fine because the chain is flat.
        var text = string.Join("+", Enumerable.Repeat("1", 400));
        Assert.Equal(400m, new Evaluator().Eval(Parser.Parse(text)));
    }

    [Fact]
    public void HugeFlatSum_IsRejected_InsteadOfOverflowingTheEvaluatorStack()
    {
        var text = string.Join("+", Enumerable.Repeat("1", 100_000));
        Assert.Throws<CalcParseException>(() => Parser.Parse(text));
    }

    [Theory]
    [InlineData("2^3!", "(2^(3!))")]
    [InlineData("-3!", "(-(3!))")]
    [InlineData("-2^2", "(-(2^2))")]
    [InlineData("2^-2", "(2^(-2))")]
    [InlineData("2^3^2", "(2^(3^2))")]
    [InlineData("8-3-2", "((8-3)-2)")]
    [InlineData("8/4/2", "((8/4)/2)")]
    [InlineData("2*3+4*5", "((2*3)+(4*5))")]
    [InlineData("50%*2", "((50%)*2)")]
    [InlineData("2+50%", "(2+(50%))")]
    [InlineData("--5", "(-(-5))")]
    [InlineData("+5", "5")]
    [InlineData("sqrt(4)^2", "((sqrt(4))^2)")]
    [InlineData("3!!", "((3!)!)")]
    public void Precedence_MatchesFullyParenthesisedForm(string text, string full) =>
        Assert.Equal(Parser.Parse(full), Parser.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("(")]
    [InlineData(")")]
    [InlineData("()")]
    [InlineData("1+")]
    [InlineData("*1")]
    [InlineData("1 2")]
    [InlineData("sin")]
    [InlineData("sin 30")]
    [InlineData("sin()")]
    [InlineData("pi(2)")]
    [InlineData("2(3)")]
    [InlineData("(1))")]
    [InlineData("1..2")]
    [InlineData("%")]
    [InlineData("!")]
    [InlineData("1 = 1")]
    public void MalformedInput_Throws(string text) =>
        Assert.Throws<CalcParseException>(() => Parser.Parse(text));

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '.';
}
