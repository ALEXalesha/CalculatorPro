using System;
using System.Globalization;

namespace CalcPro.Calculator;

public readonly record struct Fraction(long N, long D);

public static class Format
{
    /// <summary>
    /// Mirrors JS formatNumber: scientific form when |n| ≥ 1e15 or 0 &lt; |n| &lt; 1e-10;
    /// otherwise round-trips through 12-digit precision so 0.1+0.2 shows as 0.3.
    /// </summary>
    public static string Number(double n)
    {
        if (double.IsInfinity(n) || double.IsNaN(n)) return "Overflow";
        var a = Math.Abs(n);
        if (a >= 1e15 || (a < 1e-10 && n != 0))
        {
            // toExponential(6) → 6 digits after decimal point, mantissa e±dd
            var s = n.ToString("0.######e+0", CultureInfo.InvariantCulture);
            return s.Replace("e+0", "e").Replace("e-0", "e-").Replace("e+", "e").TrimEnd('0').Replace(".e", "e");
        }
        // Round through 12 sig figs then back to double — kills FP noise.
        var r = double.Parse(n.ToString("G12", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        return r.ToString("R", CultureInfo.InvariantCulture);
    }

    public static long Gcd(long a, long b)
    {
        a = Math.Abs(a); b = Math.Abs(b);
        while (b != 0) { (a, b) = (b, a % b); }
        return a == 0 ? 1 : a;
    }

    /// <summary>
    /// Stern-Brocot / continued-fractions approximation. Returns null if x can't
    /// be approximated within eps using a denominator ≤ maxDenom.
    /// </summary>
    public static Fraction? DecimalToFraction(double x, double eps = 1e-9, long maxDenom = 10000)
    {
        if (double.IsInfinity(x) || double.IsNaN(x)) return null;
        if (Math.Abs(x - Math.Round(x)) < eps) return new Fraction((long)Math.Round(x), 1);

        int sign = x < 0 ? -1 : 1;
        double v = Math.Abs(x);
        long h1 = 1, h0 = 0, k1 = 0, k0 = 1;
        double b = v;
        long bestN = 0, bestD = 1;
        double bestErr = double.PositiveInfinity;

        for (int iter = 0; iter < 64; iter++)
        {
            long a = (long)Math.Floor(b);
            long h2 = a * h1 + h0;
            long k2 = a * k1 + k0;
            if (k2 > maxDenom) break;
            h0 = h1; h1 = h2;
            k0 = k1; k1 = k2;

            double approx = (double)h1 / k1;
            double err = Math.Abs(v - approx);
            if (err < bestErr) { bestErr = err; bestN = h1; bestD = k1; }
            if (err < eps) break;

            double frac = b - a;
            if (frac < eps) break;
            b = 1 / frac;
            if (double.IsInfinity(b) || double.IsNaN(b)) break;
        }

        if (bestErr > 1e-6) return null;
        var g = Gcd(bestN, bestD);
        return new Fraction(sign * bestN / g, bestD / g);
    }

    /// <summary>
    /// "= a b/d" mixed-number string, or null if the value is integer-equivalent
    /// or has no usable rational approximation.
    /// </summary>
    public static string? FormatFraction(double value)
    {
        var f = DecimalToFraction(value);
        if (f is null || f.Value.D == 1) return null;
        var sign = f.Value.N < 0 ? "-" : "";
        long an = Math.Abs(f.Value.N);
        long d = f.Value.D;
        if (an > d)
        {
            long whole = an / d;
            long rem = an - whole * d;
            if (rem == 0) return null;
            return $"{sign}{whole} {rem}/{d}";
        }
        return $"{sign}{an}/{d}";
    }

    /// <summary>
    /// Display-form for the expression string: internal *, / become ×, ÷
    /// so the user sees what they typed.
    /// </summary>
    public static string ExpressionToDisplay(string s)
    {
        return s.Replace("*", "×")
                .Replace("/", "÷")
                .Replace("neg(", "-(")
                .Replace("sqrt(", "√(")
                .Replace("cbrt(", "³√(");
    }
}
