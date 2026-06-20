using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CalcPro.Calculator;

/// <summary>
/// Fraction-mode input handling + rendering helpers.
/// Mirrors pressKeyFrac / renderFractionExpr / evaluateFractions from calc-pro.html.
/// </summary>
public static class Fractions
{
    public static void PressKeyFrac(CalcState s, Engine engine, string k, Action onChanged)
    {
        // Clear error on next input
        if (s.HasError && k != "AC" && k != "⌫") s.HasError = false;
        // After a result, any digit/dot starts fresh
        if (s.FracResult is not null && k.Length == 1 && (char.IsDigit(k[0]) || k[0] == '.'))
            s.FracResetAll();

        FracTerm Cur() => s.FracTerms[s.FracTermIdx];
        void SetSlot(FracSlot slot) { s.FracSlot = slot; }

        // Digit
        if (k.Length == 1 && char.IsDigit(k[0]))
        {
            var t = Cur();
            switch (s.FracSlot)
            {
                case FracSlot.Whole: t.Whole += k; break;
                case FracSlot.Num:   t.Num   += k; break;
                case FracSlot.Den:   t.Den   += k; break;
            }
            s.FracResult = null;
            onChanged();
            return;
        }
        if (k == ".")
        {
            if (s.FracSlot == FracSlot.Whole && !Cur().Whole.Contains('.')) Cur().Whole += ".";
            onChanged();
            return;
        }

        if (k == "a/b" || k == "a⁄b")
        {
            SetSlot(s.FracSlot switch
            {
                FracSlot.Whole => FracSlot.Num,
                FracSlot.Num   => FracSlot.Den,
                _              => FracSlot.Num,
            });
            onChanged();
            return;
        }

        // Cursor navigation
        if (k == "↑") { SetSlot(FracSlot.Num); onChanged(); return; }
        if (k == "↓")
        {
            SetSlot(s.FracSlot == FracSlot.Whole ? FracSlot.Num : FracSlot.Den);
            onChanged();
            return;
        }
        if (k == "←")
        {
            if (s.FracSlot == FracSlot.Den) { SetSlot(FracSlot.Num); onChanged(); return; }
            if (s.FracSlot == FracSlot.Num) { SetSlot(FracSlot.Whole); onChanged(); return; }
            if (s.FracSlot == FracSlot.Whole && s.FracTermIdx > 0)
            {
                s.FracTermIdx--;
                var prev = Cur();
                SetSlot(prev.Den.Length > 0 ? FracSlot.Den : prev.Num.Length > 0 ? FracSlot.Num : FracSlot.Whole);
                onChanged();
                return;
            }
            return;
        }
        if (k == "→")
        {
            if (s.FracSlot == FracSlot.Whole)
            {
                var t = Cur();
                if (t.Num.Length > 0 || t.Den.Length > 0) SetSlot(FracSlot.Num);
                else if (s.FracTermIdx < s.FracTerms.Count - 1)
                {
                    s.FracTermIdx++; SetSlot(FracSlot.Whole);
                }
                onChanged();
                return;
            }
            if (s.FracSlot == FracSlot.Num) { SetSlot(FracSlot.Den); onChanged(); return; }
            if (s.FracSlot == FracSlot.Den && s.FracTermIdx < s.FracTerms.Count - 1)
            {
                s.FracTermIdx++; SetSlot(FracSlot.Whole); onChanged(); return;
            }
            return;
        }
        if (k == "Mix")
        {
            SetSlot(s.FracSlot == FracSlot.Whole ? FracSlot.Num : FracSlot.Whole);
            onChanged();
            return;
        }

        // Operators
        var opMap = new Dictionary<string, string>
        {
            ["+"] = "+", ["−"] = "-", ["-"] = "-",
            ["×"] = "*", ["*"] = "*",
            ["÷"] = "/", ["/"] = "/",
        };
        if (opMap.TryGetValue(k, out var op))
        {
            if (s.FracResult is not null)
            {
                var r = s.FracResult.Value;
                var rf = s.FracResultFrac;
                s.FracResetAll();
                if (rf is not null && rf.Value.D != 1)
                {
                    s.FracTerms[0] = new FracTerm
                    {
                        Whole = "",
                        Num = rf.Value.N.ToString(CultureInfo.InvariantCulture),
                        Den = rf.Value.D.ToString(CultureInfo.InvariantCulture),
                        Op = op,
                    };
                }
                else
                {
                    s.FracTerms[0] = new FracTerm { Whole = Format.Number(r), Op = op };
                }
                s.FracTerms.Add(new FracTerm());
                s.FracTermIdx = 1;
                s.FracSlot = FracSlot.Whole;
                onChanged();
                return;
            }
            var c = Cur();
            bool isCurEmpty = c.Whole.Length == 0 && c.Num.Length == 0 && c.Den.Length == 0;
            if (isCurEmpty && s.FracTermIdx > 0)
            {
                var prev = s.FracTerms[s.FracTermIdx - 1];
                if (prev.Op.Length > 0)
                {
                    prev.Op = op;
                    onChanged();
                    return;
                }
            }
            c.Op = op;
            s.FracTerms.Add(new FracTerm());
            s.FracTermIdx = s.FracTerms.Count - 1;
            s.FracSlot = FracSlot.Whole;
            s.FracResult = null;
            onChanged();
            return;
        }

        if (k == "+/-")
        {
            var t = Cur();
            string slotVal = s.FracSlot switch
            {
                FracSlot.Whole => t.Whole,
                FracSlot.Num   => t.Num,
                _              => t.Den,
            };
            string newVal;
            if (slotVal.StartsWith("-")) newVal = slotVal[1..];
            else if (slotVal.Length > 0) newVal = "-" + slotVal;
            else newVal = slotVal;

            switch (s.FracSlot)
            {
                case FracSlot.Whole: t.Whole = newVal; break;
                case FracSlot.Num:   t.Num   = newVal; break;
                case FracSlot.Den:   t.Den   = newVal; break;
            }
            onChanged();
            return;
        }

        if (k == "%")
        {
            Cur().Op = "/";
            s.FracTerms.Add(new FracTerm { Whole = "100" });
            EvaluateFractions(s, engine);
            onChanged();
            return;
        }
        if (k == "AC") { s.FracResetAll(); onChanged(); return; }
        if (k == "⌫")
        {
            var t = Cur();
            string slotVal = s.FracSlot switch
            {
                FracSlot.Whole => t.Whole,
                FracSlot.Num   => t.Num,
                _              => t.Den,
            };
            if (slotVal.Length > 0)
            {
                slotVal = slotVal[..^1];
                switch (s.FracSlot)
                {
                    case FracSlot.Whole: t.Whole = slotVal; break;
                    case FracSlot.Num:   t.Num   = slotVal; break;
                    case FracSlot.Den:   t.Den   = slotVal; break;
                }
            }
            else if (s.FracSlot == FracSlot.Den) SetSlot(FracSlot.Num);
            else if (s.FracSlot == FracSlot.Num) SetSlot(FracSlot.Whole);
            else if (s.FracTermIdx > 0)
            {
                s.FracTerms.RemoveAt(s.FracTerms.Count - 1);
                s.FracTermIdx--;
                Cur().Op = "";
                var p = Cur();
                SetSlot(p.Den.Length > 0 ? FracSlot.Den : p.Num.Length > 0 ? FracSlot.Num : FracSlot.Whole);
            }
            s.FracResult = null;
            onChanged();
            return;
        }
        if (k == "=")
        {
            if (s.FracResult is not null) return;
            EvaluateFractions(s, engine);
            onChanged();
            return;
        }
        if (k == "Simp") { SimplifyCurrent(s); onChanged(); return; }
        if (k == "D⇄F") { s.FractionMode = !s.FractionMode; onChanged(); return; }
    }

    public static void EvaluateFractions(CalcState s, Engine engine)
    {
        // Reject half-typed fractions
        foreach (var t in s.FracTerms)
        {
            bool hasNum = t.Num.Length > 0;
            bool hasDen = t.Den.Length > 0;
            if (hasNum != hasDen)
            {
                s.HasError = true;
                s.Display = "Incomplete fraction";
                return;
            }
        }
        var sb = new StringBuilder();
        foreach (var t in s.FracTerms)
        {
            sb.Append(TermToExpression(t));
            if (t.Op.Length > 0) sb.Append(t.Op);
        }
        var expr = sb.ToString();
        if (expr.Length == 0) return;
        try
        {
            var r = engine.Evaluate(Engine.DisplayToInternal(expr));
            s.FracResult = r;
            s.FracResultFrac = Format.DecimalToFraction(r);
            var histStr = PlainText(s);
            s.CalcHistory.Insert(0, new HistoryEntry(string.IsNullOrWhiteSpace(histStr) ? "frac" : histStr, r,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            if (s.CalcHistory.Count > 50) s.CalcHistory.RemoveRange(50, s.CalcHistory.Count - 50);
            HistoryStore.Save(s.CalcHistory, s.Memory);
        }
        catch (CalcException ex)
        {
            s.HasError = true;
            s.Display = ex.Code switch
            {
                "DIV_ZERO" => "Division by zero",
                "DOMAIN"   => "Math error",
                "OVERFLOW" => "Overflow",
                _          => "Error",
            };
        }
    }

    private static string TermToExpression(FracTerm term)
    {
        // -5 1/2 means -(5 + 1/2). Sign on whole or num applies to the whole term;
        // two minuses cancel.
        int sign = 1;
        string w = term.Whole, n = term.Num, d = term.Den;
        if (w.Length > 0 && w[0] == '-') { sign *= -1; w = w[1..]; }
        if (n.Length > 0 && n[0] == '-') { sign *= -1; n = n[1..]; }
        var prefix = sign < 0 ? "-" : "";
        if (n.Length > 0 && d.Length > 0)
        {
            if (w.Length > 0) return prefix + "(" + w + "+" + n + "/" + d + ")";
            return prefix + "(" + n + "/" + d + ")";
        }
        if (w.Length > 0) return prefix + w;
        if (n.Length > 0) return prefix + n;
        return "0";
    }

    private static void SimplifyCurrent(CalcState s)
    {
        var t = s.FracTerms[s.FracTermIdx];
        if (t.Num.Length == 0 || t.Den.Length == 0) return;
        if (!long.TryParse(t.Num, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n)) return;
        if (!long.TryParse(t.Den, NumberStyles.Integer, CultureInfo.InvariantCulture, out long d) || d == 0) return;
        long w = 0;
        if (t.Whole.Length > 0)
            long.TryParse(t.Whole, NumberStyles.Integer, CultureInfo.InvariantCulture, out w);

        int sign = 1;
        if (w < 0) { sign = -sign; w = -w; }
        if (n < 0) { sign = -sign; n = -n; }
        if (d < 0) d = -d;
        long g = Format.Gcd(n, d);
        n /= g; d /= g;
        if (n >= d)
        {
            w += n / d;
            n = n - (n / d) * d;
        }
        string Signed(long x, bool apply) => apply && sign < 0 ? "-" + x.ToString(CultureInfo.InvariantCulture)
                                                                 : x.ToString(CultureInfo.InvariantCulture);
        if (w != 0)
        {
            t.Whole = Signed(w, true);
            t.Num = n == 0 ? "" : n.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            t.Whole = "";
            t.Num = n == 0 ? "" : Signed(n, true);
        }
        t.Den = n == 0 ? "" : d.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Flatten fraction-mode terms into "5 1/2 + 3" style plain-text for history rows.
    /// </summary>
    public static string PlainText(CalcState s)
    {
        var sb = new StringBuilder();
        var opSym = new Dictionary<string, string> { ["+"] = "+", ["-"] = "−", ["*"] = "×", ["/"] = "÷" };
        for (int i = 0; i < s.FracTerms.Count; i++)
        {
            var t = s.FracTerms[i];
            bool any = false;
            if (t.Whole.Length > 0) { sb.Append(t.Whole); any = true; }
            if (t.Num.Length > 0 || t.Den.Length > 0)
            {
                if (any) sb.Append(' ');
                sb.Append(t.Num.Length > 0 ? t.Num : "·");
                sb.Append('/');
                sb.Append(t.Den.Length > 0 ? t.Den : "·");
            }
            if (t.Op.Length > 0 && opSym.TryGetValue(t.Op, out var sym)) sb.Append(' ').Append(sym).Append(' ');
        }
        return sb.ToString().Trim();
    }
}
