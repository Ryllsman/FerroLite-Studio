namespace Ferrolite.Core.Lang;

public enum TokenType
{
    
    Number,
    String,
    TemplateString,
    Boolean,
    Ident,

    
    Let, If, Else, While, For, In, Func, Return, Break, Continue,
    Try, Catch, Throw, True, False, Null, Import,

    
    Plus, Minus, Star, Slash, Percent,
    Assign, PlusAssign, MinusAssign,
    Eq, Neq, Lt, Gt, Lte, Gte,
    And, Or, Not,

    
    LParen, RParen, LBrace, RBrace, LBracket, RBracket,
    Comma, Dot, Colon, Semi, Arrow,

    
    Newline,
    Eof
}

public readonly record struct Token(TokenType Type, object? Value, int Line, int Col)
{
    public override string ToString() => $"Token({Type}, {Value?.ToString() ?? "null"}, L{Line}:C{Col})";
}

public class LexError : Exception
{
    public override string Message { get; }
    public int Line { get; }
    public int Col { get; }

    public LexError(string message, int line, int col)
        : base($"Lexical error at line {line}, column {col}: {message}")
    {
        Message = message;
        Line = line;
        Col = col;
    }
}
