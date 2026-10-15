using Ferrolite.Core.Lang;

namespace Ferrolite.Core.Tests;

public class LexerTests
{
    [Fact]
    public void TokenizeSimplePrint()
    {
        var tokens = new Lexer("print(\"hello\")").Tokenize();
        Assert.Equal(TokenType.Ident, tokens[0].Type);
        Assert.Equal("print", tokens[0].Value);
        Assert.Equal(TokenType.LParen, tokens[1].Type);
        Assert.Equal(TokenType.String, tokens[2].Type);
        Assert.Equal("hello", tokens[2].Value);
        Assert.Equal(TokenType.RParen, tokens[3].Type);
        Assert.Equal(TokenType.Eof, tokens[4].Type);
    }

    [Fact]
    public void TokenizeKeywords()
    {
        var tokens = new Lexer("let x = 5").Tokenize();
        Assert.Equal(TokenType.Let, tokens[0].Type);
        Assert.Equal(TokenType.Ident, tokens[1].Type);
        Assert.Equal("x", tokens[1].Value);
        Assert.Equal(TokenType.Assign, tokens[2].Type);
        Assert.Equal(TokenType.Number, tokens[3].Type);
        Assert.Equal(5.0, tokens[3].Value);
    }

    [Fact]
    public void TokenizeComments()
    {
        var tokens = new Lexer("# this is a comment\nprint(1)").Tokenize();
        
        var nonNewline = tokens.Where(t => t.Type != TokenType.Newline && t.Type != TokenType.Eof).ToList();
        Assert.Equal(TokenType.Ident, nonNewline[0].Type);
        Assert.Equal("print", nonNewline[0].Value);
    }

    [Fact]
    public void TokenizeBlockComment()
    {
        var tokens = new Lexer("/* comment */ print(1)").Tokenize();
        Assert.Equal(TokenType.Ident, tokens[0].Type);
    }

    [Fact]
    public void TokenizeStringEscapes()
    {
        var tokens = new Lexer(@"""hello\nworld""").Tokenize();
        Assert.Equal("hello\nworld", tokens[0].Value);
    }

    [Fact]
    public void TokenizeOperators()
    {
        var tokens = new Lexer("a + b == c").Tokenize();
        Assert.Equal(TokenType.Plus, tokens[1].Type);
        Assert.Equal(TokenType.Eq, tokens[3].Type);
    }
}

public class ParserTests
{
    [Fact]
    public void ParseLetStatement()
    {
        var tokens = new Lexer("let x = 5").Tokenize();
        var program = Parser.Parse(tokens);
        Assert.Single(program.Statements);
        Assert.IsType<LetStmt>(program.Statements[0]);
    }

    [Fact]
    public void ParseIfElse()
    {
        var src = "if true { print(1) } else { print(2) }";
        var tokens = new Lexer(src).Tokenize();
        var program = Parser.Parse(tokens);
        Assert.Single(program.Statements);
        Assert.IsType<IfStmt>(program.Statements[0]);
    }

    [Fact]
    public void ParseFunctionDeclaration()
    {
        var src = "func add(a, b) { return a + b }";
        var tokens = new Lexer(src).Tokenize();
        var program = Parser.Parse(tokens);
        Assert.Single(program.Statements);
        var func = Assert.IsType<FuncDecl>(program.Statements[0]);
        Assert.Equal("add", func.Name);
        Assert.Equal(2, func.Params.Count);
    }

    [Fact]
    public void ParseWhileLoop()
    {
        var src = "while true { break }";
        var tokens = new Lexer(src).Tokenize();
        var program = Parser.Parse(tokens);
        Assert.Single(program.Statements);
        Assert.IsType<WhileStmt>(program.Statements[0]);
    }

    [Fact]
    public void ParseForLoop()
    {
        var src = "for i in [1, 2, 3] { print(i) }";
        var tokens = new Lexer(src).Tokenize();
        var program = Parser.Parse(tokens);
        Assert.Single(program.Statements);
        var forStmt = Assert.IsType<ForStmt>(program.Statements[0]);
        Assert.Equal("i", forStmt.VarName);
    }

    [Fact]
    public void ParseTryCatch()
    {
        var src = "try { throw \"error\" } catch(e) { print(e) }";
        var tokens = new Lexer(src).Tokenize();
        var program = Parser.Parse(tokens);
        Assert.Single(program.Statements);
        var tryStmt = Assert.IsType<TryStmt>(program.Statements[0]);
        Assert.Equal("e", tryStmt.CatchVar);
    }

    [Fact]
    public void ParseLambda()
    {
        var src = "let f = func(x) => x * 2";
        var tokens = new Lexer(src).Tokenize();
        var program = Parser.Parse(tokens);
        var letStmt = Assert.IsType<LetStmt>(program.Statements[0]);
        Assert.IsType<FuncExpr>(letStmt.Value);
    }

    [Fact]
    public void ParseMapLiteral()
    {
        var src = "let m = { a: 1, b: 2 }";
        var tokens = new Lexer(src).Tokenize();
        var program = Parser.Parse(tokens);
        var letStmt = Assert.IsType<LetStmt>(program.Statements[0]);
        var map = Assert.IsType<MapLit>(letStmt.Value);
        Assert.Equal(2, map.Keys.Count);
    }
}

public class InterpreterTests
{
    private (string output, object? result) Run(string code)
    {
        var lines = new List<string>();
        var backend = new NullBackend();
        var interp = new Interpreter(backend, s => lines.Add(s));
        var tokens = new Lexer(code).Tokenize();
        var program = Parser.Parse(tokens);
        interp.Run(program);
        return (string.Join("\n", lines), null);
    }

    [Fact]
    public void RunPrint()
    {
        var (output, _) = Run("print(\"hello world\")");
        Assert.Contains("hello world", output);
    }

    [Fact]
    public void RunMath()
    {
        var (output, _) = Run("print(2 + 3 * 4)");
        Assert.Contains("14", output);
    }

    [Fact]
    public void RunVariable()
    {
        var (output, _) = Run("let x = 10\nprint(x)");
        Assert.Contains("10", output);
    }

    [Fact]
    public void RunIfElse()
    {
        var (output, _) = Run("let x = 5\nif x > 3 { print(\"big\") } else { print(\"small\") }");
        Assert.Contains("big", output);
    }

    [Fact]
    public void RunWhileLoop()
    {
        var (output, _) = Run("let i = 0\nwhile i < 5 { print(i)\ni += 1 }");
        Assert.Contains("0", output);
        Assert.Contains("4", output);
    }

    [Fact]
    public void RunFunction()
    {
        var (output, _) = Run("func double(x) { return x * 2 }\nprint(double(5))");
        Assert.Contains("10", output);
    }

    [Fact]
    public void RunStringOps()
    {
        var (output, _) = Run("let s = \"Hello World\"\nprint(len(s))\nprint(upper(\"abc\"))\nprint(lower(\"ABC\"))");
        Assert.Contains("11", output);
        Assert.Contains("ABC", output);
        Assert.Contains("abc", output);
    }

    [Fact]
    public void RunListOps()
    {
        var (output, _) = Run("let lst = [3, 1, 2]\nprint(len(lst))\nprint(sort(lst))");
        Assert.Contains("3", output);
        Assert.Contains("[1, 2, 3]", output);
    }

    [Fact]
    public void RunMapOps()
    {
        var (output, _) = Run("let m = { x: 10, y: 20 }\nprint(m.x)\nprint(m.y)");
        Assert.Contains("10", output);
        Assert.Contains("20", output);
    }

    [Fact]
    public void RunTryCatch()
    {
        var (output, _) = Run("try { throw \"oops\" } catch(e) { print(e) }");
        Assert.Contains("oops", output);
    }

    [Fact]
    public void RunHigherOrder()
    {
        var (output, _) = Run("let nums = [1, 2, 3]\nlet doubled = map(nums, func(x) => x * 2)\nprint(doubled)");
        Assert.Contains("[2, 4, 6]", output);
    }

    [Fact]
    public void RunNestedScoping()
    {
        
        var (output, _) = Run("func inner() { let x = 99; print(x) }\nlet x = 1\ninner()\nprint(x)");
        var lines = output.Split('\n');
        Assert.Equal("99", lines[0]);
        Assert.Equal("1", lines[1]);
    }

    [Fact]
    public void RunClosures()
    {
        var (output, _) = Run("func make_adder(n) { return func(x) => x + n }\nlet add5 = make_adder(5)\nprint(add5(3))");
        Assert.Contains("8", output);
    }

    [Fact]
    public void RunRecursion()
    {
        var (output, _) = Run("func fib(n) { if n <= 1 { return n } return fib(n-1) + fib(n-2) }\nprint(fib(10))");
        Assert.Contains("55", output);
    }

    [Fact]
    public void RunJsonParseStringify()
    {
        var (output, _) = Run("let obj = json_parse(\"{\\\"a\\\": 1}\")\nprint(obj.a)\nprint(json_stringify(obj))");
        Assert.Contains("1", output);
    }

    [Fact]
    public void RunNullBehavior()
    {
        var (output, _) = Run("print(null)\nprint(typeof(null))");
        Assert.Contains("null", output);
    }
}
