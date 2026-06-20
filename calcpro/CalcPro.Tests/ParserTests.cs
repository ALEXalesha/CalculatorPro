using CalcPro.Wpf.Services;
using Xunit;

namespace CalcPro.Tests;

public class ParserTests
{
    [Fact]
    public void OperatorPrecedence_MulBindsTighterThanAdd()
    {
        // 2 + 3 * 4 → (2 + (3 * 4))
        var ast = (BinaryOpNode)Parser.Parse("2+3*4");
        Assert.Equal("+", ast.Op);
        Assert.IsType<NumberNode>(ast.Left);
        Assert.IsType<BinaryOpNode>(ast.Right);
        var right = (BinaryOpNode)ast.Right;
        Assert.Equal("*", right.Op);
    }

    [Fact]
    public void Parens_OverridePrecedence()
    {
        // (2 + 3) * 4 → ((2 + 3) * 4)
        var ast = (BinaryOpNode)Parser.Parse("(2+3)*4");
        Assert.Equal("*", ast.Op);
        Assert.IsType<BinaryOpNode>(ast.Left);
        Assert.IsType<NumberNode>(ast.Right);
    }

    [Fact]
    public void PowerIsRightAssociative()
    {
        // 2^2^3 → 2^(2^3)
        var ast = (BinaryOpNode)Parser.Parse("2^2^3");
        Assert.Equal("^", ast.Op);
        Assert.IsType<NumberNode>(ast.Left);
        Assert.IsType<BinaryOpNode>(ast.Right);
        var right = (BinaryOpNode)ast.Right;
        Assert.Equal("^", right.Op);
    }

    [Fact]
    public void UnaryMinus_Parses()
    {
        var ast = Parser.Parse("-5");
        var unary = Assert.IsType<UnaryOpNode>(ast);
        Assert.Equal("-", unary.Op);
        Assert.IsType<NumberNode>(unary.Operand);
    }

    [Fact]
    public void TrailingOperator_Throws()
    {
        Assert.Throws<CalcParseException>(() => Parser.Parse("2+"));
    }

    [Fact]
    public void MissingClosingParen_Throws()
    {
        Assert.Throws<CalcParseException>(() => Parser.Parse("(1+2"));
    }

    [Fact]
    public void FunctionCall_Parses()
    {
        var ast = Parser.Parse("sqrt(16)");
        var fn = Assert.IsType<FunctionCallNode>(ast);
        Assert.Equal("sqrt", fn.Name);
    }
}
