using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CalcWinUI;

/// <summary>
/// Чистая логика калькулятора, портированная 1:1 из calculator.html.
/// Без UI-зависимостей — можно тестировать.
///
/// Принцип "два поля":
///   currentExpr     — белое поле снизу (текущее выражение)
///   lastExpression  — серое поле сверху (заполняется при = или цепочке операций)
///
/// Числа хранятся уже отформатированными: "1 234,56" (пробел-разделитель тысяч, запятая).
/// </summary>
public sealed class CalculatorEngine
{
    // Состояние
    public string CurrentExpr { get; private set; } = "0";
    public string LastExpression { get; private set; } = "";
    public string? PendingOperator { get; private set; }  // "+", "-", "*", "/"
    private double? _firstOperand;
    private bool _isInputtingNew = true;
    private bool _isResultShown;

    // Кнопка C превращается в AC, когда уже всё чисто
    public string ClearLabel =>
        (CurrentExpr != "0" || LastExpression.Length > 0) ? "C" : "AC";

    // Подсветка активного оператора (когда currentExpr заканчивается на " op ")
    public string? HighlightedOperator =>
        (PendingOperator != null && EndsWithOperator()) ? PendingOperator : null;

    // ===== вспомогательные =====

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    // Аналог JS /(-?[\d,]+)$/ — последний кусок из цифр и запятой.
    // Пробелы-разделители тысяч появляются только внутри уже отформатированных
    // результатов; во время ввода их нет, и регэкс остановится на пробеле.
    private static readonly Regex LastNumberRe = new(@"(-?[\d,]+)$", RegexOptions.Compiled);
    private static readonly Regex EndsWithOpRe = new(@" [+−×÷] $", RegexOptions.Compiled);

    private static string OpSymbol(string op) => op switch
    {
        "+" => "+",
        "-" => "−",   // минус U+2212
        "*" => "×",   // умножить ×
        "/" => "÷",   // делить ÷
        _ => op
    };

    /// <summary>
    /// "1 234,56" -> 1234.56. Возвращает double.
    /// </summary>
    public static double ParseDisplay(string s)
    {
        var clean = s.Replace(" ", "").Replace(",", ".");
        return double.Parse(clean, NumberStyles.Float, Inv);
    }

    /// <summary>
    /// 1234.56 -> "1 234,56". Аналог JS toLocaleString('ru-RU').
    /// </summary>
    public static string FormatNumber(double n)
    {
        if (double.IsNaN(n) || double.IsInfinity(n)) return "Ошибка";

        var abs = Math.Abs(n);

        // Целое в пределах 1e16 — без дробной части
        if (n == Math.Truncate(n) && abs < 1e16)
        {
            return ((long)n).ToString("N0", Inv).Replace(",", " ");
        }

        // Очень маленькое или очень большое — экспоненциальная
        if (abs != 0 && (abs < 1e-6 || abs >= 1e16))
        {
            return n.ToString("E6", Inv);
        }

        // Обычная дробь — обрезаем до 12 значащих
        var rounded = double.Parse(n.ToString("G12", Inv), Inv);
        var s = rounded.ToString("R", Inv);
        if (s.Contains('.'))
        {
            var parts = s.Split('.');
            var intFormatted = long.Parse(parts[0], Inv).ToString("N0", Inv).Replace(",", " ");
            return intFormatted + "," + parts[1];
        }
        return long.Parse(s, Inv).ToString("N0", Inv).Replace(",", " ");
    }

    private string GetLastNumber()
    {
        var m = LastNumberRe.Match(CurrentExpr);
        return m.Success ? m.Groups[1].Value : "";
    }

    private void ReplaceLastNumber(string newNum)
    {
        var last = GetLastNumber();
        if (last.Length > 0)
            CurrentExpr = CurrentExpr[..^last.Length] + newNum;
        else
            CurrentExpr += newNum;
    }

    private bool EndsWithOperator() => EndsWithOpRe.IsMatch(CurrentExpr);

    private static double Compute(double a, double b, string op) => op switch
    {
        "+" => a + b,
        "-" => a - b,
        "*" => a * b,
        "/" => b == 0 ? double.NaN : a / b,
        _ => b
    };

    // ===== публичный API =====

    public void InputDigit(string n)
    {
        // n: "0".."9" или "," для десятичной точки
        if (_isResultShown)
        {
            CurrentExpr = (n == ",") ? "0," : n;
            LastExpression = "";
            _firstOperand = null;
            PendingOperator = null;
            _isResultShown = false;
            _isInputtingNew = false;
            return;
        }

        if (_isInputtingNew)
        {
            if (CurrentExpr == "0")
                CurrentExpr = (n == ",") ? "0," : n;
            else if (CurrentExpr == "-0")
                CurrentExpr = (n == ",") ? "-0," : "-" + n;
            else
                CurrentExpr += (n == ",") ? "0," : n;  // после оператора
            _isInputtingNew = false;
        }
        else
        {
            var last = GetLastNumber();
            if (n == ",")
            {
                if (last.Contains(',')) return;
                CurrentExpr += ",";
            }
            else
            {
                if (last == "0") ReplaceLastNumber(n);
                else if (last == "-0") ReplaceLastNumber("-" + n);
                else CurrentExpr += n;
            }
        }
    }

    public void InputOperator(string op)
    {
        if (op == "=")
        {
            if (PendingOperator != null && _firstOperand.HasValue && !_isInputtingNew)
            {
                var b = ParseDisplay(GetLastNumber());
                var result = Compute(_firstOperand.Value, b, PendingOperator);
                LastExpression = CurrentExpr.Trim() + " =";
                CurrentExpr = FormatNumber(result);
                _firstOperand = null;
                PendingOperator = null;
                _isResultShown = true;
                _isInputtingNew = true;
            }
            return;
        }

        // продолжаем с предыдущего результата
        if (_isResultShown)
        {
            CurrentExpr = CurrentExpr + " " + OpSymbol(op) + " ";
            _firstOperand = ParseDisplay(GetLastNumber());
            PendingOperator = op;
            LastExpression = "";
            _isResultShown = false;
            _isInputtingNew = true;
            return;
        }

        // оператор уже стоит — заменяем
        if (EndsWithOperator())
        {
            CurrentExpr = CurrentExpr[..^3] + " " + OpSymbol(op) + " ";
            PendingOperator = op;
            return;
        }

        // первое число + оператор
        if (!_firstOperand.HasValue)
        {
            _firstOperand = ParseDisplay(GetLastNumber());
            CurrentExpr += " " + OpSymbol(op) + " ";
            PendingOperator = op;
            _isInputtingNew = true;
            return;
        }

        // a OP b OP → промежуточный результат
        var bv = ParseDisplay(GetLastNumber());
        var r = Compute(_firstOperand.Value, bv, PendingOperator!);
        LastExpression = CurrentExpr.Trim();
        CurrentExpr = FormatNumber(r) + " " + OpSymbol(op) + " ";
        _firstOperand = r;
        PendingOperator = op;
        _isInputtingNew = true;
    }

    public void ClearAll()
    {
        CurrentExpr = "0";
        LastExpression = "";
        _firstOperand = null;
        PendingOperator = null;
        _isResultShown = false;
        _isInputtingNew = true;
    }

    public void ClearEntry()
    {
        if (_isResultShown) { ClearAll(); return; }
        var last = GetLastNumber();
        // если уже 0 — повторное нажатие = полный сброс (AC)
        if (last == "0" || last == "-0" || last.Length == 0) { ClearAll(); return; }
        ReplaceLastNumber("0");
        _isInputtingNew = true;
    }

    public void Backspace()
    {
        if (_isResultShown) { ClearAll(); return; }
        if (EndsWithOperator()) return;
        var last = GetLastNumber();
        if (last.Length == 0) return;

        if (last.Length <= 1 || (last.Length == 2 && last.StartsWith("-")))
        {
            if (CurrentExpr.Length == last.Length)
            {
                CurrentExpr = "0";
                _isInputtingNew = true;
            }
            else
            {
                ReplaceLastNumber("0");
                _isInputtingNew = true;
            }
        }
        else
        {
            CurrentExpr = CurrentExpr[..^1];
        }
    }

    public void Negate()
    {
        if (_isResultShown)
        {
            var v = -ParseDisplay(CurrentExpr);
            CurrentExpr = FormatNumber(v);
            return;
        }
        // после оператора — готовим затравку -0
        if (_isInputtingNew && EndsWithOperator())
        {
            CurrentExpr += "-0";
            _isInputtingNew = false;
            return;
        }
        var last = GetLastNumber();
        if (last.Length == 0) return;
        var newLast = last.StartsWith("-") ? last[1..] : "-" + last;
        ReplaceLastNumber(newLast);
    }

    public void ApplyPercent()
    {
        if (_isInputtingNew || EndsWithOperator()) return;
        var last = GetLastNumber();
        var v = ParseDisplay(last);
        double result;
        if (_firstOperand.HasValue && (PendingOperator == "+" || PendingOperator == "-"))
            result = _firstOperand.Value * v / 100.0;
        else
            result = v / 100.0;
        ReplaceLastNumber(FormatNumber(result));
    }
}
