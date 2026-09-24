using System.Globalization;
using System.Numerics;
using System.Text;

namespace CalcPro.Core.Services;

/// <summary>Система счисления режима «Программист».</summary>
public enum NumberBase
{
    Bin = 2,
    Oct = 8,
    Dec = 10,
    Hex = 16,
}

/// <summary>
/// Режим «Программист» (1.6.0): целые 64 бита со знаком, как QWORD в калькуляторе Windows.
/// Число вводится в одной системе и сразу видно во всех четырёх; переполнение
/// заворачивается по модулю 2^64, отрицательные в HEX, OCT и BIN - дополнительный код.
///
/// Та же логика - в Calc Pro Glass (src/programmer-core.js). Обе версии проверяются одним
/// набором примеров (test-vectors/programmer.json), чтобы при одних нажатиях показывать одно.
///
/// Клавиши: цифры 0-9 и A-F, + − × ÷ Mod, AND OR XOR, &lt;&lt; &gt;&gt;, NOT, ±, ( ), =, AC, CE, ⌫
/// и HEX / DEC / OCT / BIN - смена системы. Приоритет как в C: × ÷ Mod, потом + −, сдвиги,
/// AND, XOR, OR.
/// </summary>
public sealed class ProgrammerCalculator
{
    public const string DivideByZero = "Cannot divide by zero";

    private static readonly Dictionary<string, int> Precedence = new()
    {
        ["×"] = 7, ["÷"] = 7, ["Mod"] = 7,
        ["+"] = 6, ["−"] = 6,
        ["<<"] = 5, [">>"] = 5,
        ["AND"] = 4,
        ["XOR"] = 3,
        ["OR"] = 2,
    };

    public static IReadOnlyCollection<string> Operators => Precedence.Keys;

    private abstract record Token;
    private sealed record Num(long Value) : Token;
    private sealed record Op(string Name) : Token;
    private sealed record Open : Token;
    private sealed record Close : Token;

    private readonly List<Token> _tokens = new();
    private string _entry = "";          // набираемое число в текущей системе, "" - не набирается
    private bool _justEvaluated;
    private string _lastExpression = "";

    public NumberBase Base { get; private set; } = NumberBase.Dec;

    /// <summary>Число на табло.</summary>
    public long Value { get; private set; }

    /// <summary>Сообщение об ошибке (деление на ноль) или null.</summary>
    public string? Error { get; private set; }

    /// <summary>Строка над табло: набранное выражение в текущей системе.</summary>
    public string Expression
    {
        get
        {
            if (_justEvaluated) return _lastExpression;
            var sb = new StringBuilder();
            foreach (var t in _tokens) Append(sb, t);
            return sb.ToString().Trim();
        }
    }

    /// <summary>Табло: набираемое число как набрано, иначе значение; с разбивкой на группы.</summary>
    public string Display => Error ?? Group(_entry.Length > 0 ? _entry : Format(Value, Base), Base);

    public void Press(string key)
    {
        if (key is "HEX" or "DEC" or "OCT" or "BIN")
        {
            SetBase(key switch { "HEX" => NumberBase.Hex, "OCT" => NumberBase.Oct, "BIN" => NumberBase.Bin, _ => NumberBase.Dec });
            return;
        }
        if (key == "AC") { Clear(); return; }
        if (Error is not null)
        {
            // После ошибки: сброс - и цифра начинает заново, остальное не делает ничего.
            if (key is "CE" or "⌫") { Clear(); return; }
            if (key.Length == 1 && DigitValue(key[0]) >= 0) Clear();
            else return;
        }

        if (key.Length == 1 && DigitValue(key[0]) >= 0) { Digit(key[0]); return; }
        switch (key)
        {
            case "⌫": Backspace(); return;
            case "CE": _entry = ""; Value = 0; return;
            case "±": Unary(v => unchecked(-v)); return;
            case "NOT": Unary(v => ~v); return;
            case "(": OpenParen(); return;
            case ")": CloseParen(); return;
            case "=": Equals(); return;
        }
        if (Precedence.ContainsKey(key)) Operator(key);
    }

    public void SetBase(NumberBase b)
    {
        Base = b;
        if (_entry.Length > 0) _entry = Format(Value, b);
        if (_justEvaluated) _lastExpression = "";
    }

    public void Clear()
    {
        _tokens.Clear();
        _entry = "";
        Value = 0;
        Error = null;
        _justEvaluated = false;
        _lastExpression = "";
    }

    // ───────── клавиши ─────────

    private void Digit(char c)
    {
        var d = DigitValue(c);
        if (d < 0 || d >= (int)Base) return; // цифры, которой в этой системе нет
        if (_justEvaluated) { _tokens.Clear(); _justEvaluated = false; _lastExpression = ""; }
        if (LastIs<Close>()) return; // после «)» сначала знак операции
        var candidate = (_entry is "0" or "-0" ? _entry[..^1] : _entry) + char.ToUpperInvariant(c);
        if (!TryParse(candidate, Base, out var v)) return; // не помещается в 64 бита
        _entry = candidate;
        Value = v;
    }

    private void Backspace()
    {
        if (_entry.Length == 0) return;
        _entry = _entry[..^1];
        if (_entry is "-") _entry = "";
        Value = _entry.Length == 0 ? 0 : (TryParse(_entry, Base, out var v) ? v : 0);
    }

    private void Unary(Func<long, long> f)
    {
        if (_justEvaluated) { _tokens.Clear(); _justEvaluated = false; _lastExpression = ""; }
        if (LastIs<Close>()) return;
        Value = f(Value);
        _entry = Format(Value, Base);
    }

    private void Operator(string op)
    {
        if (_justEvaluated) { _tokens.Clear(); _justEvaluated = false; _lastExpression = ""; }
        if (_entry.Length > 0) PushEntry();
        else if (_tokens.Count == 0) _tokens.Add(new Num(Value));
        else if (_tokens[^1] is Op) { _tokens[^1] = new Op(op); return; } // передумал - замена знака
        else if (_tokens[^1] is Open) return;
        _tokens.Add(new Op(op));
    }

    private void OpenParen()
    {
        if (_justEvaluated) { _tokens.Clear(); _justEvaluated = false; _lastExpression = ""; Value = 0; }
        if (_entry.Length > 0 || LastIs<Close>() || LastIs<Num>()) return; // скобка после числа - нет
        _tokens.Add(new Open());
    }

    private void CloseParen()
    {
        if (_justEvaluated) return;
        var open = _tokens.Count(t => t is Open) - _tokens.Count(t => t is Close);
        if (open <= 0) return;
        if (_entry.Length > 0) PushEntry();
        else if (_tokens[^1] is Op) _tokens.Add(new Num(Value));
        else if (_tokens[^1] is Open) return; // пустые скобки
        _tokens.Add(new Close());
        // На табло - значение того, что в скобках.
        var start = MatchingOpen(_tokens.Count - 1);
        if (TryEvaluate(_tokens.GetRange(start + 1, _tokens.Count - start - 2), out var v, out _)) Value = v;
    }

    private void Equals()
    {
        if (_justEvaluated) return;
        if (_entry.Length > 0) PushEntry();
        else if (_tokens.Count == 0 || _tokens[^1] is Op or Open) _tokens.Add(new Num(Value));
        if (_tokens[^1] is Open) _tokens.RemoveAt(_tokens.Count - 1);
        var open = _tokens.Count(t => t is Open) - _tokens.Count(t => t is Close);
        for (var i = 0; i < open; i++) _tokens.Add(new Close());

        var text = Expression + " =";
        if (!TryEvaluate(_tokens, out var result, out var error))
        {
            Error = error;
            _tokens.Clear();
            _entry = "";
            return;
        }
        Value = result;
        _lastExpression = text;
        _tokens.Clear();
        _justEvaluated = true;
    }

    private void PushEntry()
    {
        _tokens.Add(new Num(Value));
        _entry = "";
    }

    private bool LastIs<T>() where T : Token => _entry.Length == 0 && _tokens.Count > 0 && _tokens[^1] is T;

    private int MatchingOpen(int closeIndex)
    {
        var depth = 0;
        for (var i = closeIndex; i >= 0; i--)
        {
            if (_tokens[i] is Close) depth++;
            else if (_tokens[i] is Open && --depth == 0) return i;
        }
        return 0;
    }

    private void Append(StringBuilder sb, Token t)
    {
        switch (t)
        {
            case Num n:
                var s = Format(n.Value, Base);
                sb.Append(s.StartsWith('-') ? "(" + s + ")" : s).Append(' ');
                break;
            case Op o: sb.Append(o.Name).Append(' '); break;
            case Open: sb.Append('('); break;
            case Close:
                if (sb.Length > 0 && sb[^1] == ' ') sb.Length--;
                sb.Append(") ");
                break;
        }
    }

    // ───────── вычисление ─────────

    /// <summary>Вычислить скобочное выражение с приоритетами (сортировочная станция).</summary>
    private static bool TryEvaluate(IReadOnlyList<Token> tokens, out long result, out string? error)
    {
        var values = new Stack<long>();
        var ops = new Stack<Token>();
        error = null;
        result = 0;

        bool Reduce()
        {
            var op = ((Op)ops.Pop()).Name;
            if (values.Count < 2) return false;
            var b = values.Pop();
            var a = values.Pop();
            if (!TryApply(op, a, b, out var r)) return false;
            values.Push(r);
            return true;
        }

        foreach (var t in tokens)
        {
            switch (t)
            {
                case Num n: values.Push(n.Value); break;
                case Open: ops.Push(t); break;
                case Close:
                    while (ops.Count > 0 && ops.Peek() is Op)
                        if (!Reduce()) { error = DivideByZero; return false; }
                    if (ops.Count > 0) ops.Pop();
                    break;
                case Op o:
                    while (ops.Count > 0 && ops.Peek() is Op top && Precedence[top.Name] >= Precedence[o.Name])
                        if (!Reduce()) { error = DivideByZero; return false; }
                    ops.Push(o);
                    break;
            }
        }
        while (ops.Count > 0)
        {
            if (ops.Peek() is not Op) { ops.Pop(); continue; }
            if (!Reduce()) { error = DivideByZero; return false; }
        }
        result = values.Count > 0 ? values.Peek() : 0;
        return true;
    }

    /// <summary>Одна операция над 64-битными целыми; false - деление на ноль.</summary>
    public static bool TryApply(string op, long a, long b, out long r)
    {
        unchecked
        {
            switch (op)
            {
                case "+": r = a + b; return true;
                case "−": r = a - b; return true;
                case "×": r = a * b; return true;
                case "÷":
                    if (b == 0) { r = 0; return false; }
                    // long.MinValue / -1 в .NET бросает исключение даже без checked.
                    r = a == long.MinValue && b == -1 ? long.MinValue : a / b;
                    return true;
                case "Mod":
                    if (b == 0) { r = 0; return false; }
                    r = b == -1 ? 0 : a % b;
                    return true;
                case "AND": r = a & b; return true;
                case "OR": r = a | b; return true;
                case "XOR": r = a ^ b; return true;
                case "<<": r = a << (int)(b & 63); return true;
                case ">>": r = a >> (int)(b & 63); return true;
                default: throw new ArgumentException("Неизвестная операция " + op);
            }
        }
    }

    // ───────── перевод ─────────

    /// <summary>Значение цифры 0-9, A-F (любой регистр); -1 - не цифра.</summary>
    public static int DigitValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'F' => c - 'A' + 10,
        >= 'a' and <= 'f' => c - 'a' + 10,
        _ => -1,
    };

    /// <summary>
    /// Число в системе: DEC - со знаком, остальные - дополнительный код без ведущих нулей
    /// (-1 в HEX - шестнадцать F). Цифры заглавные.
    /// </summary>
    public static string Format(long v, NumberBase b)
    {
        if (b == NumberBase.Dec) return v.ToString(CultureInfo.InvariantCulture);
        var u = unchecked((ulong)v);
        if (u == 0) return "0";
        var radix = (ulong)b;
        var sb = new StringBuilder();
        while (u > 0)
        {
            sb.Insert(0, "0123456789ABCDEF"[(int)(u % radix)]);
            u /= radix;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Разбор записи в системе. DEC - со знаком, в пределах long; остальные - до 64 бит,
    /// старший бит - знак. Пробелы разбивки пропускаются. Не помещается или не цифра - false.
    /// </summary>
    public static bool TryParse(string? text, NumberBase b, out long v)
    {
        v = 0;
        var s = (text ?? "").Replace(" ", "");
        var negative = b == NumberBase.Dec && s.StartsWith('-');
        if (negative) s = s[1..];
        if (s.Length == 0) return false;
        BigInteger acc = 0;
        foreach (var c in s)
        {
            var d = DigitValue(c);
            if (d < 0 || d >= (int)b) return false;
            acc = acc * (int)b + d;
            if (acc > ulong.MaxValue) return false;
        }
        if (b == NumberBase.Dec)
        {
            if (negative ? acc > (BigInteger)long.MaxValue + 1 : acc > long.MaxValue) return false;
            v = (long)(negative ? -acc : acc);
            return true;
        }
        v = unchecked((long)(ulong)acc);
        return true;
    }

    /// <summary>Разбивка для глаз: BIN и HEX по 4 знака, OCT и DEC по 3, справа налево.</summary>
    public static string Group(string digits, NumberBase b)
    {
        var sign = digits.StartsWith('-') ? "-" : "";
        var s = sign.Length > 0 ? digits[1..] : digits;
        var size = b is NumberBase.Bin or NumberBase.Hex ? 4 : 3;
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            if (i > 0 && (s.Length - i) % size == 0) sb.Append(' ');
            sb.Append(s[i]);
        }
        return sign + sb;
    }
}
