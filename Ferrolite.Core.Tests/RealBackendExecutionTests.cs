using Ferrolite.Core.Lang;

namespace Ferrolite.Core.Tests;










public class RealBackendExecutionTests
{
    private static Program CompileProgram(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        return new Parser(tokens).ParseProgram();
    }

    [Fact]
    public void WindowsBackend_RunsPrintScript_WithOutput()
    {
        var lines = new List<string>();
        var backend = new WindowsBackend();
        var interp = new Interpreter(backend, s => { lock (lines) lines.Add(s); }, 1.0);
        var program = CompileProgram(
            "print(\"hello from real backend\")\n" +
            "let x = 6 * 7\n" +
            "print(x)\n" +
            "let name = \"ferrolite\"\n" +
            "print(upper(name))\n" +
            "wait(0.05)\n" +
            "print(\"done\")");

        interp.Run(program);

        lock (lines)
        {
            Assert.Contains("hello from real backend", lines);
            Assert.Contains("42", lines);
            Assert.Contains("FERROLITE", lines);
            Assert.Contains("done", lines);
        }
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public void WindowsBackend_ReadsRealHardwareState()
    {
        
        
        var backend = new WindowsBackend();

        var pos = backend.MousePosition();
        var (sw, sh) = backend.ScreenSize();
        Assert.True(sw > 0 && sh > 0, "ScreenSize returned a non-positive size");
        Assert.InRange(pos.X, 0, sw);
        Assert.InRange(pos.Y, 0, sh);

        
        
        _ = backend.IsKeyDown("caps");
        _ = backend.IsKeyDown("shift");
        _ = backend.IsKeyToggled("caps");
        _ = backend.IsMouseDown("left");

        
        
        backend.MouseMoveRelative(0, 0, 0.01, 1);

        var pad = new WindowsControllerBackend();
        _ = pad.IsGamepadConnected(0);
    }

    [Fact]
    public void WindowsBackend_ControlFlow_ExecutesFully()
    {
        var lines = new List<string>();
        var backend = new WindowsBackend();
        var interp = new Interpreter(backend, s => { lock (lines) lines.Add(s); }, 1.0);
        var program = CompileProgram(
            "let total = 0\n" +
            "for i in range(1, 6) {\n" +
            "    total = total + i\n" +
            "}\n" +
            "print(total)\n" +
            "if total == 15 {\n" +
            "    print(\"sum ok\")\n" +
            "}\n" +
            "func double(n) { return n * 2 }\n" +
            "print(double(21))");

        interp.Run(program);

        lock (lines)
        {
            Assert.Contains("15", lines);
            Assert.Contains("sum ok", lines);
            Assert.Contains("42", lines);
        }
    }
}
