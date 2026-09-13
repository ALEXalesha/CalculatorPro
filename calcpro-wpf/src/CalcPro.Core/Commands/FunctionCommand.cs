using CalcPro.Core.Models;
using CalcPro.Core.Services;

namespace CalcPro.Core.Commands;

/// <summary>
/// Applies a unary function (sin, cos, sqrt, etc.).
///   • While typing / after an operator: replaces the display with f(display).
///   • After ')' or '%': wraps that trailing group, "(2 + 3)" → "sqrt(2 + 3)".
///   • After '=': starts a new line "sqrt(16) =" with the result.
/// </summary>
public sealed class FunctionCommand : BaseCalcCommand
{
    private readonly string _name;

    public FunctionCommand(string name) => _name = name;

    public override string Description => $"f({_name})";

    protected override void Apply(CalcState state)
    {
        switch (state.Phase)
        {
            case CalcPhase.AfterValue:
                WrapTrailingGroup(state);
                return;

            case CalcPhase.AfterEquals:
                if (!DisplayFormat.TryParse(state.Display, out var previous)) return;
                var call = $"{_name}({DisplayFormat.Format(previous)})";
                if (TryEvaluate(call, state, out var result))
                {
                    state.Display = DisplayFormat.Format(result);
                    state.LastResult = result;
                }
                else
                {
                    state.Display = DisplayFormat.ErrorText;
                }
                state.Expression = call + " =";
                return;

            default:
                var argCall = $"{_name}({DisplayFormat.Format(ParseOrZero(state.Display))})";
                if (TryEvaluate(argCall, state, out var value))
                {
                    state.Display = DisplayFormat.Format(value);
                    state.Phase = CalcPhase.EnteringDigit;
                }
                else
                {
                    state.Display = DisplayFormat.ErrorText;
                    state.Expression = ExpressionText.CloseParens(state.Expression + argCall) + " =";
                    state.Phase = CalcPhase.AfterEquals;
                }
                return;
        }
    }

    private void WrapTrailingGroup(CalcState state)
    {
        var start = ExpressionText.TrailingValueStart(state.Expression);
        if (start < 0) return;
        var tail = state.Expression[start..].TrimEnd();
        var wrapped = tail.StartsWith('(') ? _name + tail : $"{_name}({tail})";
        state.Expression = state.Expression[..start] + wrapped;

        if (TryEvaluate(wrapped, state, out var value))
        {
            state.Display = DisplayFormat.Format(value);
        }
        else
        {
            state.Display = DisplayFormat.ErrorText;
            state.Expression = ExpressionText.CloseParens(state.Expression) + " =";
            state.Phase = CalcPhase.AfterEquals;
        }
    }

    private static bool TryEvaluate(string expression, CalcState state, out decimal value)
    {
        try
        {
            value = Evaluator.Evaluate(expression, state.AngleMode);
            return true;
        }
        catch (Exception ex) when (ex is CalcParseException or CalcEvalException)
        {
            value = 0m;
            return false;
        }
    }

    private static decimal ParseOrZero(string text) =>
        DisplayFormat.TryParse(text, out var v) ? v : 0m;
}

/// <summary>Inserts π or e as the current operand.</summary>
public sealed class ConstantCommand : BaseCalcCommand
{
    private readonly string _name;

    public ConstantCommand(string name) => _name = name;

    public override string Description => $"const {_name}";

    protected override void Apply(CalcState state)
    {
        var value = _name.ToLowerInvariant() switch
        {
            "pi" => (decimal)Math.PI,
            "e" => (decimal)Math.E,
            _ => throw new ArgumentException($"Unknown constant '{_name}'")
        };
        ExpressionText.EnterValue(state, value);
    }
}

/// <summary>Inserts an arbitrary value (history entry, clipboard) as the current operand.</summary>
public sealed class EnterValueCommand : BaseCalcCommand
{
    private readonly decimal _value;

    public EnterValueCommand(decimal value) => _value = value;

    public override string Description => $"value {_value}";

    protected override void Apply(CalcState state) => ExpressionText.EnterValue(state, _value);
}
