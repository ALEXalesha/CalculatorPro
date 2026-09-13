using System.Globalization;
using CalcPro.Core.Models;
using CalcPro.Core.Services;
using CalcPro.Tests.Support;
using FsCheck;
using FsCheck.Xunit;

namespace CalcPro.Tests;

/// <summary>
/// Algebraic laws the decimal evaluator must obey. Inputs are chosen so the laws
/// are exact in decimal arithmetic (≤ 4 fractional digits, modest magnitudes).
/// </summary>
public class EvaluatorPropertyTests
{
    private static readonly Arbitrary<decimal> Dec = Arb.From(Gens.SignedDecimal);
    private static readonly Arbitrary<decimal> Int = Arb.From(Gens.SmallInt);
    private static readonly Arbitrary<decimal> NonZero = Arb.From(Gens.NonZeroInt);

    private static decimal Eval(string expr, AngleMode mode = AngleMode.Deg) => Evaluator.Evaluate(expr, mode);

    private static string Lit(decimal v) =>
        v < 0 ? "(" + v.ToString(CultureInfo.InvariantCulture) + ")" : v.ToString(CultureInfo.InvariantCulture);

    [Property(MaxTest = 1000)]
    public Property Addition_IsCommutative() =>
        Prop.ForAll(Dec, Dec, (a, b) => Eval($"{Lit(a)}+{Lit(b)}") == Eval($"{Lit(b)}+{Lit(a)}"));

    [Property(MaxTest = 1000)]
    public Property Multiplication_IsCommutative() =>
        Prop.ForAll(Dec, Dec, (a, b) => Eval($"{Lit(a)}*{Lit(b)}") == Eval($"{Lit(b)}*{Lit(a)}"));

    [Property(MaxTest = 1000)]
    public Property Addition_IsAssociative() =>
        Prop.ForAll(Dec, Dec, Dec, (a, b, c) =>
            Eval($"({Lit(a)}+{Lit(b)})+{Lit(c)}") == Eval($"{Lit(a)}+({Lit(b)}+{Lit(c)})"));

    [Property(MaxTest = 1000)]
    public Property Multiplication_IsAssociative_ForIntegers() =>
        Prop.ForAll(Int, Int, Int, (a, b, c) =>
            Eval($"({Lit(a)}*{Lit(b)})*{Lit(c)}") == Eval($"{Lit(a)}*({Lit(b)}*{Lit(c)})"));

    [Property(MaxTest = 1000)]
    public Property Multiplication_DistributesOverAddition_ForIntegers() =>
        Prop.ForAll(Int, Int, Int, (a, b, c) =>
            Eval($"{Lit(a)}*({Lit(b)}+{Lit(c)})") == Eval($"{Lit(a)}*{Lit(b)}+{Lit(a)}*{Lit(c)}"));

    [Property(MaxTest = 1000)]
    public Property Identities_Hold() =>
        Prop.ForAll(Dec, a =>
            Eval($"{Lit(a)}+0") == a &&
            Eval($"{Lit(a)}*1") == a &&
            Eval($"{Lit(a)}/1") == a &&
            Eval($"{Lit(a)}-{Lit(a)}") == 0m &&
            Eval($"{Lit(a)}*0") == 0m &&
            Eval($"{Lit(a)}^1") == a &&
            Eval($"{Lit(a)}^0") == 1m);

    [Property(MaxTest = 1000)]
    public Property Negation_IsAnInvolution() =>
        Prop.ForAll(Dec, a => Eval($"--{Lit(a)}") == a && Eval($"-{Lit(a)}") == -a);

    [Property(MaxTest = 1000)]
    public Property Subtraction_IsAdditionOfNegation() =>
        Prop.ForAll(Dec, Dec, (a, b) => Eval($"{Lit(a)}-{Lit(b)}") == Eval($"{Lit(a)}+-{Lit(b)}"));

    [Property(MaxTest = 1000)]
    public Property Division_UndoesMultiplication() =>
        Prop.ForAll(Int, NonZero, (a, b) => Eval($"({Lit(a)}*{Lit(b)})/{Lit(b)}") == a);

    [Property(MaxTest = 1000)]
    public Property SelfDivision_IsOne() =>
        Prop.ForAll(NonZero, a => Eval($"{Lit(a)}/{Lit(a)}") == 1m);

    [Property(MaxTest = 1000)]
    public Property Addition_IsMonotonic() =>
        Prop.ForAll(Dec, Dec, Dec, (a, b, c) =>
            a >= b || Eval($"{Lit(a)}+{Lit(c)}") < Eval($"{Lit(b)}+{Lit(c)}"));

    [Property(MaxTest = 1000)]
    public Property Squares_AgreeAcrossSpellings() =>
        Prop.ForAll(Dec, a =>
        {
            var viaMul = Eval($"{Lit(a)}*{Lit(a)}");
            return Eval($"{Lit(a)}^2") == viaMul && Eval($"sqr({Lit(a)})") == viaMul;
        });

    [Property(MaxTest = 500)]
    public Property IntegerPowers_AddExponents() =>
        Prop.ForAll(Arb.From(Gen.Choose(-9, 9)), Arb.From(Gen.Choose(0, 6)), Arb.From(Gen.Choose(0, 6)),
            (x, m, n) => Eval($"({x})^{m}*({x})^{n}") == Eval($"({x})^{m + n}"));

    [Property(MaxTest = 1000)]
    public Property Abs_IsNonNegativeAndEven() =>
        Prop.ForAll(Dec, a =>
            Eval($"abs({Lit(a)})") >= 0m &&
            Eval($"abs({Lit(a)})") == Eval($"abs(-{Lit(a)})") &&
            (Eval($"abs({Lit(a)})") == a || Eval($"abs({Lit(a)})") == -a));

    [Property(MaxTest = 1000)]
    public Property Sqrt_OfSquare_IsAbs() =>
        Prop.ForAll(Int, a => Eval($"sqrt({Lit(a)}^2)") == Math.Abs(a));

    [Property(MaxTest = 1000)]
    public Property Percent_IsHundredth() =>
        Prop.ForAll(Dec, a => Eval($"{Lit(a)}%") * 100m == a);

    [Property(MaxTest = 1000)]
    public Property Inv_IsReciprocal() =>
        Prop.ForAll(NonZero, a => Eval($"inv({Lit(a)})") == Eval($"1/{Lit(a)}"));

    [Property(MaxTest = 200)]
    public Property Factorial_Recurrence() =>
        Prop.ForAll(Arb.From(Gen.Choose(1, 27)), n => Eval($"{n}!") == n * Eval($"{n - 1}!"));

    [Property(MaxTest = 1000)]
    public Property PythagoreanIdentity_HoldsInBothAngleModes() =>
        Prop.ForAll(Dec, Arb.From(Gen.Elements(AngleMode.Deg, AngleMode.Rad)), (a, mode) =>
        {
            var s = Eval($"sin({Lit(a)})", mode);
            var c = Eval($"cos({Lit(a)})", mode);
            return Math.Abs(s * s + c * c - 1m) < 1e-10m;
        });

    [Property(MaxTest = 1000)]
    public Property ArcSine_InvertsSine_OnPrincipalBranch() =>
        Prop.ForAll(Arb.From(Gen.Choose(-900, 900)), tenths =>
        {
            var deg = tenths / 10m;
            return Math.Abs(Eval($"asin(sin({Lit(deg)}))") - deg) < 1e-7m;
        });

    [Property(MaxTest = 500)]
    public Property Ln_InvertsExp() =>
        // x ≥ 0 only: for negative x, exp(x) < 1 loses relative precision to the
        // 12-digit rounding (exp(-20) ≈ 0.000000002061), so ln cannot recover x.
        Prop.ForAll(Arb.From(Gen.Choose(0, 2000)), hundredths =>
        {
            var x = hundredths / 100m;
            return Math.Abs(Eval($"ln(exp({Lit(x)}))") - x) < 1e-9m;
        });

    [Property(MaxTest = 200)]
    public Property Log10_OfPowerOfTen_IsExponent() =>
        Prop.ForAll(Arb.From(Gen.Choose(0, 27)), n => Eval($"log(10^{n})") == n);

    [Property(MaxTest = 2000)]
    public Property Results_NeverHaveMoreThan12FractionalDigits() =>
        Prop.ForAll(Arb.From(Gens.Ast(3)), ast =>
        {
            try
            {
                var v = new Evaluator().Eval(ast);
                return v == Math.Round(v, 12);
            }
            catch (CalcEvalException) { return true; }
        });

    [Property(MaxTest = 3000)]
    public Property EvaluatingRandomTrees_OnlyEverThrowsEvalException() =>
        // Wide numbers + exp/^/! guarantee overflows; they must surface as
        // CalcEvalException, never as OverflowException (which crashed the app).
        Prop.ForAll(Arb.From(Gens.Ast(4, Gens.WideDecimal)), Arb.From(Gen.Elements(AngleMode.Deg, AngleMode.Rad)),
            (ast, mode) =>
            {
                try { new Evaluator { AngleMode = mode }.Eval(ast); return true; }
                catch (CalcEvalException) { return true; }
            });

    [Property(MaxTest = 3000)]
    public Property EvaluatingNoisyText_OnlyEverThrowsCalculatorExceptions() =>
        Prop.ForAll(Arb.From(Gens.NoisyExpression), text =>
        {
            try { Evaluator.Evaluate(text); return true; }
            catch (CalcParseException) { return true; }
            catch (CalcEvalException) { return true; }
        });

    [Property(MaxTest = 2000)]
    public Property TextAndTree_EvaluateIdentically() =>
        Prop.ForAll(Arb.From(Gens.Ast(3)), ast =>
        {
            var text = ExpressionPrinter.Minimal(ast);
            string Outcome(Func<decimal> f)
            {
                try { return f().ToString(CultureInfo.InvariantCulture); }
                catch (CalcEvalException) { return "error"; }
            }
            return Outcome(() => new Evaluator().Eval(ast)) == Outcome(() => Evaluator.Evaluate(text));
        });

    [Property(MaxTest = 1000)]
    public Property Evaluation_IsDeterministic() =>
        Prop.ForAll(Arb.From(Gens.Ast(3)), ast =>
        {
            var text = ExpressionPrinter.Minimal(ast);
            try { return Evaluator.Evaluate(text) == Evaluator.Evaluate(text); }
            catch (CalcEvalException) { return true; }
        });
}
