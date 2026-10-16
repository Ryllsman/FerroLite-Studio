using System.Diagnostics;
using Ferrolite.Core;
using Ferrolite.Core.Lang;
using Ferrolite.Core.Recorder;
using Ferrolite.UI.Services;

namespace Ferrolite.UI.Services;

public enum RunMode { Run, Debug }






public class ScriptRunner
{
    public event Action<string, string>? Output;      
    public event Action<double, int>? FinishedOk;     
    public event Action<string, int>? FinishedError;  
    public event Action<bool>? StateChanged;          
    public event Action? DebugPaused;                 

    private Thread? _thread;
    private CancellationTokenSource? _cts;
    private double _speed = 1.0;

    public Interpreter? Interp { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsDebug { get; private set; }
    public RunMode Mode => IsDebug ? RunMode.Debug : RunMode.Run;

    private readonly ScriptGuiBridge? _guiBridge;

    public ScriptRunner(ScriptGuiBridge? guiBridge = null)
    {
        _guiBridge = guiBridge;
        RuntimePaths.RecordingsDir = AppStorage.RecordingsDir;
    }

    public void Start(string source, bool debug, IEnumerable<int>? breakpoints = null)
    {
        StopWait();
        IsRunning = true;
        IsDebug = debug;
        var bpSet = new HashSet<int>(breakpoints ?? Enumerable.Empty<int>());

        _cts = new CancellationTokenSource();
        _thread = new Thread(() => RunLoop(source, debug, bpSet, _cts!.Token))
        {
            IsBackground = true,
            Name = "FerroliteRunner",
        };
        _thread.Start();
    }

    private void RunLoop(string source, bool debug, HashSet<int> bpSet, CancellationToken ct)
    {
        double speed = _speed;
        try
        {
            var backend = new LoggingBackend(new WindowsBackend(), msg => Output?.Invoke(msg, "action"));
            var interp = new Interpreter(backend, s => Output?.Invoke(s, "info"), speed);
            Interp = interp;
            if (_guiBridge != null)
                _guiBridge.AttachTo(interp);

            if (debug)
            {
                interp.DebugEnabled = true;
                interp.PauseAtEntry = true; 
                interp.BreakpointAt = line => bpSet.Contains(line);
                interp.DebugPaused = () => DebugPaused?.Invoke();
            }

            var tokens = new Lexer(source).Tokenize();
            var program = new Parser(tokens).ParseProgram();

            var sw = Stopwatch.StartNew();
            interp.Run(program);
            sw.Stop();
            Output?.Invoke(
                $"Finished in {sw.Elapsed.TotalSeconds:F2}s ({interp.Stats["commands_run"]} commands)",
                "success");
            FinishedOk?.Invoke(sw.Elapsed.TotalSeconds, (int)interp.Stats["commands_run"]);
        }
        catch (VSRuntimeError e)
        {
            if (e.VsValue?.ToString() == "__STOP__")
            {
                Output?.Invoke("Execution stopped by user.", "warning");
                FinishedError?.Invoke("Stopped by user.", 0);
            }
            else
            {
                string msg = e.Line > 0 ? $"Runtime error at line {e.Line}: {e.Message}" : $"Runtime error: {e.Message}";
                Output?.Invoke(msg, "error");
                FinishedError?.Invoke(e.Message, e.Line);
            }
        }
        catch (Exception e)
        {
            Output?.Invoke($"Internal error: {e.Message}", "error");
            FinishedError?.Invoke(e.Message, 0);
        }        finally
        {
            Interp = null;
            IsRunning = false;
            StateChanged?.Invoke(false);
        }
    }

    public void StopWait()
    {
        Interp?.RequestStop();
        if (_thread != null && _thread.IsAlive)
        {
            
            _thread.Join(TimeSpan.FromMilliseconds(300));
            if (_thread.IsAlive)
            {
                _cts?.Cancel();
                _thread.Join(TimeSpan.FromMilliseconds(200));
            }
        }
        
        
        Interp = null;
        IsRunning = false;
    }

    public void Stop() => Interp?.RequestStop();
    public void Pause(bool paused) => Interp?.RequestPause(paused);
    public void SetSpeed(double speed)
    {
        _speed = Math.Max(0.01, speed);
        if (Interp != null) Interp.SpeedMultiplier = _speed;
    }

    
    public void DebugContinue() => Interp?.DebugContinue();
    public void DebugStepOver() => Interp?.DebugStepOver();
    public void DebugStepInto() => Interp?.DebugStepInto();
    public void DebugStepOut() => Interp?.DebugStepOut();
}
