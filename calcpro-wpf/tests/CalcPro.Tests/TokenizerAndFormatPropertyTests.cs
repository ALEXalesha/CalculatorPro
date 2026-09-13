using System.Globalization;
using CalcPro.Core.Services;
using CalcPro.Tests.Support;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace CalcPro.Tests;

public class TokenizerAndFormatPropertyTests
{
    [Property(MaxTest = 2000)]
    public Property AnyNonNegativeDecimal_IsASingleNumberToken() =>
        Prop.ForAll(Arb.From(Gens.WideDecimal), raw =>
        {
            var d = Math.Abs(raw);
            var tokens = Tokenizer.Tokenize(d.ToString(CultureInfo.InvariantCulture));
            return tokens.Count == 2 && tokens[0].Kind == TokenKind.Number && tokens[0].Number == d &&
                   tokens[1].Kind == TokenKind.End;
        });

    [Property(MaxTest = 2000)]
    public Property TokenPositions_AreStrictlyIncreasing_AndEndMarksLength() =>
        Prop.ForAll(Arb.From(Gens.Ast(3)), ast =>
        {
            var text = ExpressionPrinter.Minimal(ast);
            var tokens = Tokenizer.Tokenize(text);
            var increasing = tokens.Zip(tokens.Skip(1)).All(p => p.First.Position < p.Second.Position);
            return increasing && tokens[^1].Kind == TokenKind.End && tokens[^1].Position == text.Length;
        });

    [Property(MaxTest = 1000)]
    public Property IdentifierCase_DoesNotMatter() =>
        Prop.ForAll(Arb.From(Gens.Ast(3)), ast =>
        {
            var text = ExpressionPrinter.Minimal(ast);
            Func<List<Token>, string> kinds = ts => string.Join(",", ts.Select(t => t.Kind + t.Text.ToLowerInvariant()));
            return kinds(Tokenizer.Tokenize(text.ToUpperInvariant())) == kinds(Tokenizer.Tokenize(text));
        });

    [Property(MaxTest = 2000)]
    public Property Format_RoundTripsThroughTryParse() =>
        Prop.ForAll(Arb.From(Gens.WideDecimal), v =>
            DisplayFormat.TryParse(DisplayFormat.Format(v), out var back) && back == Math.Round(v, 12));

    [Property(MaxTest = 2000)]
    public Property Format_IsCanonical() =>
        Prop.ForAll(Arb.From(Gens.WideDecimal), v =>
        {
            var s = DisplayFormat.Format(v);
            var fraction = s.Contains('.') ? s[(s.IndexOf('.') + 1)..] : "";
            return s != "-0" && !s.Contains('E') && !s.Contains(',') && !s.StartsWith('+') &&
                   fraction.Length <= 12 && !fraction.EndsWith('0');
        });

    [Property(MaxTest = 2000)]
    public Property Operand_EvaluatesBackToValue() =>
        Prop.ForAll(Arb.From(Gens.SignedDecimal), v =>
            Evaluator.Evaluate("1*" + DisplayFormat.Operand(v)) == v);

    [Theory]
    [InlineData("3.14", 3.14)]
    [InlineData("-0.5", -0.5)]
    [InlineData("0.", 0)]
    [InlineData("-0", 0)]
    [InlineData(".5", 0.5)]
    public void TryParse_AcceptsDisplayShapes(string text, double expected)
    {
        Assert.True(DisplayFormat.TryParse(text, out var v));
        Assert.Equal((decimal)expected, v);
    }

    [Theory]
    [InlineData("Error")]
    [InlineData("")]
    [InlineData("1e5")]
    [InlineData("1,5")]
    [InlineData(" 1")]
    public void TryParse_RejectsNonNumbers(string text) =>
        Assert.False(DisplayFormat.TryParse(text, out _));
}
