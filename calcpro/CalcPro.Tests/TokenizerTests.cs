using CalcPro.Wpf.Services;
using Xunit;

namespace CalcPro.Tests;

public class TokenizerTests
{
    [Fact]
    public void SimpleArithmetic_ProducesExpectedTokens()
    {
        var tokens = Tokenizer.Tokenize("2+3*4");
        Assert.Equal(TokenKind.Number, tokens[0].Kind);
        Assert.Equal(2m, tokens[0].Number);
        Assert.Equal(TokenKind.Plus, tokens[1].Kind);
        Assert.Equal(TokenKind.Number, tokens[2].Kind);
        Assert.Equal(3m, tokens[2].Number);
        Assert.Equal(TokenKind.Star, tokens[3].Kind);
        Assert.Equal(TokenKind.Number, tokens[4].Kind);
        Assert.Equal(4m, tokens[4].Number);
        Assert.Equal(TokenKind.End, tokens[5].Kind);
    }

    [Fact]
    public void DecimalNumbers_AreParsed()
    {
        var tokens = Tokenizer.Tokenize("3.14");
        Assert.Equal(TokenKind.Number, tokens[0].Kind);
        Assert.Equal(3.14m, tokens[0].Number);
    }

    [Fact]
    public void TwoDots_ThrowsParseError()
    {
        Assert.Throws<CalcParseException>(() => Tokenizer.Tokenize("3.14.5"));
    }

    [Fact]
    public void FunctionsAndConstants_AreRecognised()
    {
        var tokens = Tokenizer.Tokenize("sin(pi)");
        Assert.Equal(TokenKind.Function, tokens[0].Kind);
        Assert.Equal("sin", tokens[0].Text);
        Assert.Equal(TokenKind.LParen, tokens[1].Kind);
        Assert.Equal(TokenKind.Constant, tokens[2].Kind);
        Assert.Equal("pi", tokens[2].Text);
        Assert.Equal(TokenKind.RParen, tokens[3].Kind);
    }

    [Fact]
    public void UnknownIdentifier_Throws()
    {
        Assert.Throws<CalcParseException>(() => Tokenizer.Tokenize("foo(2)"));
    }

    [Fact]
    public void UnicodeOperators_AreMapped()
    {
        var tokens = Tokenizer.Tokenize("2×3÷4");
        Assert.Equal(TokenKind.Star, tokens[1].Kind);
        Assert.Equal(TokenKind.Slash, tokens[3].Kind);
    }
}
