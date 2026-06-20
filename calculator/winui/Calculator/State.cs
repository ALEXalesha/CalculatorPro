using System.Collections.Generic;

namespace CalcPro.Calculator;

public enum CalcMode { Standard, Scientific, Fractions }

/// <summary>
/// Single fraction-mode term: an integer + a num/den stack + the trailing
/// binary operator that joins it to the next term. Mirrors JS state.fracTerms.
/// </summary>
public class FracTerm
{
    public string Whole { get; set; } = "";
    public string Num   { get; set; } = "";
    public string Den   { get; set; } = "";
    public string Op    { get; set; } = ""; // "+", "-", "*", "/", or ""
}

public enum FracSlot { Whole, Num, Den }

public record HistoryEntry(string Expr, double Result, long Ts);

public class CalcState
{
    // Standard/scientific input
    public string Expression { get; set; } = "";
    public string Display { get; set; } = "0";
    public string HistoryLine { get; set; } = "";
    public double? Result { get; set; }
    public bool JustEvaluated { get; set; }
    public bool HasError { get; set; }

    // Modes
    public CalcMode Mode { get; set; } = CalcMode.Standard;
    public AngleMode Angle { get; set; } = AngleMode.Deg;
    public bool FractionMode { get; set; } // D⇄F toggle
    public bool AltSet { get; set; }       // 2nd key flips trig/log/power buttons

    // Persistent
    public double Memory { get; set; }
    public List<HistoryEntry> CalcHistory { get; set; } = new();

    // Fraction-mode UI state
    public List<FracTerm> FracTerms { get; set; } = new() { new FracTerm() };
    public int FracTermIdx { get; set; }
    public FracSlot FracSlot { get; set; } = FracSlot.Whole;
    public double? FracResult { get; set; }
    public Fraction? FracResultFrac { get; set; }

    public FracTerm CurrentFracTerm => FracTerms[FracTermIdx];

    public void FracResetAll()
    {
        FracTerms = new List<FracTerm> { new FracTerm() };
        FracTermIdx = 0;
        FracSlot = FracSlot.Whole;
        FracResult = null;
        FracResultFrac = null;
        HasError = false;
    }
}
