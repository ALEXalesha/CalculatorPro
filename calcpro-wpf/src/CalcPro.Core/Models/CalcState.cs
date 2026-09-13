namespace CalcPro.Core.Models;

/// <summary>
/// Snapshot of calculator state. Mutable, owned by ViewModel.
/// Commands read and write this through controlled methods only.
/// </summary>
public sealed class CalcState
{
    /// Full expression being assembled, e.g. "2 + 3 *".
    public string Expression { get; set; } = string.Empty;

    /// Number currently shown on the main display.
    public string Display { get; set; } = "0";

    /// Last successful evaluation result (decimal), null if none.
    public decimal? LastResult { get; set; }

    /// Memory cell (M+, M-, MS, MR).
    public decimal Memory { get; set; }

    public CalcMode Mode { get; set; } = CalcMode.Standard;

    public AngleMode AngleMode { get; set; } = AngleMode.Deg;

    public CalcPhase Phase { get; set; } = CalcPhase.Idle;

    public CalcState Clone() => new()
    {
        Expression = Expression,
        Display = Display,
        LastResult = LastResult,
        Memory = Memory,
        Mode = Mode,
        AngleMode = AngleMode,
        Phase = Phase
    };
}
