using CalcPro.Core.Models;

namespace CalcPro.Core.Services;

/// <summary>
/// Walks an AST producing a decimal result. Pure-decimal where possible;
/// trig/log/exp drop to double inside <see cref="EvalTranscendental"/> and
/// the conversion boundary is explicit.
///
/// Contract: the only exceptions that escape are <see cref="CalcEvalException"/>
/// (and <see cref="CalcParseException"/> from <see cref="Evaluate"/>). Decimal
/// overflow and double→decimal conversion of NaN/∞ are reported as
/// CalcEvalException("Overflow") instead of crashing the UI.
/// </summary>
public sealed class Evaluator
{
    private const int RoundingDigits = 12;
    public AngleMode AngleMode { get; init; } = AngleMode.Deg;

    public static decimal Evaluate(string expression, AngleMode angleMode = AngleMode.Deg)
    {
        var ast = Parser.Parse(expression);
        return new Evaluator { AngleMode = angleMode }.Eval(ast);
    }

    public decimal Eval(AstNode node)
    {
        try
        {
            // Final rounding covers bare literals and constants (π has 14 decimals).
            return Round(EvalNode(node));
        }
        catch (OverflowException)
        {
            throw new CalcEvalException("Overflow");
        }
    }

    private decimal EvalNode(AstNode node) => node switch
    {
        NumberNode n => n.Value,
        ConstantNode c => EvalConstant(c.Name),
        UnaryOpNode u => EvalUnary(u),
        BinaryOpNode b => EvalBinary(b),
        FunctionCallNode f => EvalFunction(f),
        PercentNode p => Round(EvalNode(p.Operand) / 100m),
        FactorialNode f => Factorial(EvalNode(f.Operand)),
        _ => throw new CalcEvalException($"Unknown AST node: {node.GetType().Name}")
    };

    private static decimal EvalConstant(string name) => name.ToLowerInvariant() switch
    {
        "pi" => (decimal)Math.PI,
        "e" => (decimal)Math.E,
        _ => throw new CalcEvalException($"Unknown constant '{name}'")
    };

    private decimal EvalUnary(UnaryOpNode u)
    {
        var v = EvalNode(u.Operand);
        return u.Op switch
        {
            "-" => -v,
            "+" => v,
            _ => throw new CalcEvalException($"Unknown unary op '{u.Op}'")
        };
    }

    private decimal EvalBinary(BinaryOpNode b)
    {
        var l = EvalNode(b.Left);
        var r = EvalNode(b.Right);
        return b.Op switch
        {
            "+" => Round(l + r),
            "-" => Round(l - r),
            "*" => Round(l * r),
            "/" => r == 0m
                ? throw new CalcEvalException("Division by zero")
                : Round(l / r),
            "^" => Round(Pow(l, r)),
            _ => throw new CalcEvalException($"Unknown binary op '{b.Op}'")
        };
    }

    private decimal EvalFunction(FunctionCallNode f)
    {
        var arg = EvalNode(f.Argument);
        return Round(f.Name switch
        {
            "sqrt" => arg < 0m
                ? throw new CalcEvalException("sqrt of negative")
                : (decimal)Math.Sqrt((double)arg),
            "abs" => Math.Abs(arg),
            "sqr" => arg * arg,
            "inv" => arg == 0m
                ? throw new CalcEvalException("1/0")
                : 1m / arg,
            "sin" => EvalTranscendental(arg, Math.Sin, applyAngleMode: true),
            "cos" => EvalTranscendental(arg, Math.Cos, applyAngleMode: true),
            "tan" => EvalTan(arg),
            "asin" => EvalInverseTrig(arg, Math.Asin),
            "acos" => EvalInverseTrig(arg, Math.Acos),
            "atan" => EvalInverseTrig(arg, Math.Atan),
            "ln" => arg <= 0m
                ? throw new CalcEvalException("ln of non-positive")
                : (decimal)Math.Log((double)arg),
            "log" => arg <= 0m
                ? throw new CalcEvalException("log of non-positive")
                : (decimal)Math.Log10((double)arg),
            "exp" => (decimal)Math.Exp((double)arg),
            _ => throw new CalcEvalException($"Unknown function '{f.Name}'")
        });
    }

    private decimal EvalTranscendental(decimal arg, Func<double, double> fn, bool applyAngleMode)
    {
        var x = (double)arg;
        if (applyAngleMode && AngleMode == AngleMode.Deg) x = x * Math.PI / 180.0;
        return (decimal)fn(x);
    }

    private decimal EvalTan(decimal arg)
    {
        // tan(90°) in double is ~1.6e16 rather than ∞ because π/2 is not exact.
        // Treat anything that large as the pole it really is.
        var v = EvalTranscendental(arg, Math.Tan, applyAngleMode: true);
        if (Math.Abs(v) > 1e15m) throw new CalcEvalException("tan undefined");
        return v;
    }

    private decimal EvalInverseTrig(decimal arg, Func<double, double> fn)
    {
        var rad = fn((double)arg);
        if (double.IsNaN(rad)) throw new CalcEvalException("Argument out of domain");
        if (AngleMode == AngleMode.Deg) rad = rad * 180.0 / Math.PI;
        return (decimal)rad;
    }

    private static decimal Pow(decimal baseVal, decimal exp)
    {
        if (exp == decimal.Truncate(exp) && exp >= -28 && exp <= 28)
        {
            var n = (int)exp;
            if (n == 0) return 1m;
            if (baseVal == 0m && n < 0) throw new CalcEvalException("0 to a negative power");
            decimal result = 1m;
            var absN = Math.Abs(n);
            for (var i = 0; i < absN; i++) result *= baseVal;
            return n < 0 ? 1m / result : result;
        }
        if (baseVal < 0m)
            throw new CalcEvalException("Negative base with non-integer exponent");
        if (baseVal == 0m)
            return exp > 0m ? 0m : throw new CalcEvalException("0 to a negative power");
        return (decimal)Math.Pow((double)baseVal, (double)exp);
    }

    private static decimal Factorial(decimal v)
    {
        if (v < 0m || v != decimal.Truncate(v))
            throw new CalcEvalException("Factorial requires non-negative integer");
        if (v > 27m) throw new CalcEvalException("Factorial overflow");
        decimal r = 1m;
        for (var i = 2; i <= (int)v; i++) r *= i;
        return r;
    }

    private static decimal Round(decimal v) =>
        Math.Round(v, RoundingDigits, MidpointRounding.ToEven);
}
