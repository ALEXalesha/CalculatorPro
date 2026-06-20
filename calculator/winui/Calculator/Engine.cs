using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CalcPro.Calculator;

public enum AngleMode { Deg, Rad, Grad }

public class CalcException : Exception
{
    public string Code { get; }
    public CalcException(string code) : base(code) { Code = code; }
}

public enum TokenType { Num, Var, Op, Func, LParen, RParen }

public readonly struct Token
{
    public TokenType Type { get; init; }
    public double Number { get; init; }
    public string Text { get; init; }
    public static Token Num(double v)   => new() { Type = TokenType.Num,  Number = v, Text = "" };
    public static Token Var(string n)   => new() { Type = TokenType.Var,  Text = n };
    public static Token Op(string o)    => new() { Type = TokenType.Op,   Text = o };
    public static Token Func(string n)  => new() { Type = TokenType.Func, Text = n };
    public static Token LP()            => new() { Type = TokenType.LParen, Text = "(" };
    public static Token RP()            => new() { Type = TokenType.RParen, Text = ")" };
}

internal readonly struct OpInfo
{
    public int Prec { get; init; }
    public bool RightAssoc { get; init; }
    public Func<double, double, double> Fn { get; init; }
}

public class Engine
{
    public AngleMode Angle { get; set; } = AngleMode.Deg;

    private static readonly Dictionary<string, OpInfo> Operators = new()
    {
        ["+"] = new() { Prec = 1, Fn = (a, b) => a + b },
        ["-"] = new() { Prec = 1, Fn = (a, b) => a - b },
        ["*"] = new() { Prec = 2, Fn = (a, b) => a * b },
        ["/"] = new() { Prec = 2, Fn = (a, b) =>
            {
                if (b == 0) throw new CalcException("DIV_ZERO");
                return a / b;
            } },
        ["^"] = new() { Prec = 4, RightAssoc = true, Fn = Math.Pow },
        ["%"] = new() { Prec = 2, Fn = (a, b) => a % b },
    };

    // Order matters: longer names first so `asinh` wins over `asin`.
    private static readonly string[] FuncNames =
    {
        "asinh", "acosh", "atanh", "asin", "acos", "atan",
        "sinh", "cosh", "tanh", "sin", "cos", "tan", "cot",
        "ln", "log", "sqrt", "cbrt", "abs", "fact", "exp10", "exp", "inv", "neg",
    };

    private double ToRadFromMode(double v) => Angle switch
    {
        AngleMode.Deg  => v * Math.PI / 180.0,
        AngleMode.Grad => v * Math.PI / 200.0,
        _              => v,
    };

    private double FromRadToMode(double v) => Angle switch
    {
        AngleMode.Deg  => v * 180.0 / Math.PI,
        AngleMode.Grad => v * 200.0 / Math.PI,
        _              => v,
    };

    private double ApplyFunc(string name, double v)
    {
        switch (name)
        {
            case "sin":   return Math.Sin(ToRadFromMode(v));
            case "cos":   return Math.Cos(ToRadFromMode(v));
            case "tan":
                {
                    var r = Math.Tan(ToRadFromMode(v));
                    if (double.IsInfinity(r) || double.IsNaN(r) || Math.Abs(r) > 1e15)
                        throw new CalcException("DOMAIN");
                    return r;
                }
            case "cot":
                {
                    var t = Math.Tan(ToRadFromMode(v));
                    if (double.IsInfinity(t) || double.IsNaN(t) || Math.Abs(t) < 1e-15)
                        throw new CalcException("DOMAIN");
                    return 1 / t;
                }
            case "asin":
                if (v < -1 || v > 1) throw new CalcException("DOMAIN");
                return FromRadToMode(Math.Asin(v));
            case "acos":
                if (v < -1 || v > 1) throw new CalcException("DOMAIN");
                return FromRadToMode(Math.Acos(v));
            case "atan":  return FromRadToMode(Math.Atan(v));
            case "sinh":  return Math.Sinh(v);
            case "cosh":  return Math.Cosh(v);
            case "tanh":  return Math.Tanh(v);
            case "asinh": return Math.Asinh(v);
            case "acosh":
                if (v < 1) throw new CalcException("DOMAIN");
                return Math.Acosh(v);
            case "atanh":
                if (v <= -1 || v >= 1) throw new CalcException("DOMAIN");
                return Math.Atanh(v);
            case "ln":
                if (v <= 0) throw new CalcException("DOMAIN");
                return Math.Log(v);
            case "log":
                if (v <= 0) throw new CalcException("DOMAIN");
                return Math.Log10(v);
            case "sqrt":
                if (v < 0) throw new CalcException("DOMAIN");
                return Math.Sqrt(v);
            case "cbrt":  return Math.Cbrt(v);
            case "abs":   return Math.Abs(v);
            case "neg":   return -v;
            case "fact":
                {
                    if (v < 0 || v != Math.Floor(v)) throw new CalcException("DOMAIN");
                    if (v > 170) throw new CalcException("OVERFLOW");
                    double r = 1;
                    for (int i = 2; i <= (int)v; i++) r *= i;
                    return r;
                }
            case "exp":   return Math.Exp(v);
            case "exp10": return Math.Pow(10, v);
            case "inv":
                if (v == 0) throw new CalcException("DIV_ZERO");
                return 1 / v;
            default: throw new CalcException("UNKNOWN_FN");
        }
    }

    public List<Token> Tokenise(string expr)
    {
        var tokens = new List<Token>();
        var e = expr.Trim();
        int i = 0;
        while (i < e.Length)
        {
            char ch = e[i];
            if (char.IsWhiteSpace(ch)) { i++; continue; }

            // Number with optional E exponent
            if (char.IsDigit(ch) || (ch == '.' && i + 1 < e.Length && char.IsDigit(e[i + 1])))
            {
                var num = "";
                int dots = 0;
                while (i < e.Length && (char.IsDigit(e[i]) || e[i] == '.'))
                {
                    if (e[i] == '.' && ++dots > 1) throw new CalcException("SYNTAX");
                    num += e[i++];
                }
                if (i < e.Length && (e[i] == 'E' || e[i] == 'e'))
                {
                    // Only treat as exponent if followed by digit/sign
                    if (i + 1 < e.Length && (char.IsDigit(e[i + 1]) || e[i + 1] == '+' || e[i + 1] == '-'))
                    {
                        num += e[i++]; // E
                        if (i < e.Length && (e[i] == '+' || e[i] == '-')) num += e[i++];
                        while (i < e.Length && char.IsDigit(e[i])) num += e[i++];
                    }
                }
                tokens.Add(Token.Num(double.Parse(num, CultureInfo.InvariantCulture)));
                continue;
            }

            // Function name (longest first via FuncNames order)
            var rest = e[i..];
            bool matched = false;
            foreach (var name in FuncNames)
            {
                if (rest.StartsWith(name, StringComparison.Ordinal))
                {
                    tokens.Add(Token.Func(name));
                    i += name.Length;
                    matched = true;
                    break;
                }
            }
            if (matched) continue;

            if (ch == 'π') { tokens.Add(Token.Num(Math.PI)); i++; continue; }
            if (ch == 'e') { tokens.Add(Token.Num(Math.E));  i++; continue; }
            if (ch == 'x') { tokens.Add(Token.Var("x"));     i++; continue; }

            if ("+-*/^%".IndexOf(ch) >= 0) { tokens.Add(Token.Op(ch.ToString())); i++; continue; }
            if (ch == '(') { tokens.Add(Token.LP()); i++; continue; }
            if (ch == ')') { tokens.Add(Token.RP()); i++; continue; }

            throw new CalcException("SYNTAX");
        }
        return tokens;
    }

    public List<Token> ToRpn(List<Token> tokens)
    {
        var output = new List<Token>();
        var ops = new Stack<Token>();
        for (int i = 0; i < tokens.Count; i++)
        {
            var tok = tokens[i];
            Token? prev = i > 0 ? tokens[i - 1] : null;

            if (tok.Type == TokenType.Num || tok.Type == TokenType.Var)
            {
                output.Add(tok);
                continue;
            }
            if (tok.Type == TokenType.Func)
            {
                ops.Push(tok);
                continue;
            }
            if (tok.Type == TokenType.Op)
            {
                // Unary -
                if (tok.Text == "-" && (prev is null || prev.Value.Type == TokenType.LParen || prev.Value.Type == TokenType.Op))
                {
                    ops.Push(Token.Func("neg"));
                    continue;
                }
                // Unary + ignored
                if (tok.Text == "+" && (prev is null || prev.Value.Type == TokenType.LParen || prev.Value.Type == TokenType.Op))
                {
                    continue;
                }
                var cur = Operators[tok.Text];
                while (ops.Count > 0)
                {
                    var top = ops.Peek();
                    if (top.Type == TokenType.LParen) break;
                    bool pop = top.Type == TokenType.Func
                        || (top.Type == TokenType.Op &&
                            (Operators[top.Text].Prec > cur.Prec ||
                             (Operators[top.Text].Prec == cur.Prec && !cur.RightAssoc)));
                    if (!pop) break;
                    output.Add(ops.Pop());
                }
                ops.Push(tok);
                continue;
            }
            if (tok.Type == TokenType.LParen) { ops.Push(tok); continue; }
            if (tok.Type == TokenType.RParen)
            {
                while (ops.Count > 0 && ops.Peek().Type != TokenType.LParen)
                    output.Add(ops.Pop());
                if (ops.Count == 0) throw new CalcException("MISMATCH");
                ops.Pop(); // pop LParen
                if (ops.Count > 0 && ops.Peek().Type == TokenType.Func)
                    output.Add(ops.Pop());
                continue;
            }
        }
        while (ops.Count > 0)
        {
            var t = ops.Pop();
            if (t.Type == TokenType.LParen || t.Type == TokenType.RParen)
                throw new CalcException("MISMATCH");
            output.Add(t);
        }
        return output;
    }

    public double EvalRpn(List<Token> rpn, Dictionary<string, double>? vars = null)
    {
        var stack = new Stack<double>();
        foreach (var tok in rpn)
        {
            switch (tok.Type)
            {
                case TokenType.Num:
                    stack.Push(tok.Number);
                    break;
                case TokenType.Var:
                    if (vars is null || !vars.TryGetValue(tok.Text, out var v))
                        throw new CalcException("UNDEFINED_VAR");
                    stack.Push(v);
                    break;
                case TokenType.Func:
                    if (stack.Count == 0) throw new CalcException("SYNTAX");
                    stack.Push(ApplyFunc(tok.Text, stack.Pop()));
                    break;
                case TokenType.Op:
                    if (stack.Count < 2) throw new CalcException("SYNTAX");
                    var b = stack.Pop();
                    var a = stack.Pop();
                    stack.Push(Operators[tok.Text].Fn(a, b));
                    break;
            }
        }
        if (stack.Count != 1) throw new CalcException("SYNTAX");
        return stack.Pop();
    }

    public double Evaluate(string s, Dictionary<string, double>? vars = null)
    {
        if (string.IsNullOrWhiteSpace(s)) throw new CalcException("EMPTY");
        var tokens = Tokenise(s);
        if (tokens.Count == 0) throw new CalcException("EMPTY");

        // Implicit multiplication
        var expanded = new List<Token>();
        for (int i = 0; i < tokens.Count; i++)
        {
            var cur = tokens[i];
            Token? prev = i > 0 ? tokens[i - 1] : null;
            if (prev is not null)
            {
                var p = prev.Value;
                bool prevEndsValue = p.Type == TokenType.Num || p.Type == TokenType.Var || p.Type == TokenType.RParen;
                bool curStartsValue = cur.Type == TokenType.Num || cur.Type == TokenType.Var
                                   || cur.Type == TokenType.LParen || cur.Type == TokenType.Func;
                if (prevEndsValue && curStartsValue) expanded.Add(Token.Op("*"));
            }
            expanded.Add(cur);
        }

        var rpn = ToRpn(expanded);
        var r = EvalRpn(rpn, vars);
        if (double.IsInfinity(r) || double.IsNaN(r)) throw new CalcException("OVERFLOW");
        return r;
    }

    public static string DisplayToInternal(string s)
        => s.Replace('×', '*').Replace('÷', '/').Replace('−', '-');
}
