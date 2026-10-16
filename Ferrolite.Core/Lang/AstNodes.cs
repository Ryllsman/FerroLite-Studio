namespace Ferrolite.Core.Lang;

public abstract record AstNode(int Line = 0);


public record NumberLit(double Value, int Line = 0) : AstNode(Line);
public record StringLit(string Value, int Line = 0) : AstNode(Line);
public record BoolLit(bool Value, int Line = 0) : AstNode(Line);
public record NullLit(int Line = 0) : AstNode(Line);
public record ListLit(List<AstNode> Elements, int Line = 0) : AstNode(Line);
public record MapLit(List<AstNode> Keys, List<AstNode> Values, int Line = 0) : AstNode(Line);


public record Identifier(string Name, int Line = 0) : AstNode(Line);
public record Assign(AstNode Target, AstNode Value, string Op = "=", int Line = 0) : AstNode(Line);
public record BinaryOp(string Op, AstNode Left, AstNode Right, int Line = 0) : AstNode(Line);
public record UnaryOp(string Op, AstNode Operand, int Line = 0) : AstNode(Line);
public record LogicalOp(string Op, AstNode Left, AstNode Right, int Line = 0) : AstNode(Line);
public record Call(AstNode Callee, List<AstNode> Args, int Line = 0) : AstNode(Line);
public record Index(AstNode Obj, AstNode IndexExpr, int Line = 0) : AstNode(Line);
public record MemberAccess(AstNode Obj, string Name, int Line = 0) : AstNode(Line);
public record FuncExpr(List<string> Params, AstNode Body, bool IsBlock = false, int Line = 0) : AstNode(Line);


public record Program(List<AstNode> Statements) : AstNode(0);
public record Block(List<AstNode> Statements, int Line = 0) : AstNode(Line);
public record LetStmt(string Name, AstNode? Value, int Line = 0) : AstNode(Line);
public record ExprStmt(AstNode Expr, int Line = 0) : AstNode(Line);
public record IfStmt(AstNode Condition, Block ThenBranch, AstNode? ElseBranch = null, int Line = 0) : AstNode(Line);
public record WhileStmt(AstNode Condition, Block Body, int Line = 0) : AstNode(Line);
public record ForStmt(string VarName, AstNode Iterable, Block Body, int Line = 0) : AstNode(Line);
public record FuncDecl(string Name, List<string> Params, Block Body, int Line = 0) : AstNode(Line);
public record ReturnStmt(AstNode? Value, int Line = 0) : AstNode(Line);
public record BreakStmt(int Line = 0) : AstNode(Line);
public record ContinueStmt(int Line = 0) : AstNode(Line);
public record TryStmt(Block TryBlock, string? CatchVar, Block CatchBlock, int Line = 0) : AstNode(Line);
public record ThrowStmt(AstNode Value, int Line = 0) : AstNode(Line);
public record ImportStmt(string Path, int Line = 0) : AstNode(Line);
