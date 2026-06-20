using System.Globalization;
using System.Text;

namespace CalcPro.Wpf.Services;

/// <summary>
/// Lexes an input expression into a stream of tokens. Pure function — no state
/// carried between calls. Throws <see cref="CalcParseException"/> on bad input.
/// </summary>
public static class Tokenizer
{
    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "sin", "cos", "tan", "asin", "acos", "atan",
        "log", "ln", "exp", "sqrt", "abs", "sqr", "inv"
    };

    private static readonly HashSet<string> Constants = new(StringComparer.OrdinalIgnoreCase)
    {
        "pi", "e"
    };

    public static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        var i = 0;
        var src = input ?? string.Empty;

        while (i < src.Length)
        {
            var c = src[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || c == '.')
            {
                var start = i;
                var sb = new StringBuilder();
                var sawDot = false;
                while (i < src.Length && (char.IsDigit(src[i]) || src[i] == '.'))
                {
                    if (src[i] == '.')
                    {
                        if (sawDot) throw new CalcParseException($"Unexpected '.' at {i}");
                        sawDot = true;
                    }
                    sb.Append(src[i]);
                    i++;
                }
                var text = sb.ToString();
                if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                    throw new CalcParseException($"Bad number '{text}' at {start}");
                tokens.Add(new Token(TokenKind.Number, text, num, start));
                continue;
            }

            if (char.IsLetter(c))
            {
                var start = i;
                var sb = new StringBuilder();
                while (i < src.Length && char.IsLetter(src[i]))
                {
                    sb.Append(src[i]);
                    i++;
                }
                var word = sb.ToString();
                if (Functions.Contains(word))
                    tokens.Add(new Token(TokenKind.Function, word.ToLowerInvariant(), 0m, start));
                else if (Constants.Contains(word))
                    tokens.Add(new Token(TokenKind.Constant, word.ToLowerInvariant(), 0m, start));
                else
                    throw new CalcParseException($"Unknown identifier '{word}' at {start}");
                continue;
            }

            switch (c)
            {
                case '+': tokens.Add(new Token(TokenKind.Plus, "+", 0m, i)); i++; break;
                case '-': tokens.Add(new Token(TokenKind.Minus, "-", 0m, i)); i++; break;
                case '*':
                case '×': tokens.Add(new Token(TokenKind.Star, "*", 0m, i)); i++; break;
                case '/':
                case '÷': tokens.Add(new Token(TokenKind.Slash, "/", 0m, i)); i++; break;
                case '%': tokens.Add(new Token(TokenKind.Percent, "%", 0m, i)); i++; break;
                case '^': tokens.Add(new Token(TokenKind.Caret, "^", 0m, i)); i++; break;
                case '(': tokens.Add(new Token(TokenKind.LParen, "(", 0m, i)); i++; break;
                case ')': tokens.Add(new Token(TokenKind.RParen, ")", 0m, i)); i++; break;
                case '!': tokens.Add(new Token(TokenKind.Bang, "!", 0m, i)); i++; break;
                default:
                    throw new CalcParseException($"Unexpected character '{c}' at {i}");
            }
        }

        tokens.Add(new Token(TokenKind.End, "", 0m, src.Length));
        return tokens;
    }
}

public sealed class CalcParseException : Exception
{
    public CalcParseException(string message) : base(message) { }
}

public sealed class CalcEvalException : Exception
{
    public CalcEvalException(string message) : base(message) { }
}
