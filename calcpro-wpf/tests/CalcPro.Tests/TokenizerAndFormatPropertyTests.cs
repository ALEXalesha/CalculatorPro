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

    // Строка выражения и история показывали "*" и "/", хотя на кнопках × и ÷:
    // нашлось на кадре для README. Pretty - только для экрана, но текст после него
    // должен читаться движком так же, как до.
    [Property(MaxTest = 2000)]
    public Property Pretty_TokenizesExactlyLikeTheOriginal() =>
        Prop.ForAll(Arb.From(Gens.Ast(3)), ast =>
        {
            var text = ExpressionPrinter.Full(ast);
            Func<List<Token>, string> all = ts => string.Join(",", ts.Select(t => $"{t.Kind}:{t.Text}:{t.Number}@{t.Position}"));
            var pretty = DisplayFormat.Pretty(text);
            return (all(Tokenizer.Tokenize(pretty)) == all(Tokenizer.Tokenize(text)))
                .Label($"{text} -> {pretty}")
                .And(!pretty.Any(c => c is '*' or '/' or '-'))
                .And(pretty.Length == text.Length);
        });

    [Theory]
    [InlineData("12 * 3 / (-4)", "12 × 3 ÷ (−4)")]
    [InlineData("(2 + 3) * 4", "(2 + 3) × 4")]
    [InlineData("0.1 + 0.2", "0.1 + 0.2")]
    [InlineData("", "")]
    public void Pretty_UsesTheButtonSymbols(string text, string expected) =>
        Assert.Equal(expected, DisplayFormat.Pretty(text));

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
