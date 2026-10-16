using System.Text;

namespace Ferrolite.Core.Lang;

public class Lexer
{
    private readonly string _src;
    private int _pos;
    private int _line = 1;
    private int _col = 1;
    private readonly List<Token> _tokens = new();

    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        ["let"] = TokenType.Let,
        ["if"] = TokenType.If,
        ["else"] = TokenType.Else,
        ["while"] = TokenType.While,
        ["for"] = TokenType.For,
        ["in"] = TokenType.In,
        ["func"] = TokenType.Func,
        ["return"] = TokenType.Return,
        ["break"] = TokenType.Break,
        ["continue"] = TokenType.Continue,
        ["try"] = TokenType.Try,
        ["catch"] = TokenType.Catch,
        ["throw"] = TokenType.Throw,
        ["true"] = TokenType.True,
        ["false"] = TokenType.False,
        ["null"] = TokenType.Null,
        ["import"] = TokenType.Import,
        ["and"] = TokenType.And,
        ["or"] = TokenType.Or,
        ["not"] = TokenType.Not,
    };

    public Lexer(string source)
    {
        _src = source;
    }

    private char Peek(int offset = 0)
    {
        int idx = _pos + offset;
        return idx >= _src.Length ? '\0' : _src[idx];
    }

    private char Advance()
    {
        char ch = _src[_pos++];
        if (ch == '\n') { _line++; _col = 1; }
        else _col++;
        return ch;
    }

    private void Add(TokenType type, object? value = null, int? line = null, int? col = null)
    {
        _tokens.Add(new Token(type, value, line ?? _line, col ?? _col));
    }

    public List<Token> Tokenize()
    {
        while (_pos < _src.Length)
        {
            char ch = Peek();

            
            if (ch is ' ' or '\t' or '\r') { Advance(); continue; }

            
            if (ch == '#')
            {
                while (Peek() != '\n' && Peek() != '\0') Advance();
                continue;
            }

            
            if (ch == '/' && Peek(1) == '/')
            {
                while (Peek() != '\n' && Peek() != '\0') Advance();
                continue;
            }

            
            if (ch == '/' && Peek(1) == '*')
            {
                Advance(); Advance();
                while (!(Peek() == '*' && Peek(1) == '/') && Peek() != '\0') Advance();
                if (Peek() == '*') Advance();
                if (Peek() == '/') Advance();
                continue;
            }

            
            if (ch == '\n')
            {
                int sl = _line, sc = _col;
                Advance();
                Add(TokenType.Newline, "\\n", sl, sc);
                continue;
            }

            
            if (char.IsDigit(ch) || (ch == '.' && char.IsDigit(Peek(1))))
            {
                ReadNumber();
                continue;
            }

            
            if (ch is '"' or '\'')
            {
                ReadString(ch);
                continue;
            }

            
            if (ch == '`')
            {
                ReadTemplateString();
                continue;
            }

            
            if (char.IsLetter(ch) || ch == '_')
            {
                ReadIdent();
                continue;
            }

            
            ReadSymbol();
        }

        Add(TokenType.Eof);
        return _tokens;
    }

    private void ReadNumber()
    {
        int sl = _line, sc = _col;
        int start = _pos;
        bool seenDot = false;
        while (char.IsDigit(Peek()) || (Peek() == '.' && !seenDot))
        {
            if (Peek() == '.') seenDot = true;
            Advance();
        }
        string text = _src[start.._pos];
        Add(TokenType.Number, seenDot ? double.Parse(text, System.Globalization.CultureInfo.InvariantCulture) : (double)int.Parse(text), sl, sc);
    }

    private void ReadString(char quote)
    {
        int sl = _line, sc = _col;
        Advance(); 
        var sb = new StringBuilder();
        while (Peek() != quote)
        {
            if (Peek() == '\0') throw new LexError("Unterminated string literal", sl, sc);
            if (Peek() == '\\')
            {
                Advance();
                char esc = Advance();
                sb.Append(esc switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '\\' => '\\',
                    '"' => '"',
                    '\'' => '\'',
                    '0' => '\0',
                    _ => esc,
                });
            }
            else
            {
                sb.Append(Advance());
            }
        }
        Advance(); 
        Add(TokenType.String, sb.ToString(), sl, sc);
    }

    private void ReadTemplateString()
    {
        int sl = _line, sc = _col;
        Advance(); 
        var sb = new StringBuilder();
        while (Peek() != '`')
        {
            if (Peek() == '\0') throw new LexError("Unterminated template string", sl, sc);
            if (Peek() == '\\')
            {
                Advance();
                char esc = Advance();
                sb.Append(esc switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '\\' => '\\',
                    '`' => '`',
                    '$' => '$',
                    _ => esc,
                });
            }
            else if (Peek() == '$' && Peek(1) == '{')
            {
                sb.Append(Advance()); 
                sb.Append(Advance()); 
            }
            else
            {
                sb.Append(Advance());
            }
        }
        Advance(); 
        Add(TokenType.TemplateString, sb.ToString(), sl, sc);
    }

    private void ReadIdent()
    {
        int sl = _line, sc = _col;
        int start = _pos;
        while (char.IsLetterOrDigit(Peek()) || Peek() == '_') Advance();
        string text = _src[start.._pos];

        if (Keywords.TryGetValue(text, out TokenType ktype))
        {
            if (ktype == TokenType.True)
                Add(TokenType.Boolean, true, sl, sc);
            else if (ktype == TokenType.False)
                Add(TokenType.Boolean, false, sl, sc);
            else
                Add(ktype, text, sl, sc);
        }
        else
        {
            Add(TokenType.Ident, text, sl, sc);
        }
    }

    private void ReadSymbol()
    {
        int sl = _line, sc = _col;
        char ch = Advance();
        string two = ch.ToString() + Peek();

        var twoChar = new Dictionary<string, TokenType>
        {
            ["=="] = TokenType.Eq,
            ["!="] = TokenType.Neq,
            ["<="] = TokenType.Lte,
            [">="] = TokenType.Gte,
            ["&&"] = TokenType.And,
            ["||"] = TokenType.Or,
            ["+="] = TokenType.PlusAssign,
            ["-="] = TokenType.MinusAssign,
            ["=>"] = TokenType.Arrow,
        };

        if (twoChar.TryGetValue(two, out TokenType tt))
        {
            Advance();
            Add(tt, two, sl, sc);
            return;
        }

        var single = new Dictionary<char, TokenType>
        {
            ['+'] = TokenType.Plus,
            ['-'] = TokenType.Minus,
            ['*'] = TokenType.Star,
            ['/'] = TokenType.Slash,
            ['%'] = TokenType.Percent,
            ['='] = TokenType.Assign,
            ['<'] = TokenType.Lt,
            ['>'] = TokenType.Gt,
            ['!'] = TokenType.Not,
            ['('] = TokenType.LParen,
            [')'] = TokenType.RParen,
            ['{'] = TokenType.LBrace,
            ['}'] = TokenType.RBrace,
            ['['] = TokenType.LBracket,
            [']'] = TokenType.RBracket,
            [','] = TokenType.Comma,
            ['.'] = TokenType.Dot,
            [':'] = TokenType.Colon,
            [';'] = TokenType.Semi,
        };

        if (single.TryGetValue(ch, out TokenType sType))
        {
            Add(sType, ch.ToString(), sl, sc);
            return;
        }

        throw new LexError($"Unexpected character '{ch}'", sl, sc);
    }
}
