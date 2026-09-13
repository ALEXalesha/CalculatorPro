namespace CalcPro.Core.Models;

public sealed record HistoryEntry(string Expression, string Result, DateTime Timestamp)
{
    public string Display => $"{Expression} = {Result}";
}
