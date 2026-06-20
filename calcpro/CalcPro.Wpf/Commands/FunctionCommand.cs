using System.Globalization;
using CalcPro.Wpf.Models;
using CalcPro.Wpf.Services;

namespace CalcPro.Wpf.Commands;

/// <summary>
/// Applies a unary function (sin, cos, sqrt, etc.) immediately to the current
/// display value. Replaces the display with the function's result.
/// </summary>
public sealed class FunctionCommand : BaseCalcCommand
{
    private readonly string _name;

    public FunctionCommand(string name) => _name = name;

    public override string Description => $"f({_name})";

    protected override void Apply(CalcState state)
    {
        var argExpr = $"{_name}({state.Display})";
        try
        {
            var result = Evaluator.Evaluate(argExpr, state.AngleMode);
            state.Display = EqualsCommand.FormatResult(result);
            state.Phase = CalcPhase.EnteringDigit;
        }
        catch (Exception ex) when (ex is CalcParseException or CalcEvalException)
        {
            state.Display = "Error";
            state.Phase = CalcPhase.AfterEquals;
        }
    }
}

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
            _ => 0m
        };
        state.Display = value.ToString("0.############", CultureInfo.InvariantCulture);
        state.Phase = CalcPhase.EnteringDigit;
    }
}
