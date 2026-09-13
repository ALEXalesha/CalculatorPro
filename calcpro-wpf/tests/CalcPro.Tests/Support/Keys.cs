using System.Globalization;
using CalcPro.Core.Commands;
using CalcPro.Core.Models;
using CalcPro.Core.Services;
using FsCheck;

namespace CalcPro.Tests.Support;

/// <summary>
/// A tiny "keyboard" for driving the command layer from tests. A key is a short
/// code ("7", ".", "+", "(", "=", "AC", "sqrt", "M+"…); numbers written as "12.5"
/// in <see cref="Press(Calculator,string)"/> are typed digit by digit.
/// </summary>
public sealed class Calculator
{
    public CalcState State { get; } = new();
    public HistoryManager History { get; } = new();
    public List<string> Log { get; } = new();

    public void Key(string key)
    {
        var cmd = Keys.Create(key, History);
        cmd.Execute(State);
        History.Record(cmd);
        Log.Add(key);
    }

    /// Space-separated keys; multi-digit numbers are split into digit keys.
    public Calculator Press(string sequence)
    {
        foreach (var token in sequence.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (char.IsDigit(token[0]) || (token[0] == '.' && token.Length > 1))
                foreach (var ch in token) Key(ch.ToString());
            else
                Key(token);
        }
        return this;
    }

    public Snapshot Snap() => new(State.Expression, State.Display, State.LastResult, State.Phase,
        State.Memory, History.Entries.Count);
}

public sealed record Snapshot(string Expression, string Display, decimal? LastResult, CalcPhase Phase,
    decimal Memory, int HistoryCount);

public static class Keys
{
    public static readonly string[] Digits = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

    public static readonly string[] All =
    {
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", ".",
        "+", "-", "*", "/", "^", "%", "(", ")", "=",
        "AC", "CE", "BS", "±",
        "sqrt", "sin", "cos", "tan", "asin", "ln", "log", "exp", "abs", "sqr", "inv",
        "pi", "e", "M+", "M-", "MS", "MR", "MC", "v:-2.5", "v:1000000",
    };

    public static ICalcCommand Create(string key, HistoryManager history) => key switch
    {
        "." => new DigitCommand("."),
        _ when key.Length == 1 && char.IsDigit(key[0]) => new DigitCommand(key),
        "+" or "-" or "*" or "/" or "^" or "%" => new OperatorCommand(key),
        "(" or ")" => new ParenCommand(key),
        "=" => new EqualsCommand(history),
        "AC" => new ClearAllCommand(),
        "CE" => new ClearEntryCommand(),
        "BS" => new BackspaceCommand(),
        "±" => new SignCommand(),
        "pi" or "e" => new ConstantCommand(key),
        "M+" => new MemoryCommand(MemoryOp.Add),
        "M-" => new MemoryCommand(MemoryOp.Subtract),
        "MS" => new MemoryCommand(MemoryOp.Store),
        "MR" => new MemoryCommand(MemoryOp.Recall),
        "MC" => new MemoryCommand(MemoryOp.Clear),
        _ when key.StartsWith("v:") => new EnterValueCommand(decimal.Parse(key[2..], CultureInfo.InvariantCulture)),
        _ => new FunctionCommand(key)
    };

    /// Digits are over-represented so that sequences actually build numbers.
    public static Gen<string> Key =>
        Gen.OneOf(Gen.Elements(Digits), Gen.Elements(Digits), Gen.Elements(All));

    public static Gen<string[]> Sequence(int maxLength = 40) =>
        from n in Gen.Choose(0, maxLength)
        from keys in Key.ArrayOf(n)
        select keys;
}
