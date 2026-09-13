using CalcPro.Core.Services;
using FsCheck;

namespace CalcPro.Tests.Support;

/// <summary>FsCheck generators shared by the property tests.</summary>
public static class Gens
{
    public static readonly string[] Functions =
        { "sin", "cos", "tan", "asin", "acos", "atan", "log", "ln", "exp", "sqrt", "abs", "sqr", "inv" };

    /// Non-negative decimal with up to 4 fractional digits, below 10 000.
    public static Gen<decimal> SmallDecimal =>
        from mantissa in Gen.Choose(0, 99_999_999)
        from scale in Gen.Choose(0, 4)
        select new decimal(mantissa, 0, 0, false, (byte)scale);

    public static Gen<decimal> SignedDecimal =>
        from d in SmallDecimal
        from negative in Gen.Elements(true, false)
        select negative ? -d : d;

    public static Gen<decimal> SmallInt =>
        Gen.Choose(-1000, 1000).Select(i => (decimal)i);

    public static Gen<decimal> NonZeroInt =>
        Gen.Choose(1, 1000).SelectMany(i => Gen.Elements((decimal)i, -(decimal)i));

    /// Anything a user could plausibly produce, including values near decimal's limits.
    public static Gen<decimal> WideDecimal =>
        Gen.OneOf(
            SignedDecimal,
            from hi in Gen.Choose(0, int.MaxValue)
            from scale in Gen.Choose(0, 28)
            from negative in Gen.Elements(true, false)
            select new decimal(hi, hi, hi, negative, (byte)scale));

    /// Random expression trees. Numbers are non-negative (a literal "-5" parses as
    /// unary minus), unary plus is omitted (the parser does not keep it in the AST).
    public static Gen<AstNode> Ast(int depth, Gen<decimal>? numbers = null)
    {
        numbers ??= SmallDecimal;
        var leaf = Gen.OneOf(
            numbers.Select(d => (AstNode)new NumberNode(d)),
            numbers.Select(d => (AstNode)new NumberNode(d)),
            numbers.Select(d => (AstNode)new NumberNode(d)),
            Gen.Elements("pi", "e").Select(n => (AstNode)new ConstantNode(n)));
        if (depth <= 0) return leaf;

        var sub = Ast(depth - 1, numbers);
        var binary =
            from op in Gen.Elements("+", "-", "*", "/", "^")
            from l in sub
            from r in sub
            select (AstNode)new BinaryOpNode(op, l, r);

        return Gen.OneOf(
            leaf, leaf,
            binary, binary, binary,
            sub.Select(x => (AstNode)new UnaryOpNode("-", x)),
            from f in Gen.Elements(Functions)
            from x in sub
            select (AstNode)new FunctionCallNode(f, x),
            sub.Select(x => (AstNode)new PercentNode(x)),
            sub.Select(x => (AstNode)new FactorialNode(x)));
    }

    /// Random strings over the calculator alphabet, including garbage.
    public static Gen<string> NoisyExpression
    {
        get
        {
            var pieces = new[]
            {
                "0", "1", "2", "3", "7", "9", "12", "0.5", ".", "..", "1e5",
                "+", "-", "*", "/", "^", "%", "!", "(", ")", "((", "))", " ", "×", "÷",
                "sin", "cos", "tan", "sqrt", "ln", "log", "exp", "abs", "inv", "sqr",
                "asin", "acos", "pi", "e", "PI", "Sin", "x", "#", "999999999999999999999999",
                "0.0000000001", "28", "!!", "^^",
            };
            return from n in Gen.Choose(0, 30)
                   from parts in Gen.Elements(pieces).ArrayOf(n)
                   select string.Concat(parts);
        }
    }
}
