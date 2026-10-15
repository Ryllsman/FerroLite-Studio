namespace Ferrolite.Core.Lang;

public class ParseError : Exception
{
    public override string Message { get; }
    public int Line { get; }
    public int Col { get; }

    public ParseError(string message, int line, int col)
        : base($"Syntax error at line {line}, column {col}: {message}")
    {
        Message = message;
        Line = line;
        Col = col;
    }
}

public class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public Parser(List<Token> tokens)
    {
        _tokens = new List<Token>(tokens);
    }

    private Token Peek(int offset = 0)
    {
        int idx = _pos + offset;
        return idx >= _tokens.Count ? _tokens[^1] : _tokens[idx];
    }

    private bool AtEnd() => Peek().Type == TokenType.Eof;

    private Token Advance()
    {
        var tok = _tokens[_pos];
        if (_pos < _tokens.Count - 1) _pos++;
        return tok;
    }

    private bool Check(TokenType ttype) => Peek().Type == ttype;

    private Token? Match(params TokenType[] types)
    {
        if (types.Contains(Peek().Type))
            return Advance();
        return null;
    }

    private Token Expect(TokenType ttype, string? msg = null)
    {
        if (Check(ttype)) return Advance();
        var tok = Peek();
        throw new ParseError(msg ?? $"Expected {ttype}, got {tok.Type}", tok.Line, tok.Col);
    }

    private void SkipNewlines()
    {
        while (Check(TokenType.Newline) || Check(TokenType.Semi))
            Advance();
    }

    private void SkipTerminator()
    {
        if (Check(TokenType.Newline) || Check(TokenType.Semi))
            SkipNewlines();
        else if (Check(TokenType.Eof) || Check(TokenType.RBrace))
            return;
    }

    public Program ParseProgram()
    {
        var stmts = new List<AstNode>();
        SkipNewlines();
        while (!AtEnd())
        {
            stmts.Add(Statement());
            SkipNewlines();
        }
        return new Program(stmts);
    }

    private Block Block()
    {
        int line = Peek().Line;
        Expect(TokenType.LBrace, "Expected '{'");
        SkipNewlines();
        var stmts = new List<AstNode>();
        while (!Check(TokenType.RBrace) && !AtEnd())
        {
            stmts.Add(Statement());
            SkipNewlines();
        }
        Expect(TokenType.RBrace, "Expected '}'");
        return new Block(stmts, line);
    }

    private AstNode AdvanceAndBreak(Token tok) { Advance(); return new BreakStmt(tok.Line); }
    private AstNode AdvanceAndContinue(Token tok) { Advance(); return new ContinueStmt(tok.Line); }
    private AstNode AdvanceAndThrow(Token tok) { Advance(); return new ThrowStmt(Expression(), tok.Line); }

    private AstNode Statement()
    {
        var tok = Peek();
        return tok.Type switch
        {
            TokenType.Let => LetStmt(),
            TokenType.If => IfStmt(),
            TokenType.While => WhileStmt(),
            TokenType.For => ForStmt(),
            TokenType.Func => FuncDecl(),
            TokenType.Return => ReturnStmt(),
            TokenType.Break => AdvanceAndBreak(tok),
            TokenType.Continue => AdvanceAndContinue(tok),
            TokenType.Try => TryStmt(),
            TokenType.Throw => AdvanceAndThrow(tok),
            TokenType.Import => ParseImport(tok),
            TokenType.LBrace => Block(),
            _ => new ExprStmt(Expression(), tok.Line),
        };
    }

    private AstNode ParseImport(Token tok)
    {
        Advance(); 
        var pathTok = Expect(TokenType.String, "Expected string path after import");
        return new ImportStmt((string)pathTok.Value!, tok.Line);
    }

    private LetStmt LetStmt()
    {
        int line = Advance().Line;
        string name = Expect(TokenType.Ident, "Expected variable name").Value!.ToString()!;
        AstNode? value = null;
        if (Match(TokenType.Assign) != null)
            value = Expression();
        return new LetStmt(name, value, line);
    }

    private IfStmt IfStmt()
    {
        int line = Advance().Line;
        var cond = Expression();
        var thenB = Block();
        AstNode? elseB = null;

        
        int save = _pos;
        int i = _pos;
        while (i < _tokens.Count && _tokens[i].Type == TokenType.Newline) i++;
        if (i < _tokens.Count && _tokens[i].Type == TokenType.Else)
            _pos = i;

        if (Match(TokenType.Else) != null)
        {
            if (Check(TokenType.If))
                elseB = IfStmt();
            else
                elseB = Block();
        }
        return new IfStmt(cond, thenB, elseB, line);
    }

    private WhileStmt WhileStmt()
    {
        int line = Advance().Line;
        var cond = Expression();
        var body = Block();
        return new WhileStmt(cond, body, line);
    }

    private ForStmt ForStmt()
    {
        int line = Advance().Line;
        string varName = Expect(TokenType.Ident, "Expected loop variable").Value!.ToString()!;
        Expect(TokenType.In, "Expected 'in'");
        var iterable = Expression();
        var body = Block();
        return new ForStmt(varName, iterable, body, line);
    }

    private FuncDecl FuncDecl()
    {
        int line = Advance().Line;
        string name = Expect(TokenType.Ident, "Expected function name").Value!.ToString()!;
        Expect(TokenType.LParen, "Expected '('");
        var parameters = ParseParamList();
        Expect(TokenType.RParen, "Expected ')'");
        var body = Block();
        return new FuncDecl(name, parameters, body, line);
    }

    private ReturnStmt ReturnStmt()
    {
        int line = Advance().Line;
        if (Check(TokenType.Newline) || Check(TokenType.Semi) || Check(TokenType.RBrace))
            return new ReturnStmt(null, line);
        return new ReturnStmt(Expression(), line);
    }

    private TryStmt TryStmt()
    {
        int line = Advance().Line;
        var tryBlock = Block();
        SkipNewlines();
        Expect(TokenType.Catch, "Expected 'catch' after try block");
        string? catchVar = null;
        if (Match(TokenType.LParen) != null)
        {
            catchVar = Expect(TokenType.Ident, "Expected catch variable").Value!.ToString()!;
            Expect(TokenType.RParen, "Expected ')'");
        }
        var catchBlock = Block();
        return new TryStmt(tryBlock, catchVar, catchBlock, line);
    }

    private AstNode Expression() => Assignment();

    private AstNode Assignment()
    {
        var expr = OrExpr();
        if (Peek().Type is TokenType.Assign or TokenType.PlusAssign or TokenType.MinusAssign)
        {
            var opTok = Advance();
            var value = Assignment();
            string op = opTok.Type switch
            {
                TokenType.Assign => "=",
                TokenType.PlusAssign => "+=",
                TokenType.MinusAssign => "-=",
                _ => "=",
            };
            if (expr is not (Identifier or Index or MemberAccess))
                throw new ParseError("Invalid assignment target", opTok.Line, opTok.Col);
            return new Assign(expr, value, op, opTok.Line);
        }
        return expr;
    }

    private AstNode OrExpr()
    {
        var expr = AndExpr();
        while (Peek().Type == TokenType.Or)
        {
            var op = Advance();
            var right = AndExpr();
            expr = new LogicalOp("or", expr, right, op.Line);
        }
        return expr;
    }

    private AstNode AndExpr()
    {
        var expr = Equality();
        while (Peek().Type == TokenType.And)
        {
            var op = Advance();
            var right = Equality();
            expr = new LogicalOp("and", expr, right, op.Line);
        }
        return expr;
    }

    private AstNode Equality()
    {
        var expr = Comparison();
        while (Peek().Type is TokenType.Eq or TokenType.Neq)
        {
            var op = Advance();
            var right = Comparison();
            expr = new BinaryOp(op.Value!.ToString()!, expr, right, op.Line);
        }
        return expr;
    }

    private AstNode Comparison()
    {
        var expr = Additive();
        while (Peek().Type is TokenType.Lt or TokenType.Gt or TokenType.Lte or TokenType.Gte)
        {
            var op = Advance();
            var right = Additive();
            expr = new BinaryOp(op.Value!.ToString()!, expr, right, op.Line);
        }
        return expr;
    }

    private AstNode Additive()
    {
        var expr = Multiplicative();
        while (Peek().Type is TokenType.Plus or TokenType.Minus)
        {
            var op = Advance();
            var right = Multiplicative();
            expr = new BinaryOp(op.Value!.ToString()!, expr, right, op.Line);
        }
        return expr;
    }

    private AstNode Multiplicative()
    {
        var expr = Unary();
        while (Peek().Type is TokenType.Star or TokenType.Slash or TokenType.Percent)
        {
            var op = Advance();
            var right = Unary();
            expr = new BinaryOp(op.Value!.ToString()!, expr, right, op.Line);
        }
        return expr;
    }

    private AstNode Unary()
    {
        if (Peek().Type is TokenType.Minus or TokenType.Not)
        {
            var op = Advance();
            var operand = Unary();
            string sym = op.Type == TokenType.Minus ? "-" : "not";
            return new UnaryOp(sym, operand, op.Line);
        }
        return CallIndex();
    }

    private AstNode CallIndex()
    {
        var expr = Primary();
        while (true)
        {
            if (Match(TokenType.LParen) != null)
            {
                var args = ParseArgList();
                Expect(TokenType.RParen, "Expected ')'");
                expr = new Call(expr, args, expr.Line);
            }
            else if (Match(TokenType.LBracket) != null)
            {
                var idx = Expression();
                Expect(TokenType.RBracket, "Expected ']'");
                expr = new Index(expr, idx, expr.Line);
            }
            else if (Match(TokenType.Dot) != null)
            {
                string name = Expect(TokenType.Ident, "Expected property name after '.'").Value!.ToString()!;
                expr = new MemberAccess(expr, name, expr.Line);
            }
            else break;
        }
        return expr;
    }

    private AstNode Primary()
    {
        var tok = Peek();
        switch (tok.Type)
        {
            case TokenType.Number:
                Advance();
                return new NumberLit((double)tok.Value!, tok.Line);
            case TokenType.String:
            case TokenType.TemplateString:
                Advance();
                return new StringLit((string)tok.Value!, tok.Line);
            case TokenType.Boolean:
                Advance();
                return new BoolLit((bool)tok.Value!, tok.Line);
            case TokenType.Null:
                Advance();
                return new NullLit(tok.Line);
            case TokenType.Ident:
                Advance();
                return new Identifier(tok.Value!.ToString()!, tok.Line);
            case TokenType.LParen:
                Advance();
                var expr = Expression();
                Expect(TokenType.RParen, "Expected ')'");
                return expr;
            case TokenType.LBracket:
                Advance();
                var elements = new List<AstNode>();
                SkipNewlines();
                if (!Check(TokenType.RBracket))
                {
                    elements.Add(Expression());
                    SkipNewlines();
                    while (Match(TokenType.Comma) != null)
                    {
                        SkipNewlines();
                        if (Check(TokenType.RBracket)) break;
                        elements.Add(Expression());
                        SkipNewlines();
                    }
                }
                Expect(TokenType.RBracket, "Expected ']'");
                return new ListLit(elements, tok.Line);
            case TokenType.LBrace:
                Advance();
                var keys = new List<AstNode>();
                var values = new List<AstNode>();
                SkipNewlines();
                if (!Check(TokenType.RBrace))
                {
                    ParseMapEntry(keys, values);
                    SkipNewlines();
                    while (Match(TokenType.Comma) != null)
                    {
                        SkipNewlines();
                        if (Check(TokenType.RBrace)) break;
                        ParseMapEntry(keys, values);
                        SkipNewlines();
                    }
                }
                Expect(TokenType.RBrace, "Expected '}'");
                return new MapLit(keys, values, tok.Line);
            case TokenType.Func:
                return FuncExpr();
            default:
                throw new ParseError($"Unexpected token {tok.Type}", tok.Line, tok.Col);
        }
    }

    private void ParseMapEntry(List<AstNode> keys, List<AstNode> values)
    {
        AstNode keyNode;
        if (Check(TokenType.String) || Check(TokenType.Ident))
        {
            var keyTok = Advance();
            keyNode = new StringLit(keyTok.Value!.ToString()!, keyTok.Line);
        }
        else
        {
            keyNode = Expression();
        }
        Expect(TokenType.Colon, "Expected ':' in map literal");
        var valNode = Expression();
        keys.Add(keyNode);
        values.Add(valNode);
    }

    private FuncExpr FuncExpr()
    {
        int line = Advance().Line;
        Expect(TokenType.LParen, "Expected '('");
        var parameters = ParseParamList();
        Expect(TokenType.RParen, "Expected ')'");
        if (Match(TokenType.Arrow) != null)
        {
            var body = Expression();
            return new FuncExpr(parameters, body, false, line);
        }
        else
        {
            var body = Block();
            return new FuncExpr(parameters, body, true, line);
        }
    }

    private List<string> ParseParamList()
    {
        var parameters = new List<string>();
        if (!Check(TokenType.RParen))
        {
            parameters.Add(Expect(TokenType.Ident, "Expected parameter name").Value!.ToString()!);
            while (Match(TokenType.Comma) != null)
                parameters.Add(Expect(TokenType.Ident, "Expected parameter name").Value!.ToString()!);
        }
        return parameters;
    }

    private List<AstNode> ParseArgList()
    {
        var args = new List<AstNode>();
        if (!Check(TokenType.RParen))
        {
            args.Add(Expression());
            while (Match(TokenType.Comma) != null)
                args.Add(Expression());
        }
        return args;
    }

    public static Program Parse(List<Token> tokens)
    {
        return new Parser(tokens).ParseProgram();
    }
}
