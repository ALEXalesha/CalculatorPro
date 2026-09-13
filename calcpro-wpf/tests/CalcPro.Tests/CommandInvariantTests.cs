using CalcPro.Core.Commands;
using CalcPro.Core.Models;
using CalcPro.Core.Services;
using CalcPro.Tests.Support;
using FsCheck;
using FsCheck.Xunit;

namespace CalcPro.Tests;

/// <summary>
/// The command layer is a state machine driven by arbitrary key presses. These
/// properties hammer it with random sequences and check the invariants after
/// every single key.
/// </summary>
public class CommandInvariantTests
{
    private static readonly Arbitrary<string[]> Sequences = Arb.From(Keys.Sequence());

    private static readonly Snapshot Initial = new Calculator().Snap();

    /// Throws with a readable message when an invariant is broken.
    private static void CheckInvariants(Calculator calc)
    {
        var s = calc.State;
        var expr = s.Expression;
        var trimmed = expr.TrimEnd();
        void Require(bool ok, string what)
        {
            if (!ok)
                throw new InvalidOperationException(
                    $"{what}\n  keys: {string.Join(" ", calc.Log)}\n  phase={s.Phase} expr='{expr}' display='{s.Display}'");
        }

        Require(s.Display == DisplayFormat.ErrorText || DisplayFormat.TryParse(s.Display, out _),
            "display must be a number or Error");
        Require(s.Display != DisplayFormat.ErrorText || s.Phase == CalcPhase.AfterEquals,
            "Error is only shown after '='");
        Require(s.Phase == CalcPhase.AfterEquals ? expr.EndsWith(" =") : !expr.Contains('='),
            "'=' appears exactly when phase is AfterEquals");
        Require(ExpressionText.OpenParens(expr) >= 0, "never more ')' than '('");
        Require(s.Phase != CalcPhase.AfterEquals || ExpressionText.OpenParens(expr) == 0,
            "finished expression is balanced");

        switch (s.Phase)
        {
            case CalcPhase.Idle:
                Require(expr.Length == 0, "Idle has an empty expression");
                break;
            case CalcPhase.AfterOperator:
                Require(trimmed.Length > 0 && "+-*/^(".Contains(trimmed[^1]),
                    "AfterOperator expression ends with an operator or '('");
                break;
            case CalcPhase.AfterValue:
                Require(trimmed.EndsWith(')') || trimmed.EndsWith('%'), "AfterValue ends with ')' or '%'");
                break;
            case CalcPhase.EnteringDigit:
                Require(trimmed.Length == 0 || "+-*/^(".Contains(trimmed[^1]),
                    "EnteringDigit expression is empty or ends with an operator or '('");
                break;
        }

        Require(calc.History.Entries.Count <= 50, "history is capped at 50");

        // The central guarantee: whatever was pressed, '=' would evaluate a
        // syntactically valid expression. Only math errors remain possible.
        if (s.Phase != CalcPhase.AfterEquals)
        {
            var pending = EqualsCommand.BuildExpression(s);
            try { Parser.Parse(pending); }
            catch (CalcParseException e)
            {
                Require(false, $"pending expression '{pending}' does not parse: {e.Message}");
            }
        }
    }

    [Property(MaxTest = 3000)]
    public Property RandomKeySequences_PreserveInvariants_AfterEveryKey() =>
        Prop.ForAll(Sequences, keys =>
        {
            var calc = new Calculator();
            foreach (var key in keys)
            {
                calc.Key(key);
                CheckInvariants(calc);
            }
            return true;
        });

    [Property(MaxTest = 2000)]
    public Property Equals_NeverReportsASyntaxError() =>
        Prop.ForAll(Sequences, keys =>
        {
            var calc = new Calculator();
            foreach (var key in keys) calc.Key(key);
            if (calc.State.Phase == CalcPhase.AfterEquals) return true;

            var pending = EqualsCommand.BuildExpression(calc.State);
            calc.Key("=");
            if (calc.State.Display != DisplayFormat.ErrorText)
                return calc.State.Display == DisplayFormat.Format(Evaluator.Evaluate(pending, calc.State.AngleMode));

            // Error is allowed only for genuine math errors.
            try { Evaluator.Evaluate(pending, calc.State.AngleMode); return false; }
            catch (CalcEvalException) { return true; }
        });

    [Property(MaxTest = 2000)]
    public Property UndoingEverything_RestoresTheInitialState() =>
        Prop.ForAll(Sequences, keys =>
        {
            var calc = new Calculator();
            foreach (var key in keys) calc.Key(key);
            while (calc.History.CanUndo) calc.History.Undo(calc.State);
            return calc.Snap() == Initial;
        });

    [Property(MaxTest = 2000)]
    public Property UndoThenRedo_IsIdentity() =>
        Prop.ForAll(Sequences, Arb.From(Gen.Choose(0, 40)), (keys, k) =>
        {
            var calc = new Calculator();
            foreach (var key in keys) calc.Key(key);
            var before = calc.Snap() with { HistoryCount = 0 };
            var steps = Math.Min(k, keys.Length);
            for (var i = 0; i < steps; i++) calc.History.Undo(calc.State);
            for (var i = 0; i < steps; i++) calc.History.Redo(calc.State);
            return (calc.Snap() with { HistoryCount = 0 }) == before;
        });

    [Property(MaxTest = 1000)]
    public Property EachUndo_RevertsExactlyOneKey() =>
        Prop.ForAll(Sequences, keys =>
        {
            var calc = new Calculator();
            var snapshots = new List<Snapshot> { calc.Snap() };
            foreach (var key in keys)
            {
                calc.Key(key);
                snapshots.Add(calc.Snap());
            }
            for (var i = snapshots.Count - 2; i >= 0; i--)
            {
                calc.History.Undo(calc.State);
                if (calc.Snap() != snapshots[i]) return false;
            }
            return true;
        });

    [Property(MaxTest = 1000)]
    public Property TypingAnInteger_ShowsItCanonically() =>
        Prop.ForAll(Arb.From(Gen.Choose(0, int.MaxValue)), Arb.From(Gen.Choose(0, 3)), (n, leadingZeros) =>
        {
            var calc = new Calculator();
            calc.Press(new string('0', leadingZeros) + n);
            return calc.State.Display == n.ToString();
        });

    [Property(MaxTest = 1000)]
    public Property TypedBinaryExpression_MatchesEvaluator() =>
        Prop.ForAll(Arb.From(Gens.SmallDecimal), Arb.From(Gen.Elements("+", "-", "*", "/", "^")),
            Arb.From(Gens.SmallDecimal), (a, op, b) =>
            {
                if (op == "^") { a = Math.Round(a % 10); b = Math.Round(b % 5); }
                var calc = new Calculator().Press($"{Keyed(a)} {op} {Keyed(b)} =");
                string expected;
                try { expected = DisplayFormat.Format(Evaluator.Evaluate($"{Keyed(a)}{op}{Keyed(b)}")); }
                catch (CalcEvalException) { expected = DisplayFormat.ErrorText; }
                return calc.State.Display == expected;
            });

    [Property(MaxTest = 1000)]
    public Property ClosedGroupFollowedByNumber_Multiplies() =>
        Prop.ForAll(Arb.From(Gen.Choose(0, 9999)), Arb.From(Gen.Choose(0, 9999)), (a, b) =>
            new Calculator().Press($"( {a} ) {b} =").State.Display == (a * (long)b).ToString());

    [Property(MaxTest = 1000)]
    public Property MissingCloseParens_AreAddedOnEquals() =>
        Prop.ForAll(Arb.From(Gen.Choose(0, 9999)), Arb.From(Gen.Choose(0, 9999)), Arb.From(Gen.Choose(1, 10)),
            (a, b, depth) =>
                new Calculator().Press(string.Concat(Enumerable.Repeat("( ", depth)) + $"{a} + {b} =")
                    .State.Display == (a + b).ToString());

    [Property(MaxTest = 1000)]
    public Property SignTwice_IsIdentity_WhileTyping() =>
        Prop.ForAll(Arb.From(Gens.SmallDecimal), a =>
        {
            var calc = new Calculator().Press(Keyed(a));
            var display = calc.State.Display;
            calc.Press("± ±");
            return calc.State.Display == display;
        });

    [Property(MaxTest = 1000)]
    public Property MemoryAddThenSubtract_LeavesMemoryUnchanged() =>
        Prop.ForAll(Arb.From(Gens.SmallDecimal), Arb.From(Gens.SmallDecimal), (m, x) =>
        {
            var calc = new Calculator().Press($"{Keyed(m)} MS AC {Keyed(x)} M+ M-");
            return calc.State.Memory == m;
        });

    [Property(MaxTest = 500)]
    public Property History_KeepsNewestFiftyResults() =>
        Prop.ForAll(Arb.From(Gen.Choose(0, 120)), n =>
        {
            var calc = new Calculator();
            for (var i = 1; i <= n; i++) calc.Press($"{i} + 0 =");
            var entries = calc.History.Entries;
            return entries.Count == Math.Min(n, 50) && (n == 0 || entries[0].Result == n.ToString());
        });

    [Property(MaxTest = 1000)]
    public Property ClearAll_AlwaysReturnsToIdle() =>
        Prop.ForAll(Sequences, keys =>
        {
            var calc = new Calculator();
            foreach (var key in keys) calc.Key(key);
            calc.Key("AC");
            var s = calc.State;
            return s.Expression == "" && s.Display == "0" && s.LastResult == null && s.Phase == CalcPhase.Idle;
        });

    /// Types a decimal the way a user would: "12.5", "0.25".
    private static string Keyed(decimal v) => DisplayFormat.Format(v);
}
