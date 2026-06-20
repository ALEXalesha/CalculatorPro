using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CalcPro.Calculator;

/// <summary>
/// pressKey() port. Mutates CalcState; the host UI calls UpdateDisplay() after.
/// Fraction-mode keys are forwarded to Fractions.PressKeyFrac.
/// </summary>
public class Input
{
    private readonly Engine _engine;
    private readonly CalcState _state;
    private readonly Action _onChanged;        // raised after every mutation
    private readonly Action _onEvaluated;      // raised after a successful "=" (for the bounce animation)

    public Input(Engine engine, CalcState state, Action onChanged, Action onEvaluated)
    {
        _engine = engine;
        _state = state;
        _onChanged = onChanged;
        _onEvaluated = onEvaluated;
    }

    public void PressKey(string k)
    {
        if (_state.Mode == CalcMode.Fractions)
        {
            Fractions.PressKeyFrac(_state, _engine, k, _onChanged);
            return;
        }

        // Error swallow / reset on next input
        if (_state.HasError)
        {
            if (k == "AC" || k == "⌫") { ClearAll(); return; }
            if (Regex.IsMatch(k, @"^[0-9.]$")) { ClearAll(); /* fall through */ }
            else return;
        }

        if (k == "AC") { ClearAll(); return; }
        if (k == "a⁄b") { PressKey("÷"); return; }

        if (k == "⌫")
        {
            if (_state.JustEvaluated) { ClearAll(); return; }
            if (_state.Expression.Length > 0)
            {
                var m = Regex.Match(_state.Expression,
                    @"(asin|acos|atan|sinh|cosh|tanh|sin|cos|tan|cot|ln|log|sqrt|cbrt|abs|fact|exp10|exp|neg)\($");
                if (m.Success)
                    _state.Expression = _state.Expression[..^m.Length];
                else
                    _state.Expression = _state.Expression[..^1];
                _state.Display = _state.Expression.Length > 0 ? _state.Expression : "0";
            }
            _state.HistoryLine = "";
            _onChanged();
            return;
        }

        // Digit
        if (k.Length == 1 && char.IsDigit(k[0]))
        {
            if (_state.JustEvaluated)
            {
                _state.Expression = "";
                _state.HistoryLine = "";
                _state.JustEvaluated = false;
                _state.Result = null;
            }
            if (_state.Expression == "0") _state.Expression = "";
            _state.Expression += k;
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }

        // Decimal
        if (k == ".")
        {
            if (_state.JustEvaluated)
            {
                _state.Expression = "0";
                _state.JustEvaluated = false;
                _state.Result = null;
                _state.HistoryLine = "";
            }
            var parts = Regex.Split(_state.Expression, @"[+\-*/^%(),]");
            var lastPart = parts.Length > 0 ? parts[^1] : "";
            if (lastPart.Contains('.')) return;
            if (_state.Expression.Length == 0 || Regex.IsMatch(_state.Expression, @"[+\-*/^(]$"))
                _state.Expression += "0";
            _state.Expression += ".";
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }

        if (k == "=") { DoEvaluate(); return; }

        if (k == "+/-") { ToggleSign(); return; }

        // Operators
        var opMap = new Dictionary<string, string>
        {
            ["+"] = "+", ["−"] = "-", ["-"] = "-",
            ["×"] = "*", ["*"] = "*",
            ["÷"] = "/", ["/"] = "/",
            ["^"] = "^",
        };
        if (opMap.TryGetValue(k, out var op))
        {
            if (_state.JustEvaluated && _state.Result is not null)
            {
                _state.Expression = Format.Number(_state.Result.Value) + op;
                _state.JustEvaluated = false;
                _state.HistoryLine = "";
            }
            else if (_state.Expression.Length == 0)
            {
                _state.Expression = op == "-" ? "-" : "0" + op;
            }
            else if (Regex.IsMatch(_state.Expression, @"[+\-*/^]$"))
            {
                _state.Expression = _state.Expression[..^1] + op;
            }
            else
            {
                _state.Expression += op;
            }
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }

        if (k == "%") { ApplyPercent(); return; }

        // Parens
        if (k == "(" || k == ")")
        {
            if (_state.JustEvaluated)
            {
                if (k == "(") { _state.Expression = ""; _state.JustEvaluated = false; _state.Result = null; _state.HistoryLine = ""; }
                else return;
            }
            _state.Expression += k;
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }

        // Functions that open paren
        var funcMap = new Dictionary<string, string>
        {
            ["sin"] = "sin(", ["cos"] = "cos(", ["tan"] = "tan(", ["cot"] = "cot(",
            ["asin"] = "asin(", ["acos"] = "acos(", ["atan"] = "atan(",
            ["sinh"] = "sinh(", ["cosh"] = "cosh(", ["tanh"] = "tanh(",
            ["asinh"] = "asinh(", ["acosh"] = "acosh(", ["atanh"] = "atanh(",
            ["ln"] = "ln(", ["log"] = "log(", ["abs"] = "abs(", ["|x|"] = "abs(",
            ["eˣ"] = "exp(", ["10ˣ"] = "exp10(",
            ["EE"] = "E",
        };
        if (funcMap.TryGetValue(k, out var fn))
        {
            if (k == "EE")
            {
                if (Regex.IsMatch(_state.Expression, @"\d$")) _state.Expression += "E";
                _state.Display = _state.Expression;
                _onChanged();
                return;
            }
            if (_state.JustEvaluated && _state.Result is not null)
            {
                _state.Expression = fn + Format.Number(_state.Result.Value) + ")";
                _state.JustEvaluated = false; _state.HistoryLine = ""; _state.Result = null;
                DoEvaluate(); return;
            }
            _state.Expression += fn;
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }

        if (k == "√")  { WrapOrOpen("sqrt("); return; }
        if (k == "³√") { WrapOrOpen("cbrt("); return; }

        if (k == "ʸ√x")
        {
            if (_state.Expression.Length == 0 && _state.Result is null) return;
            if (_state.JustEvaluated && _state.Result is not null)
            {
                _state.Expression = Format.Number(_state.Result.Value) + "^(1/";
                _state.JustEvaluated = false; _state.HistoryLine = ""; _state.Result = null;
            }
            else _state.Expression += "^(1/";
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }

        if (k == "n!" || k == "x!")
        {
            if (_state.JustEvaluated && _state.Result is not null)
            {
                _state.Expression = "fact(" + Format.Number(_state.Result.Value) + ")";
                _state.JustEvaluated = false; _state.HistoryLine = ""; _state.Result = null;
                DoEvaluate(); return;
            }
            if (_state.Expression.Length == 0) return;
            var m = Regex.Match(_state.Expression, @"^(.*?)(\d+\.?\d*)$");
            _state.Expression = m.Success
                ? m.Groups[1].Value + "fact(" + m.Groups[2].Value + ")"
                : "fact(" + _state.Expression + ")";
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }

        if (k == "2nd") { _state.AltSet = !_state.AltSet; _onChanged(); return; }
        if (k == "Rad")
        {
            _state.Angle = _state.Angle switch
            {
                AngleMode.Deg  => AngleMode.Rad,
                AngleMode.Rad  => AngleMode.Grad,
                _              => AngleMode.Deg,
            };
            _engine.Angle = _state.Angle;
            _onChanged();
            return;
        }

        if (k == "x²" || k == "x³")
        {
            var n = k == "x²" ? "2" : "3";
            if (_state.JustEvaluated && _state.Result is not null)
            {
                _state.Expression = Format.Number(_state.Result.Value) + "^" + n;
                _state.JustEvaluated = false; _state.HistoryLine = ""; _state.Result = null;
                DoEvaluate(); return;
            }
            if (_state.Expression.Length == 0) return;
            _state.Expression += "^" + n;
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }

        if (k == "xʸ")
        {
            if (_state.JustEvaluated && _state.Result is not null)
            {
                _state.Expression = Format.Number(_state.Result.Value) + "^";
                _state.JustEvaluated = false; _state.HistoryLine = ""; _state.Result = null;
            }
            else
            {
                if (_state.Expression.Length == 0) return;
                _state.Expression += "^";
            }
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }

        if (k == "1/x")
        {
            if (_state.JustEvaluated && _state.Result is not null)
            {
                _state.Expression = "1/(" + Format.Number(_state.Result.Value) + ")";
                _state.JustEvaluated = false; _state.HistoryLine = ""; _state.Result = null;
                DoEvaluate(); return;
            }
            if (_state.Expression.Length == 0) return;
            _state.Expression = "1/(" + _state.Expression + ")";
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }

        if (k == "π" || k == "e")
        {
            if (_state.JustEvaluated)
            {
                _state.Expression = ""; _state.JustEvaluated = false; _state.HistoryLine = ""; _state.Result = null;
            }
            if (Regex.IsMatch(_state.Expression, @"[\d)]$")) _state.Expression += "*";
            _state.Expression += k;
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }
    }

    private void WrapOrOpen(string opener)
    {
        if (_state.JustEvaluated && _state.Result is not null)
        {
            _state.Expression = opener + Format.Number(_state.Result.Value) + ")";
            _state.JustEvaluated = false; _state.HistoryLine = ""; _state.Result = null;
            DoEvaluate(); return;
        }
        _state.Expression += opener;
        _state.Display = _state.Expression;
        _onChanged();
    }

    private void ToggleSign()
    {
        if (_state.JustEvaluated && _state.Result is not null)
        {
            _state.Result = -_state.Result;
            _state.Expression = Format.Number(_state.Result.Value);
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }
        if (_state.Expression.Length == 0)
        {
            _state.Expression = "neg(";
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }
        var closed = Regex.Match(_state.Expression, @"^(.*)neg\(([^()]*)\)$");
        if (closed.Success)
        {
            _state.Expression = closed.Groups[1].Value + closed.Groups[2].Value;
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }
        var m = Regex.Match(_state.Expression, @"(.*?)(\d+\.?\d*)$");
        if (m.Success)
        {
            var before = m.Groups[1].Value;
            var num = m.Groups[2].Value;
            if (before.EndsWith("neg("))
                _state.Expression = before[..^4] + num;
            else
                _state.Expression = before + "neg(" + num + ")";
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }
        _state.Expression = "neg(" + _state.Expression + ")";
        _state.Display = _state.Expression;
        _onChanged();
    }

    /// <summary>
    /// Apple-style context-aware percent:
    ///   a + b%  → a + a*b/100      a - b%  → a - a*b/100
    ///   a * b%  → a * b/100        a / b%  → a / b/100
    ///   b%      → b/100
    /// </summary>
    private void ApplyPercent()
    {
        if (_state.Expression.Length == 0 && _state.Result is null) return;
        if (_state.JustEvaluated && _state.Result is not null)
        {
            _state.Result = _state.Result / 100;
            _state.Expression = Format.Number(_state.Result.Value);
            _state.Display = _state.Expression;
            _onChanged();
            return;
        }
        var expr = _state.Expression;
        int depth = 0, splitIdx = -1;
        char splitOp = ' ';
        for (int i = expr.Length - 1; i >= 0; i--)
        {
            var c = expr[i];
            if (c == ')') depth++;
            else if (c == '(') depth--;
            else if (depth == 0 && "+-*/".IndexOf(c) >= 0 && i > 0 && "+-*/^(".IndexOf(expr[i - 1]) < 0)
            {
                splitIdx = i; splitOp = c; break;
            }
        }
        try
        {
            if (splitIdx >= 0)
            {
                var left = expr[..splitIdx];
                var right = expr[(splitIdx + 1)..];
                if (right.Length == 0) return;
                var leftVal  = _engine.Evaluate(Engine.DisplayToInternal(left));
                var rightVal = _engine.Evaluate(Engine.DisplayToInternal(right));
                string replacement = (splitOp == '+' || splitOp == '-')
                    ? Format.Number(leftVal * rightVal / 100)
                    : Format.Number(rightVal / 100);
                _state.Expression = left + splitOp + replacement;
            }
            else
            {
                var v = _engine.Evaluate(Engine.DisplayToInternal(expr));
                _state.Expression = Format.Number(v / 100);
            }
            _state.Display = _state.Expression;
        }
        catch
        {
            _state.Expression = "(" + expr + ")/100";
            _state.Display = _state.Expression;
        }
        _onChanged();
    }

    public void DoEvaluate()
    {
        var raw = _state.Expression.Trim();
        if (raw.Length == 0) return;

        var expr = raw;
        int opens  = CountChar(expr, '(');
        int closes = CountChar(expr, ')');
        for (int i = 0; i < opens - closes; i++) expr += ")";

        try
        {
            var r = _engine.Evaluate(Engine.DisplayToInternal(expr));
            var txt = Format.Number(r);
            _state.CalcHistory.Insert(0, new HistoryEntry(Format.ExpressionToDisplay(_state.Expression), r, NowMs()));
            if (_state.CalcHistory.Count > 50)
                _state.CalcHistory.RemoveRange(50, _state.CalcHistory.Count - 50);
            HistoryStore.Save(_state.CalcHistory, _state.Memory);

            _state.HistoryLine = Format.ExpressionToDisplay(_state.Expression) + " =";
            _state.Display = txt;
            _state.Result = r;
            _state.Expression = txt;
            _state.JustEvaluated = true;
            _state.HasError = false;
            _onChanged();
            _onEvaluated();
        }
        catch (CalcException ex)
        {
            if (ex.Code == "EMPTY") return;
            string msg = ex.Code switch
            {
                "DIV_ZERO" => "Can't divide by 0",
                "OVERFLOW" => "Overflow",
                "DOMAIN"   => "Not a number",
                "MISMATCH" or "SYNTAX" => "Syntax error",
                _          => "Error",
            };
            _state.Display = msg;
            _state.HistoryLine = Format.ExpressionToDisplay(_state.Expression);
            _state.HasError = true;
            _state.JustEvaluated = false;
            _state.Result = null;
            _onChanged();
        }
    }

    public void ClearAll()
    {
        _state.Expression = "";
        _state.Display = "0";
        _state.HistoryLine = "";
        _state.Result = null;
        _state.JustEvaluated = false;
        _state.HasError = false;
        _onChanged();
    }

    // ----- Memory ops -----
    private double MemValue()
    {
        if (_state.Result is not null) return _state.Result.Value;
        if (double.TryParse(_state.Expression, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var v) && !double.IsNaN(v))
            return v;
        return 0;
    }

    public void MemAdd()    { _state.Memory += MemValue(); HistoryStore.Save(_state.CalcHistory, _state.Memory); }
    public void MemSub()    { _state.Memory -= MemValue(); HistoryStore.Save(_state.CalcHistory, _state.Memory); }
    public void MemStore()  { _state.Memory  = MemValue(); HistoryStore.Save(_state.CalcHistory, _state.Memory); }
    public void MemClear()  { _state.Memory  = 0;          HistoryStore.Save(_state.CalcHistory, _state.Memory); }
    public void MemRecall()
    {
        var v = Format.Number(_state.Memory);
        if (_state.JustEvaluated || _state.HasError)
        {
            _state.Expression = v;
            _state.JustEvaluated = false; _state.HasError = false; _state.HistoryLine = ""; _state.Result = null;
        }
        else if (_state.Expression.Length == 0 || Regex.IsMatch(_state.Expression, @"[+\-*/^(]$"))
        {
            _state.Expression += v;
        }
        else
        {
            _state.Expression = v;
        }
        _state.Display = _state.Expression;
        _onChanged();
    }

    private static int CountChar(string s, char c)
    {
        int n = 0;
        foreach (var ch in s) if (ch == c) n++;
        return n;
    }

    private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
