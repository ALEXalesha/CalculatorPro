namespace CalcPro.Core.Services;

public enum TokenKind
{
    Number,
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Caret,
    LParen,
    RParen,
    Function,
    Constant,
    Bang,
    End
}

public readonly record struct Token(TokenKind Kind, string Text, decimal Number = 0m, int Position = 0)
{
    public override string ToString() => Kind == TokenKind.Number
        ? $"Num({Number})"
        : $"{Kind}({Text})";
}
