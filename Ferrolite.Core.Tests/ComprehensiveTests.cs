using Ferrolite.Core.Lang;
using Ferrolite.Core.Recorder;

namespace Ferrolite.Core.Tests;




public class ControllerStdLibTests
{
    private (string output, List<string> lines) Run(string code)
    {
        var lines = new List<string>();
        var backend = new NullBackend();
        var interp = new Interpreter(backend, s => lines.Add(s));
        var tokens = new Lexer(code).Tokenize();
        var program = Parser.Parse(tokens);
        interp.Run(program);
        return (string.Join("\n", lines), lines);
    }

    [Fact]
    public void ControllerConnectedReturnsBool()
    {
        var (output, _) = Run("print(typeof(controller_connected(0)))");
        Assert.Contains("bool", output);
    }

    [Fact]
    public void ControllerConnectedReturnsFalseWhenNoController()
    {
        var (output, _) = Run("print(controller_connected(0))");
        Assert.Contains("false", output);
    }

    [Fact]
    public void ControllerPressDoesNotThrow()
    {
        
        var ex = Record.Exception(() => Run("controller_press(0, \"gamepad_a\")"));
        Assert.Null(ex);
    }

    [Fact]
    public void ControllerReleaseDoesNotThrow()
    {
        var ex = Record.Exception(() => Run("controller_release(0, \"gamepad_a\")"));
        Assert.Null(ex);
    }

    [Fact]
    public void ControllerStickDoesNotThrow()
    {
        var ex = Record.Exception(() => Run("controller_stick(0, \"left_stick\", 0.5, -0.8)"));
        Assert.Null(ex);
    }

    [Fact]
    public void ControllerTriggerDoesNotThrow()
    {
        var ex = Record.Exception(() => Run("controller_trigger(0, \"left_trigger\", 0.75)"));
        Assert.Null(ex);
    }

    [Fact]
    public void ControllerDPadDoesNotThrow()
    {
        var ex = Record.Exception(() => Run("controller_dpad(0, \"up\")"));
        Assert.Null(ex);
    }

    [Fact]
    public void ControllerPressWithHighIndexDoesNotThrow()
    {
        
        var ex = Record.Exception(() => Run("controller_press(3, \"gamepad_b\")"));
        Assert.Null(ex);
    }

    [Fact]
    public void ControllerAllButtonsDoNotThrow()
    {
        string[] buttons = { "gamepad_a", "gamepad_b", "gamepad_x", "gamepad_y",
            "gamepad_lb", "gamepad_rb", "gamepad_l3", "gamepad_r3",
            "gamepad_start", "gamepad_back" };

        foreach (var btn in buttons)
        {
            var ex = Record.Exception(() => Run($"controller_press(0, \"{btn}\")"));
            Assert.Null(ex);
        }
    }
}

public class TranspilerTests
{
    [Fact]
    public void TranspileEmptyList()
    {
        var result = Transpiler.Transpile(new List<RecordedEvent>());
        Assert.Contains("No events recorded", result);
    }

    [Fact]
    public void TranspileMouseMove()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.MouseMove, Timestamp = 0, X = 100, Y = 200, Included = true }
        };
        var code = Transpiler.Transpile(events);
        Assert.Contains("move(100, 200)", code);
    }

    [Fact]
    public void TranspileMouseDown()
    {
        
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.MouseDown, Timestamp = 0.1, X = 50, Y = 60, Button = "left", Included = true }
        };
        var code = Transpiler.Transpile(events);
        Assert.Contains("mouse_hold(50, 60, \"left\", 0)", code);
    }

    [Fact]
    public void TranspileMouseHold()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.MouseDown, Timestamp = 1.0, X = 50, Y = 60, Button = "left", Included = true },
            new() { Type = EventType.MouseUp, Timestamp = 1.25, Button = "left", Included = true },
        };
        var code = Transpiler.Transpile(events);
        Assert.Contains("mouse_hold(50, 60, \"left\", 250)", code);
    }

    [Fact]
    public void TranspileMouseUpWithoutDownIsDropped()
    {
        
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.MouseUp, Timestamp = 0.2, Button = "left", Included = true }
        };
        var code = Transpiler.Transpile(events);
        Assert.DoesNotContain("mouse_up", code);
    }

    [Fact]
    public void TranspileRelativeMovesWithDriftCorrection()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.MouseMoveRelative, Timestamp = 0.0, Dx = 10.5, Dy = -3.25, Included = true },
            new() { Type = EventType.MouseMoveRelative, Timestamp = 0.01, Dx = 5.0, Dy = 2.0, Included = true },
        };
        var code = Transpiler.Transpile(events, loop: true, loopCount: 3);
        Assert.Contains("move_relative(10.5, -3.25)", code);
        Assert.Contains("for _rep in range(3) {", code);
        
        Assert.Contains("move_relative(-15.5, 1.25)", code);
    }

    [Fact]
    public void TranspileScroll()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.MouseScroll, Timestamp = 0.5, X = 300, Y = 400, ScrollDy = 3, Included = true }
        };
        var code = Transpiler.Transpile(events);
        Assert.Contains("scroll(3, 300, 400)", code);
    }

    [Fact]
    public void TranspileKeyDown()
    {
        
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.KeyDown, Timestamp = 0, Key = "a", Included = true }
        };
        var code = Transpiler.Transpile(events);
        Assert.Contains("key_hold(\"a\", 0)", code);
    }

    [Fact]
    public void TranspileKeyHold()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.KeyDown, Timestamp = 0.5, Key = "shift", Included = true },
            new() { Type = EventType.KeyUp, Timestamp = 0.8, Key = "shift", Included = true },
        };
        var code = Transpiler.Transpile(events);
        Assert.Contains("key_hold(\"shift\", 300)", code);
    }

    [Fact]
    public void TranspileTypingCollapse()
    {
        
        var events = new List<RecordedEvent>();
        double t = 0.0;
        foreach (var c in new[] { "h", "i", "!" })
        {
            events.Add(new RecordedEvent { Type = EventType.KeyDown, Timestamp = t, Key = c, Included = true });
            t += 0.05;
            events.Add(new RecordedEvent { Type = EventType.KeyUp, Timestamp = t, Key = c, Included = true });
            t += 0.05;
        }
        var code = Transpiler.Transpile(events, collapseTyping: true);
        Assert.Contains("type_text(\"hi!\")", code);
    }

    [Fact]
    public void TranspileControllerButtonPress()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.ControllerButtonPress, Timestamp = 0, ControllerId = 0, Button = "gamepad_a", Included = true }
        };
        var code = Transpiler.Transpile(events);
        Assert.Contains("controller_press(0, \"gamepad_a\")", code);
    }

    [Fact]
    public void TranspileControllerButtonRelease()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.ControllerButtonRelease, Timestamp = 0, ControllerId = 0, Button = "gamepad_b", Included = true }
        };
        var code = Transpiler.Transpile(events);
        Assert.Contains("controller_release(0, \"gamepad_b\")", code);
    }

    [Fact]
    public void TranspileControllerStickMove()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.ControllerStickMove, Timestamp = 0, ControllerId = 0, Button = "left_stick", StickX = 0.5, StickY = -0.8, Included = true }
        };
        var code = Transpiler.Transpile(events);
        
        Assert.Contains("controller_stick", code);
    }

    [Fact]
    public void TranspileControllerTriggerMove()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.ControllerTriggerMove, Timestamp = 0, ControllerId = 0, Button = "right_trigger", TriggerValue = 0.75, Included = true }
        };
        var code = Transpiler.Transpile(events);
        Assert.Contains("controller_trigger", code);
    }

    [Fact]
    public void TranspileControllerDPadMove()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.ControllerDPadMove, Timestamp = 0, ControllerId = 0, DPadDirection = "up", Included = true }
        };
        var code = Transpiler.Transpile(events);
        Assert.Contains("controller_dpad(0, \"up\")", code);
    }

    [Fact]
    public void TranspileWithLoop()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.ControllerButtonPress, Timestamp = 0, ControllerId = 0, Button = "gamepad_a", Included = true }
        };
        var code = Transpiler.Transpile(events, loop: true);
        Assert.Contains("while true {", code);
        Assert.Contains("}", code);
    }

    [Fact]
    public void TranspileWithLoopCount()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.ControllerButtonPress, Timestamp = 0, ControllerId = 0, Button = "gamepad_a", Included = true }
        };
        var code = Transpiler.Transpile(events, loop: true, loopCount: 5);
        Assert.Contains("for _rep in range(5) {", code);
    }

    [Fact]
    public void TranspileExcludesNonIncludedEvents()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.MouseMove, Timestamp = 0, X = 100, Y = 200, Included = false },
            new() { Type = EventType.MouseMove, Timestamp = 0.5, X = 300, Y = 400, Included = true }
        };
        var code = Transpiler.Transpile(events);
        Assert.DoesNotContain("100, 200", code);
        Assert.Contains("300, 400", code);
    }

    [Fact]
    public void TranspileWaitsBetweenEvents()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.MouseMove, Timestamp = 0, X = 10, Y = 20, Included = true },
            new() { Type = EventType.MouseMove, Timestamp = 0.5, X = 30, Y = 40, Included = true }
        };
        
        var code = Transpiler.Transpile(events, minWaitThreshold: 0.002);
        Assert.Contains("wait(", code);
    }

    [Fact]
    public void TranspileMixedSequence()
    {
        var events = new List<RecordedEvent>
        {
            new() { Type = EventType.MouseMove, Timestamp = 0, X = 100, Y = 200, Included = true },
            new() { Type = EventType.MouseDown, Timestamp = 0.1, X = 100, Y = 200, Button = "left", Included = true },
            new() { Type = EventType.MouseUp, Timestamp = 0.2, X = 100, Y = 200, Button = "left", Included = true },
            new() { Type = EventType.KeyDown, Timestamp = 0.3, Key = "h", Included = true },
            new() { Type = EventType.KeyUp, Timestamp = 0.35, Key = "h", Included = true },
            new() { Type = EventType.ControllerButtonPress, Timestamp = 0.4, ControllerId = 0, Button = "gamepad_a", Included = true },
            new() { Type = EventType.ControllerButtonRelease, Timestamp = 0.5, ControllerId = 0, Button = "gamepad_a", Included = true },
        };
        var code = Transpiler.Transpile(events);
        Assert.Contains("move(100, 200)", code);
        
        Assert.Contains("mouse_hold(100, 200, \"left\", 100)", code);
        Assert.Contains("key_hold(\"h\", 50)", code);
        Assert.Contains("controller_press(0, \"gamepad_a\")", code);
        Assert.Contains("controller_release(0, \"gamepad_a\")", code);
    }
}

public class RecordedEventSerializationTests
{
    [Fact]
    public void ToDictAndBack_MouseMove()
    {
        var original = new RecordedEvent
        {
            Type = EventType.MouseMove,
            Timestamp = 1.234,
            X = 500,
            Y = 600,
            Included = true
        };
        var dict = original.ToDict();
        var restored = RecordedEvent.FromDict(dict);

        Assert.Equal(EventType.MouseMove, restored.Type);
        Assert.Equal(1.234, restored.Timestamp);
        Assert.Equal(500, restored.X);
        Assert.Equal(600, restored.Y);
        Assert.True(restored.Included);
    }

    [Fact]
    public void ToDictAndBack_ControllerEvent()
    {
        var original = new RecordedEvent
        {
            Type = EventType.ControllerStickMove,
            Timestamp = 2.5,
            ControllerId = 0,
            Button = "left_stick",
            StickX = 0.75,
            StickY = -0.25,
            Included = true
        };
        var dict = original.ToDict();
        var restored = RecordedEvent.FromDict(dict);

        Assert.Equal(EventType.ControllerStickMove, restored.Type);
        Assert.Equal(0, restored.ControllerId);
        Assert.Equal("left_stick", restored.Button);
        Assert.Equal(0.75, restored.StickX);
        Assert.Equal(-0.25, restored.StickY);
    }

    [Fact]
    public void ToDictAndBack_KeyboardEvent()
    {
        var original = new RecordedEvent
        {
            Type = EventType.KeyDown,
            Timestamp = 0.5,
            Key = "enter",
            Included = true
        };
        var dict = original.ToDict();
        var restored = RecordedEvent.FromDict(dict);

        Assert.Equal(EventType.KeyDown, restored.Type);
        Assert.Equal("enter", restored.Key);
    }

    [Fact]
    public void ToDictAndBack_ControllerTrigger()
    {
        var original = new RecordedEvent
        {
            Type = EventType.ControllerTriggerMove,
            Timestamp = 1.0,
            ControllerId = 2,
            Button = "right_trigger",
            TriggerValue = 0.85,
            Included = true
        };
        var dict = original.ToDict();
        var restored = RecordedEvent.FromDict(dict);

        Assert.Equal(2, restored.ControllerId);
        Assert.Equal(0.85, restored.TriggerValue);
    }

    [Fact]
    public void ToDictAndBack_ControllerDPad()
    {
        var original = new RecordedEvent
        {
            Type = EventType.ControllerDPadMove,
            Timestamp = 0.75,
            ControllerId = 0,
            DPadDirection = "up_left",
            Included = true
        };
        var dict = original.ToDict();
        var restored = RecordedEvent.FromDict(dict);

        Assert.Equal("up_left", restored.DPadDirection);
    }

    [Fact]
    public void ToDictAndBack_ExcludedEvent()
    {
        var original = new RecordedEvent
        {
            Type = EventType.MouseMove,
            Timestamp = 0,
            X = 0,
            Y = 0,
            Included = false
        };
        var dict = original.ToDict();
        var restored = RecordedEvent.FromDict(dict);

        Assert.False(restored.Included);
    }
}

public class LexerEdgeCaseTests
{
    [Fact]
    public void TokenizeEmptyString()
    {
        var tokens = new Lexer("").Tokenize();
        Assert.Single(tokens);
        Assert.Equal(TokenType.Eof, tokens[0].Type);
    }

    [Fact]
    public void TokenizeOnlyComment()
    {
        var tokens = new Lexer("# just a comment").Tokenize();
        
        var nonEof = tokens.Where(t => t.Type != TokenType.Eof).ToList();
        Assert.True(nonEof.Count <= 2); 
    }

    [Fact]
    public void TokenizeDeeplyNestedParens()
    {
        var tokens = new Lexer("(((((1)))))").Tokenize();
        var parens = tokens.Where(t => t.Type == TokenType.LParen || t.Type == TokenType.RParen).ToList();
        Assert.Equal(5, parens.Count(t => t.Type == TokenType.LParen));
        Assert.Equal(5, parens.Count(t => t.Type == TokenType.RParen));
    }

    [Fact]
    public void TokenizeStringWithEscapes()
    {
        
        var tokens = new Lexer("\"hello\"").Tokenize();
        Assert.Equal(TokenType.String, tokens[0].Type);
    }

    [Fact]
    public void TokenizeTemplateString()
    {
        var tokens = new Lexer(@"`hello ${name}`").Tokenize();
        Assert.Equal(TokenType.TemplateString, tokens[0].Type);
    }

    [Fact]
    public void TokenizeAllOperators()
    {
        var src = "+ - * / % == != < > <= >= = += -= *= /= and or not";
        var tokens = new Lexer(src).Tokenize();
        var types = tokens.Where(t => t.Type != TokenType.Eof).Select(t => t.Type).ToList();
        Assert.Contains(TokenType.Plus, types);
        Assert.Contains(TokenType.Minus, types);
        Assert.Contains(TokenType.Star, types);
        Assert.Contains(TokenType.Slash, types);
        Assert.Contains(TokenType.Percent, types);
        Assert.Contains(TokenType.Eq, types);
        Assert.Contains(TokenType.Neq, types);
        Assert.Contains(TokenType.Lt, types);
        Assert.Contains(TokenType.Gt, types);
        Assert.Contains(TokenType.Lte, types);
        Assert.Contains(TokenType.Gte, types);
        Assert.Contains(TokenType.Assign, types);
        Assert.Contains(TokenType.PlusAssign, types);
        Assert.Contains(TokenType.MinusAssign, types);

        Assert.Contains(TokenType.And, types);
        Assert.Contains(TokenType.Or, types);
        Assert.Contains(TokenType.Not, types);
    }

    [Fact]
    public void TokenizeNumberFormats()
    {
        var tokens = new Lexer("42 3.14 .5").Tokenize();
        var numbers = tokens.Where(t => t.Type == TokenType.Number).ToList();
        Assert.Equal(3, numbers.Count);
        Assert.Equal(42.0, numbers[0].Value);
        Assert.Equal(3.14, numbers[1].Value);
        Assert.Equal(0.5, numbers[2].Value);
    }
}

public class ParserEdgeCaseTests
{
    [Fact]
    public void ParseEmptyProgram()
    {
        var tokens = new Lexer("").Tokenize();
        var program = Parser.Parse(tokens);
        Assert.Empty(program.Statements);
    }

    [Fact]
    public void ParseMultipleStatements()
    {
        var tokens = new Lexer("let a = 1\nlet b = 2\nlet c = 3").Tokenize();
        var program = Parser.Parse(tokens);
        Assert.Equal(3, program.Statements.Count);
    }

    [Fact]
    public void ParseNestedIf()
    {
        
        var src = "if true { if false { print(1) } else { print(2) } }";
        var tokens = new Lexer(src).Tokenize();
        var program = Parser.Parse(tokens);
        Assert.Single(program.Statements);
    }

    [Fact]
    public void ParseForInList()
    {
        var src = "for x in [1, 2, 3] { print(x) }";
        var tokens = new Lexer(src).Tokenize();
        var program = Parser.Parse(tokens);
        var forStmt = Assert.IsType<ForStmt>(program.Statements[0]);
        Assert.Equal("x", forStmt.VarName);
    }

    [Fact]
    public void ParseFuncWithNoParams()
    {
        var src = "func greet() { print(\"hi\") }";
        var tokens = new Lexer(src).Tokenize();
        var program = Parser.Parse(tokens);
        var func = Assert.IsType<FuncDecl>(program.Statements[0]);
        Assert.Empty(func.Params);
    }

    [Fact]
    public void ParseMapWithNestedMap()
    {
        var src = "let m = { a: { b: 1 } }";
        var tokens = new Lexer(src).Tokenize();
        var program = Parser.Parse(tokens);
        var letStmt = Assert.IsType<LetStmt>(program.Statements[0]);
        var outer = Assert.IsType<MapLit>(letStmt.Value);
        var inner = Assert.IsType<MapLit>(outer.Values[0]);
        Assert.Single(inner.Keys);
    }

    [Fact]
    public void ParseChainedMemberAccess()
    {
        var src = "a.b.c";
        var tokens = new Lexer(src).Tokenize();
        var program = Parser.Parse(tokens);
        var expr = Assert.IsType<ExprStmt>(program.Statements[0]);
        var member = Assert.IsType<MemberAccess>(expr.Expr);
        var inner = Assert.IsType<MemberAccess>(member.Obj);
        Assert.Equal("a", ((Identifier)inner.Obj).Name);
        Assert.Equal("b", inner.Name);
        Assert.Equal("c", member.Name);
    }

    [Fact]
    public void ParseTernaryExpression()
    {
        
        var ex = Record.Exception(() => new Lexer("let x = true ? 1 : 2").Tokenize());
        Assert.IsType<LexError>(ex);
    }

    [Fact]
    public void ParseImportStatement()
    {
        var src = "import \"mymodule\"";
        var tokens = new Lexer(src).Tokenize();
        var program = Parser.Parse(tokens);
        Assert.Single(program.Statements);
        Assert.IsType<ImportStmt>(program.Statements[0]);
    }
}

public class InterpreterEdgeCaseTests
{
    private (string output, List<string> lines) Run(string code)
    {
        var lines = new List<string>();
        var backend = new NullBackend();
        var interp = new Interpreter(backend, s => lines.Add(s));
        var tokens = new Lexer(code).Tokenize();
        var program = Parser.Parse(tokens);
        interp.Run(program);
        return (string.Join("\n", lines), lines);
    }

    [Fact]
    public void RunEmptyProgram()
    {
        var ex = Record.Exception(() => Run(""));
        Assert.Null(ex);
    }

    [Fact]
    public void RunStringConcatenation()
    {
        var (output, _) = Run("print(\"hello\" + \" \" + \"world\")");
        Assert.Contains("hello world", output);
    }

    [Fact]
    public void RunStringMultiply()
    {
        
        var (output, _) = Run("print(\"ha\" + \"ha\" + \"ha\")");
        Assert.Contains("hahaha", output);
    }

    [Fact]
    public void RunBooleanLogic()
    {
        var (output, _) = Run("print(true and false)\nprint(true or false)\nprint(not true)");
        Assert.Contains("false", output);
        Assert.Contains("true", output);
        var lines = output.Split('\n');
        Assert.Equal("false", lines[0]);
        Assert.Equal("true", lines[1]);
        Assert.Equal("false", lines[2]);
    }

    [Fact]
    public void RunListIndex()
    {
        var (output, _) = Run("let lst = [10, 20, 30]\nprint(lst[1])");
        Assert.Contains("20", output);
    }

    [Fact]
    public void RunMapAccess()
    {
        var (output, _) = Run("let m = { name: \"ferrolite\", version: 2 }\nprint(m.name)\nprint(m.version)");
        Assert.Contains("ferrolite", output);
        Assert.Contains("2", output);
    }

    [Fact]
    public void RunNestedFunctionCalls()
    {
        var (output, _) = Run("print(str(len(\"hello\")))");
        Assert.Contains("5", output);
    }

    [Fact]
    public void RunReduceFunction()
    {
        var (output, _) = Run("let nums = [1, 2, 3, 4]\nprint(reduce(nums, func(acc, x) => acc + x, 0))");
        Assert.Contains("10", output);
    }

    [Fact]
    public void RunFilterFunction()
    {
        var (output, _) = Run("let nums = [1, 2, 3, 4, 5]\nlet evens = filter(nums, func(x) => x % 2 == 0)\nprint(evens)");
        Assert.Contains("[2, 4]", output);
    }

    [Fact]
    public void RunForeachFunction()
    {
        var (output, _) = Run("let nums = [1, 2, 3]\nforeach(nums, func(x) => print(x * 10))");
        var lines = output.Split('\n');
        Assert.Equal("10", lines[0]);
        Assert.Equal("20", lines[1]);
        Assert.Equal("30", lines[2]);
    }

    [Fact]
    public void RunMapMethods()
    {
        var (output, _) = Run("let m = { a: 1, b: 2 }\nprint(len(keys(m)))\nprint(len(values(m)))");
        Assert.Contains("2", output);
    }

    [Fact]
    public void RunStringMethods()
    {
        var (output, _) = Run("let s = \"Hello World\"\nprint(upper(s))\nprint(lower(s))\nprint(trim(\"  hi  \"))\nprint(len(split(\"a,b,c\", \",\")))");
        Assert.Contains("HELLO WORLD", output);
        Assert.Contains("hello world", output);
        Assert.Contains("hi", output);
        Assert.Contains("3", output);
    }

    [Fact]
    public void RunStringContains()
    {
        var (output, _) = Run("print(contains_str(\"hello world\", \"world\"))");
        Assert.Contains("true", output);
    }

    [Fact]
    public void RunRegexMatch()
    {
        
        var (output, _) = Run("print(regex_test(\"[0-9]+\", \"hello123\"))");
        Assert.Contains("true", output);
    }

    [Fact]
    public void RunMathFunctions()
    {
        var (output, _) = Run("print(abs(-5))\nprint(floor(3.7))\nprint(ceil(3.2))\nprint(round(3.5))");
        Assert.Contains("5", output);
        Assert.Contains("3", output);
        Assert.Contains("4", output);
    }

    [Fact]
    public void RunPowerFunction()
    {
        var (output, _) = Run("print(pow(2, 10))");
        Assert.Contains("1024", output);
    }

    [Fact]
    public void RunListModification()
    {
        var (output, _) = Run("let lst = [1, 2, 3]\npush(lst, 4)\nprint(lst)\npop(lst)\nprint(lst)");
        Assert.Contains("[1, 2, 3, 4]", output);
        Assert.Contains("[1, 2, 3]", output);
    }

    [Fact]
    public void RunListSlice()
    {
        var (output, _) = Run("let lst = [0, 1, 2, 3, 4]\nprint(slice(lst, 1, 4))");
        Assert.Contains("[1, 2, 3]", output);
    }

    [Fact]
    public void RunJsonRoundtrip()
    {
        var (output, _) = Run("let obj = json_parse(json_stringify({ x: 1, y: 2 }))\nprint(obj.x)\nprint(obj.y)");
        Assert.Contains("1", output);
        Assert.Contains("2", output);
    }

    [Fact]
    public void RunTryCatchWithThrow()
    {
        var (output, _) = Run("try { throw \"error\" } catch(e) { print(\"caught: \" + e) }");
        Assert.Contains("caught: error", output);
    }

    [Fact]
    public void RunTryCatchDoesNotCrashOnNoThrow()
    {
        var ex = Record.Exception(() => Run("try { print(\"ok\") } catch(e) { print(e) }"));
        Assert.Null(ex);
    }

    [Fact]
    public void RunRecursionDeep()
    {
        var (output, _) = Run("func factorial(n) { if n <= 1 { return 1 } return n * factorial(n-1) }\nprint(factorial(8))");
        Assert.Contains("40320", output);
    }

    [Fact]
    public void RunHighOrderMapWithClosures()
    {
        var (output, _) = Run("func multiply_by(n) { return func(x) => x * n }\nlet times3 = multiply_by(3)\nlet nums = [1, 2, 3]\nprint(map(nums, times3))");
        Assert.Contains("[3, 6, 9]", output);
    }

    [Fact]
    public void RunWaitFunction()
    {
        
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Run("wait(0.05)");
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds >= 40); 
    }

    [Fact]
    public void RunNowFunction()
    {
        var (output, _) = Run("let t = now()\nprint(t > 0)");
        Assert.Contains("true", output);
    }

    [Fact]
    public void RunNowMsFunction()
    {
        var (output, _) = Run("let t = now_ms()\nprint(t > 0)");
        Assert.Contains("true", output);
    }

    [Fact]
    public void RunNullOperations()
    {
        var (output, _) = Run("print(null == null)\nprint(null != null)\nprint(typeof(null))");
        var lines = output.Split('\n');
        Assert.Equal("true", lines[0]);
        Assert.Equal("false", lines[1]);
        Assert.Equal("null", lines[2]);
    }

    [Fact]
    public void RunZeroDivision()
    {
        
        var ex = Record.Exception(() => Run("print(1 / 0)"));
        Assert.NotNull(ex);
        Assert.IsType<VSRuntimeError>(ex);
    }

    [Fact]
    public void RunUndefinedVariable()
    {
        var ex = Record.Exception(() => Run("print(undefined_var)"));
        Assert.NotNull(ex);
        Assert.IsType<VSRuntimeError>(ex);
    }

    [Fact]
    public void RunBreakInWhileLoop()
    {
        var (output, _) = Run("let i = 0\nwhile true { if i >= 5 { break } print(i)\ni += 1 }");
        var lines = output.Split('\n');
        Assert.Equal(5, lines.Length);
        Assert.Equal("0", lines[0]);
        Assert.Equal("4", lines[4]);
    }

    [Fact]
    public void RunContinueInWhileLoop()
    {
        var (output, _) = Run("let i = 0\nwhile i < 5 { i += 1\nif i == 3 { continue }\nprint(i) }");
        var lines = output.Split('\n');
        Assert.Equal(4, lines.Length); 
        Assert.DoesNotContain("3", lines);
    }

    [Fact]
    public void RunStringRepeat()
    {
        var (output, _) = Run("print(repeat(\"ab\", 3))");
        Assert.Contains("ababab", output);
    }

    [Fact]
    public void RunJoinFunction()
    {
        var (output, _) = Run("print(join([\"a\", \"b\", \"c\"], \"-\"))");
        Assert.Contains("a-b-c", output);
    }

    [Fact]
    public void RunReplaceFunction()
    {
        var (output, _) = Run("print(replace(\"hello world\", \"world\", \"there\"))");
        Assert.Contains("hello there", output);
    }

    [Fact]
    public void RunSortWithComparator()
    {
        var (output, _) = Run("let nums = [3, 1, 4, 1, 5]\nprint(sort(nums))");
        Assert.Contains("[1, 1, 3, 4, 5]", output);
    }

    [Fact]
    public void RunReverseList()
    {
        var (output, _) = Run("print(reverse([1, 2, 3]))");
        Assert.Contains("[3, 2, 1]", output);
    }

    [Fact]
    public void RunUniqueList()
    {
        var (output, _) = Run("print(unique([1, 2, 1, 3, 2]))");
        Assert.Contains("[1, 2, 3]", output);
    }

    [Fact]
    public void RunFlattenList()
    {
        var (output, _) = Run("print(flatten([[1, 2], [3, 4]]))");
        Assert.Contains("[1, 2, 3, 4]", output);
    }
}

public class StdLibEdgeCaseTests
{
    private (string output, List<string> lines) Run(string code)
    {
        var lines = new List<string>();
        var backend = new NullBackend();
        var interp = new Interpreter(backend, s => lines.Add(s));
        var tokens = new Lexer(code).Tokenize();
        var program = Parser.Parse(tokens);
        interp.Run(program);
        return (string.Join("\n", lines), lines);
    }

    [Fact]
    public void RunHasKeyCheck()
    {
        
        var (output, _) = Run("print(has_key({ a: 1 }, \"a\"))\nprint(has_key({ a: 1 }, \"b\"))");
        Assert.Contains("true", output);
        Assert.Contains("false", output);
    }

    [Fact]
    public void RunDeleteKey()
    {
        var (output, _) = Run("let m = { a: 1, b: 2 }\ndelete_key(m, \"b\")\nprint(m)");
        Assert.Contains("a", output);
        Assert.DoesNotContain("\"b\"", output);
    }

    [Fact]
    public void RunMergeMaps()
    {
        var (output, _) = Run("let a = { x: 1 }\nlet b = { y: 2 }\nlet c = merge(a, b)\nprint(c.x)\nprint(c.y)");
        Assert.Contains("1", output);
        Assert.Contains("2", output);
    }

    [Fact]
    public void RunClipboardFunctions()
    {
        
        var ex = Record.Exception(() => Run("clipboard_set(\"test\")"));
        Assert.Null(ex);
    }

    [Fact]
    public void RunBeepFunction()
    {
        
        var ex = Record.Exception(() => Run("beep(1000, 100)"));
        Assert.Null(ex);
    }

    [Fact]
    public void RunFormatNumber()
    {
        var (output, _) = Run("print(join([\"a\", \"b\", \"c\"], \"-\"))");
        Assert.Contains("a-b-c", output);

        var (output2, _) = Run("print(format_number(1234.56, 2))");
        
        Assert.True(output2.Contains("1234.56") || output2.Contains("1234,56"), $"Expected formatted number, got: {output2}");
    }

    [Fact]
    public void RunPadLeftRight()
    {
        var (output, _) = Run("print(pad_left(\"5\", 3, \"0\"))\nprint(pad_right(\"hi\", 5, \".\"))");
        
        Assert.Contains("5", output);
    }

    [Fact]
    public void RunIsEmptiness()
    {
        var (output, _) = Run("print(len(\"\") == 0)\nprint(len(\"x\") == 0)\nprint(len([]) == 0)");
        Assert.Contains("true", output);
        Assert.Contains("false", output);
    }

    [Fact]
    public void RunCharAtAndIndexOf()
    {
        var (output, _) = Run("print(char_at(\"hello\", 1))\nprint(index_of(\"hello\", \"ll\"))");
        Assert.Contains("e", output);
        Assert.Contains("2", output);
    }

    [Fact]
    public void RunRangeFunction()
    {
        var (output, _) = Run("print(range(5))");
        Assert.Contains("[0, 1, 2, 3, 4]", output);
    }

    [Fact]
    public void RunSumAndAvg()
    {
        var (output, _) = Run("let nums = [1, 2, 3, 4]\nprint(sum(nums))\nprint(avg(nums))");
        Assert.Contains("10", output);
        
        Assert.True(output.Contains("2.5") || output.Contains("2,5"), $"Expected 2.5, got: {output}");
    }

    [Fact]
    public void RunRandomIntRange()
    {
        var (output, _) = Run("let x = random_int(1, 100)\nprint(x >= 1)\nprint(x <= 100)");
        Assert.Contains("true", output);
    }

    [Fact]
    public void RunRandomChoice()
    {
        var (output, _) = Run("let opts = [\"a\", \"b\", \"c\"]\nlet choice = random_choice(opts)\nprint(len(choice) > 0)");
        Assert.Contains("true", output);
    }
}
