namespace CalcPro.Core.Services;

public abstract record AstNode;

public sealed record NumberNode(decimal Value) : AstNode;

public sealed record ConstantNode(string Name) : AstNode;

public sealed record UnaryOpNode(string Op, AstNode Operand) : AstNode;

public sealed record BinaryOpNode(string Op, AstNode Left, AstNode Right) : AstNode;

public sealed record FunctionCallNode(string Name, AstNode Argument) : AstNode;

/// Postfix percent: x% → x / 100
public sealed record PercentNode(AstNode Operand) : AstNode;

/// Postfix factorial: x! → x * (x-1) * ... * 1
public sealed record FactorialNode(AstNode Operand) : AstNode;
