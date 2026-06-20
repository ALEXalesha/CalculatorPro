using CalcPro.Wpf.Models;

namespace CalcPro.Wpf.Services;

/// <summary>
/// Walks an AST producing a decimal result. Pure-decimal where possible;
/// trig/log/exp drop to double inside <see cref="EvalTranscendental"/> and
/// the conversion boundary is explicit.
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

    public decimal Eval(AstNode node) => node switch
    {
        NumberNode n => n.Value,
        ConstantNode c => EvalConstant(c.Name),
        UnaryOpNode u => EvalUnary(u),
        BinaryOpNode b => EvalBinary(b),
        FunctionCallNode f => EvalFunction(f),
        PercentNode p => Round(Eval(p.Operand) / 100m),
        FactorialNode f => Factorial(Eval(f.Operand)),
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
        var v = Eval(u.Operand);
        return u.Op switch
        {
            "-" => -v,
            "+" => v,
            _ => throw new CalcEvalException($"Unknown unary op '{u.Op}'")
        };
    }

    private decimal EvalBinary(BinaryOpNode b)
    {
        var l = Eval(b.Left);
        var r = Eval(b.Right);
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
        var arg = Eval(f.Argument);
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
            "tan" => EvalTranscendental(arg, Math.Tan, applyAngleMode: true),
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

    private decimal EvalInverseTrig(decimal arg, Func<double, double> fn)
    {
        var rad = fn((double)arg);
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
