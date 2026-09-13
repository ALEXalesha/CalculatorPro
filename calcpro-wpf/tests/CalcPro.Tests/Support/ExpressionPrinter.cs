using System.Globalization;
using CalcPro.Core.Services;

namespace CalcPro.Tests.Support;

/// <summary>
/// Turns an AST back into text, either fully parenthesised or with the minimum
/// parentheses the Pratt parser needs. Parsing the minimal form must give back
/// the same tree — that single property pins down every precedence and
/// associativity rule of the grammar.
/// </summary>
public static class ExpressionPrinter
{
    private const int Inf = int.MaxValue;
    private const int UnaryRbp = 90;
    private const int PostfixLbp = 110;

    public static string Full(AstNode node) => node switch
    {
        NumberNode n => Number(n.Value),
        ConstantNode c => c.Name,
        UnaryOpNode u => $"(-{Full(u.Operand)})",
        BinaryOpNode b => $"({Full(b.Left)}{b.Op}{Full(b.Right)})",
        FunctionCallNode f => $"{f.Name}({Full(f.Argument)})",
        PercentNode p => $"({Full(p.Operand)}%)",
        FactorialNode f => $"({Full(f.Operand)}!)",
        _ => throw new ArgumentOutOfRangeException(nameof(node))
    };

    public static string Minimal(AstNode node) => Print(node).Text;

    /// <param name="Top">Binding power of the construct's top-level infix/postfix
    /// operator (∞ for atoms and prefix forms). A parent parsing this with rbp
    /// ≥ Top would cut it short, so parentheses are needed.</param>
    /// <param name="Trail">Smallest rbp still "open" on the right edge. An operator
    /// with lbp above it that follows this text would be swallowed by it.</param>
    private sealed record Printed(string Text, int Top, int Trail);

    private static Printed Print(AstNode node) => node switch
    {
        NumberNode n => new(Number(n.Value), Inf, Inf),
        ConstantNode c => new(c.Name, Inf, Inf),
        FunctionCallNode f => new($"{f.Name}({Print(f.Argument).Text})", Inf, Inf),
        UnaryOpNode u => Unary(u),
        PercentNode p => Postfix(p.Operand, "%"),
        FactorialNode f => Postfix(f.Operand, "!"),
        BinaryOpNode b => Binary(b),
        _ => throw new ArgumentOutOfRangeException(nameof(node))
    };

    private static Printed Paren(Printed p) => new("(" + p.Text + ")", Inf, Inf);

    private static Printed Unary(UnaryOpNode u)
    {
        var x = Print(u.Operand);
        if (x.Top <= UnaryRbp) x = Paren(x);
        return new("-" + x.Text, Inf, Math.Min(UnaryRbp, x.Trail));
    }

    private static Printed Postfix(AstNode operand, string symbol)
    {
        var x = Print(operand);
        if (x.Trail < PostfixLbp) x = Paren(x);
        return new(x.Text + symbol, PostfixLbp, Inf);
    }

    private static Printed Binary(BinaryOpNode b)
    {
        var (lbp, rbp) = b.Op switch
        {
            "+" or "-" => (70, 70),
            "*" or "/" => (80, 80),
            "^" => (100, 99),
            _ => throw new ArgumentOutOfRangeException(nameof(b))
        };
        var left = Print(b.Left);
        if (left.Trail < lbp) left = Paren(left);
        var right = Print(b.Right);
        if (right.Top <= rbp) right = Paren(right);
        return new(left.Text + b.Op + right.Text, lbp, Math.Min(rbp, right.Trail));
    }

    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
