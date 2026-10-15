using Ferrolite.Core.Lang;

namespace Ferrolite.Core.Tests;

public class MapAndDebugRegressionTests
{
    private static (string Output, bool Ok) Run(string source)
    {
        var lines = new List<string>();
        var interp = new Interpreter(new NullBackend(_ => { }), s => { lock (lines) lines.Add(s); }, 1.0);
        try
        {
            var tokens = new Lexer(source).Tokenize();
            var program = new Parser(tokens).ParseProgram();
            interp.Run(program);
            string joined;
            lock (lines) joined = string.Join("\n", lines);
            return (joined, true);
        }
        catch (Exception e)
        {
            return ($"ERROR: {e.Message}", false);
        }
    }

    [Fact]
    public void BuiltinMaps_CanBeIndexedAndExplored()
    {
        
        
        var (output, ok) = Run(
            "let p = get_mouse_pos()\n" +
            "print(p[\"x\"])\n" +
            "print(p[\"y\"])\n" +
            "print(len(p))\n" +
            "print(join(keys(p), \",\"))\n" +
            "let s = screen_size()\n" +
            "print(s[\"width\"])\n" +
            "print(s[\"height\"])\n" +
            "print(typeof(mouse_move_relative))");
        Assert.True(ok, output);
        string[] lines = output.Split('\n');
        Assert.Equal("0", lines[0]);
        Assert.Equal("0", lines[1]);
        Assert.Equal("2", lines[2]);
        Assert.Equal("x,y", lines[3]);
        Assert.Equal("1920", lines[4]);
        Assert.Equal("1080", lines[5]);
        Assert.Equal("function", lines[6]);
    }

    [Fact]
    public void WandererStyleScript_RunsWithoutError()
    {
        
        var (output, ok) = Run(
            "for i in range(0, 20) {\n" +
            "    let dx = random_int(-60, 60)\n" +
            "    let dy = random_int(-60, 60)\n" +
            "    let pos = get_mouse_pos()\n" +
            "    let scr = screen_size()\n" +
            "    if pos[\"x\"] < 100 or pos[\"x\"] > scr[\"width\"] - 100 {\n" +
            "        dx = 0 - dx\n" +
            "    }\n" +
            "    if pos[\"y\"] < 100 or pos[\"y\"] > scr[\"height\"] - 100 {\n" +
            "        dy = 0 - dy\n" +
            "    }\n" +
            "    move_relative(dx, dy)\n" +
            "    wait(0.001)\n" +
            "}\n" +
            "print(\"done\")");
        Assert.True(ok, output);
        Assert.Contains("done", output);
    }

    [Fact]
    public void Debug_PausesAtEntry_ThenContinues()
    {
        var paused = new ManualResetEventSlim(false);
        var lines = new List<string>();
        var interp = new Interpreter(new NullBackend(_ => { }), s => { lock (lines) lines.Add(s); }, 1.0)
        {
            DebugEnabled = true,
            PauseAtEntry = true,
        };
        interp.DebugPaused = () => paused.Set();

        var program = new Parser(new Lexer("let x = 1\nprint(\"mid\")\nlet y = 2").Tokenize()).ParseProgram();
        var t = new Thread(() =>
        {
            try { interp.Run(program); } catch { }
        }) { IsBackground = true };
        t.Start();

        Assert.True(paused.Wait(3000), "Debug never paused at entry");
        Assert.Equal(1, interp.CurrentLine);
        Assert.False(LockedContains(lines, "mid"), "First statement ran before entry pause");

        interp.DebugContinue();
        Assert.True(t.Join(3000), "Script did not finish after Continue");
        Assert.True(LockedContains(lines, "mid"), "Script did not continue after entry pause");
    }

    private static bool LockedContains(List<string> lines, string v)
    {
        lock (lines) return lines.Contains(v);
    }
}
