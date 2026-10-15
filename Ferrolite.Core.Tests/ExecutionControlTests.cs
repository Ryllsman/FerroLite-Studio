using Ferrolite.Core.Lang;

namespace Ferrolite.Core.Tests;

public class ExecutionControlTests
{
    private static Program CompileProgram(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        return new Parser(tokens).ParseProgram();
    }

    private static Thread RunAsync(Interpreter interp, Program program)
    {
        var t = new Thread(() =>
        {
            try { interp.Run(program); }
            catch {  }
        }) { IsBackground = true };
        t.Start();
        return t;
    }

    private static bool LockedContains(List<string> lines, string value)
    {
        lock (lines) return lines.Contains(value);
    }

    [Fact]
    public void Stop_InterruptsInfiniteLoop()
    {
        var interp = new Interpreter(new NullBackend(_ => { }), _ => { }, 1.0);
        var program = CompileProgram("while true { }");
        var t = RunAsync(interp, program);
        Thread.Sleep(150);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        interp.RequestStop();
        Assert.True(t.Join(3000), "Interpreter did not stop within 3s");
        Assert.True(sw.Elapsed.TotalSeconds < 2.5, $"Stop took too long: {sw.Elapsed.TotalSeconds:F2}s");
    }

    [Fact]
    public void Stop_InterruptsSleepingWait()
    {
        var lines = new List<string>();
        var interp = new Interpreter(new NullBackend(_ => { }), s => { lock (lines) lines.Add(s); }, 1.0);
        var program = CompileProgram("print(\"start\")\nwait(30)\nprint(\"after\")");
        var t = RunAsync(interp, program);
        for (int i = 0; i < 200 && !LockedContains(lines, "start"); i++) Thread.Sleep(10);
        Assert.True(LockedContains(lines, "start"), "\"start\" never printed");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        interp.RequestStop();
        Assert.True(t.Join(3000), "Wait was not interrupted by Stop");
        Assert.True(sw.Elapsed.TotalSeconds < 2.5);
        Assert.False(LockedContains(lines, "after"), "Script continued after Stop");
    }

    [Fact]
    public void Pause_ThenResume_CompletesScript()
    {
        var lines = new List<string>();
        var interp = new Interpreter(new NullBackend(_ => { }), s => { lock (lines) lines.Add(s); }, 1.0);
        var program = CompileProgram("print(\"a\")\nwait(2)\nprint(\"b\")");

        var t = RunAsync(interp, program);
        for (int i = 0; i < 200 && !LockedContains(lines, "a"); i++) Thread.Sleep(10);
        Assert.True(LockedContains(lines, "a"), "\"a\" never printed");

        interp.RequestPause(true);
        Thread.Sleep(300);
        Assert.True(t.IsAlive, "Script finished while paused");
        Assert.False(LockedContains(lines, "b"), "\"b\" printed while paused");

        interp.RequestPause(false);
        Assert.True(t.Join(4000), "Script did not resume after pause");
        Assert.True(LockedContains(lines, "b"), "\"b\" never printed after resume");
    }

    [Fact]
    public void SpeedMultiplier_ScalesWaits()
    {
        
        var lines = new List<string>();
        var interp = new Interpreter(new NullBackend(_ => { }), lines.Add, 4.0);
        var program = CompileProgram("wait(2)\nprint(\"done\")");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        interp.Run(program);
        sw.Stop();
        Assert.Contains("done", lines);
        Assert.True(sw.Elapsed.TotalSeconds < 1.6, $"4x wait(2) took {sw.Elapsed.TotalSeconds:F2}s");
    }

    [Fact]
    public void DebugBreakpoint_PausesAndContinues()
    {
        var linesOut = new List<string>();
        var interp = new Interpreter(new NullBackend(_ => { }), s => { lock (linesOut) linesOut.Add(s); }, 1.0);
        var program = CompileProgram("let x = 1\nprint(\"first\")\nlet y = 2\nprint(\"done\")");
        const int breakLine = 3; 
        var paused = new ManualResetEventSlim(false);

        interp.DebugEnabled = true;
        interp.BreakpointAt = ln => ln == breakLine;
        interp.DebugPaused = () => paused.Set();
        var t = RunAsync(interp, program);

        Assert.True(paused.Wait(3000), "Breakpoint never hit");
        Assert.Equal(breakLine, interp.CurrentLine);
        var scope = interp.SnapshotScope();
        Assert.Contains(scope, v => v.Name == "x" && v.Value == "1");
        Assert.DoesNotContain(scope, v => v.Name == "y");
        Assert.False(LockedContains(linesOut, "done"), "Script ran past breakpoint");

        interp.DebugContinue();
        Assert.True(t.Join(3000), "Script did not finish after DebugContinue");
        Assert.True(LockedContains(linesOut, "done"), "\"done\" never printed");
    }

    [Fact]
    public void DebugStepInto_EntersFunctionBody()
    {
        var paused = new ManualResetEventSlim(false);
        var interp = new Interpreter(new NullBackend(_ => { }), _ => { }, 1.0);
        var program = CompileProgram("func add(a, b) { let s = a + b\n return s }\nprint(add(1, 2))");
        interp.DebugEnabled = true;
        interp.BreakpointAt = ln => ln == 1; 
        interp.DebugPaused = () => paused.Set();
        var t = RunAsync(interp, program);

        Assert.True(paused.Wait(3000), "Initial breakpoint not hit");
        Assert.Single(interp.GetCallFrames());

        
        paused.Reset();
        interp.DebugStepInto();
        Assert.True(paused.Wait(3000), "Step Into (1) did not pause again");
        Assert.Single(interp.GetCallFrames());

        
        paused.Reset();
        interp.DebugStepInto();
        Assert.True(paused.Wait(3000), "Step Into (2) did not pause again");
        Assert.True(interp.GetCallFrames().Count >= 2, "Not inside function after Step Into");
        Assert.Contains(interp.SnapshotScope(), v => v.Name == "a");

        interp.DebugContinue();
        Assert.True(t.Join(3000), "Script did not finish after continue");
    }

    [Fact]
    public void ControllerPassthrough_DoesNotBlockTheScript()
    {
        
        
        
        
        
        
        var lines = new List<string>();
        var interp = new Interpreter(new NullBackend(_ => { }), s => { lock (lines) lines.Add(s); }, 1.0);
        var program = CompileProgram(
            "try { controller_passthrough(true) } catch(err) { print(\"no driver\") }\n" +
            "print(\"after-arm\")");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var t = RunAsync(interp, program);
        bool finished = t.Join(TimeSpan.FromSeconds(5));
        sw.Stop();
        
        try { if (ControllerPassthrough.Active) ControllerPassthrough.Stop(); } catch { }

        Assert.True(finished, "controller_passthrough(true) blocked the script");
        Assert.True(sw.Elapsed.TotalSeconds < 4.0, $"controller_passthrough(true) stalled for {sw.Elapsed.TotalSeconds:F2}s");
        Assert.True(LockedContains(lines, "after-arm"), "statements after controller_passthrough(true) never ran");
    }
}
