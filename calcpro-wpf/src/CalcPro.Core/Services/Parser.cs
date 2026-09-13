namespace CalcPro.Core.Services;

/// <summary>
/// Pratt parser: each token kind has a left-binding-power (lbp) and either a
/// null-denotation handler (nud — used when token starts an expression) or
/// a left-denotation handler (led — used when token follows an expression).
///
/// Precedence (right-assoc marked R):
///   call/parens      120
///   postfix ! %      110 (tighter than ^ and unary: 2^3! = 2^6, -3! = -(3!))
///   ^                100 R
///   unary - +         90
///   * /               80
///   + -               70
/// </summary>
public sealed class Parser
{
    /// Recursion guard: "((((…" or "----…" deeper than this is rejected with a
    /// CalcParseException instead of overflowing the stack (which can't be caught).
    public const int MaxDepth = 256;

    /// "1+1+…" parses iteratively but yields a left-deep tree the evaluator walks
    /// recursively; capping the token count bounds that depth too.
    public const int MaxTokens = 1000;

    private readonly List<Token> _tokens;
    private int _pos;
    private int _depth;

    public Parser(List<Token> tokens) => _tokens = tokens;

    public static AstNode Parse(string input)
    {
        var tokens = Tokenizer.Tokenize(input);
        if (tokens.Count > MaxTokens)
            throw new CalcParseException("Expression is too long");
        var parser = new Parser(tokens);
        var ast = parser.Expression(0);
        if (parser.Peek().Kind != TokenKind.End)
            throw new CalcParseException($"Unexpected token '{parser.Peek().Text}' at {parser.Peek().Position}");
        return ast;
    }

    private Token Peek() => _tokens[_pos];
    private Token Consume() => _tokens[_pos++];

    private Token Expect(TokenKind kind)
    {
        var tok = Peek();
        if (tok.Kind != kind)
            throw new CalcParseException($"Expected {kind} but found '{tok.Text}' at {tok.Position}");
        return Consume();
    }

    public AstNode Expression(int rbp)
    {
        if (++_depth > MaxDepth)
            throw new CalcParseException("Expression is nested too deeply");
        try
        {
            var left = Nud(Consume());
            while (rbp < Lbp(Peek()))
            {
                left = Led(Consume(), left);
            }
            return left;
        }
        finally
        {
            _depth--;
        }
    }

    private AstNode Nud(Token token) => token.Kind switch
    {
        TokenKind.Number => new NumberNode(token.Number),
        TokenKind.Constant => new ConstantNode(token.Text),
        TokenKind.Minus => new UnaryOpNode("-", Expression(90)),
        TokenKind.Plus => Expression(90),
        TokenKind.LParen => ParseParenExpression(),
        TokenKind.Function => ParseFunctionCall(token),
        _ => throw new CalcParseException($"Unexpected token '{token.Text}' at {token.Position}")
    };

    private AstNode ParseParenExpression()
    {
        var inner = Expression(0);
        Expect(TokenKind.RParen);
        return inner;
    }

    private AstNode ParseFunctionCall(Token func)
    {
        Expect(TokenKind.LParen);
        var arg = Expression(0);
        Expect(TokenKind.RParen);
        return new FunctionCallNode(func.Text, arg);
    }

    private AstNode Led(Token token, AstNode left) => token.Kind switch
    {
        TokenKind.Plus => new BinaryOpNode("+", left, Expression(70)),
        TokenKind.Minus => new BinaryOpNode("-", left, Expression(70)),
        TokenKind.Star => new BinaryOpNode("*", left, Expression(80)),
        TokenKind.Slash => new BinaryOpNode("/", left, Expression(80)),
        // ^ is right-associative: parse RHS with rbp = lbp - 1
        TokenKind.Caret => new BinaryOpNode("^", left, Expression(99)),
        TokenKind.Percent => new PercentNode(left),
        TokenKind.Bang => new FactorialNode(left),
        _ => throw new CalcParseException($"Unexpected infix token '{token.Text}' at {token.Position}")
    };

    private static int Lbp(Token token) => token.Kind switch
    {
        TokenKind.Plus or TokenKind.Minus => 70,
        TokenKind.Star or TokenKind.Slash => 80,
        TokenKind.Percent or TokenKind.Bang => 110,
        TokenKind.Caret => 100,
        TokenKind.LParen => 120,
        _ => 0
    };
}
