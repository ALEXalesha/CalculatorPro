namespace CalcPro.Core.Models;

public enum CalcMode
{
    Standard,
    Scientific,
    /// <summary>Целые 64 бита в HEX, DEC, OCT и BIN (1.6.0), см. ProgrammerCalculator.</summary>
    Programmer
}
