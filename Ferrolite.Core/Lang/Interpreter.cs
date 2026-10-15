namespace Ferrolite.Core.Lang;

public class Interpreter
{
    public Action<string>? OutputFn { get; set; }
    public double SpeedMultiplier { get; set; } = 1.0;
    public Environment GlobalEnv { get; }
    public IActionBackend? ActionBackend { get; set; }
    public IScriptGuiBridge? ScriptGuiBridge { get; set; }

    private bool _stopRequested;
    private bool _pauseRequested;
    public Dictionary<string, object> Stats { get; } = new()
    {
        ["statements_executed"] = 0,
        ["start_time"] = null!,
        ["commands_run"] = 0,
    };

    
    public bool DebugEnabled { get; set; }

    
    
    public bool PauseAtEntry { get; set; }
    public Func<int, bool>? BreakpointAt { get; set; }
    public Action? DebugPaused { get; set; }
    public event Action<int>? StatementEntered;
    public int CurrentLine { get; private set; }

    private readonly ManualResetEventSlim _debugContinue = new(true);
    private readonly object _debugLock = new();
    private bool _debugStepMode;
    private bool _entryPaused;
    private int _stepOverDepth = -1;
    private int _stepOutDepth = -1;
    private int _callDepth;
    private Environment? _debugEnv;
    private readonly List<(string Name, int Line)> _callFrames = new() { ("<script>", 0) };

    
    public IReadOnlyList<(string Name, int Line)> GetCallFrames()
    {
        lock (_debugLock) { return _callFrames.ToList(); }
    }

    
    public List<(string Name, string Value)> SnapshotScope()
    {
        var result = new List<(string, string)>();
        var seen = new HashSet<string>();
        for (var env = _debugEnv ?? GlobalEnv; env != null; env = env.Parent)
        {
            foreach (var kv in env.LocalVars())
            {
                if (kv.Value is VsFunction or NativeFunction) continue;
                if (seen.Add(kv.Key))
                    result.Add((kv.Key, ValueHelpers.VsRepr(kv.Value)));
            }
        }
        return result;
    }

    public void DebugContinue() => ResumeGate(false, -1, -1);
    public void DebugStepOver() => ResumeGate(false, _callDepth, -1);
    public void DebugStepInto() => ResumeGate(true, -1, -1);
    public void DebugStepOut() => ResumeGate(false, -1, Math.Max(0, _callDepth - 1));

    private void ResumeGate(bool stepInto, int stepOverDepth, int stepOutDepth)
    {
        lock (_debugLock)
        {
            _debugStepMode = stepInto;
            _stepOverDepth = stepOverDepth;
            _stepOutDepth = stepOutDepth;
        }
        _debugContinue.Set();
    }

    private void DebugGate(int line)
    {
        if (!DebugEnabled) return;
        bool shouldBreak;
        lock (_debugLock)
        {
            shouldBreak = _debugStepMode
                          || (_stepOverDepth >= 0 && _callDepth <= _stepOverDepth)
                          || (_stepOutDepth >= 0 && _callDepth <= _stepOutDepth)
                          || (BreakpointAt?.Invoke(line) ?? false)
                          || (PauseAtEntry && !_entryPaused);
            if (shouldBreak)
            {
                if (PauseAtEntry && !_entryPaused) _entryPaused = true;
                _debugStepMode = false;
                _stepOverDepth = -1;
                _stepOutDepth = -1;
            }
        }
        if (!shouldBreak) return;
        _debugContinue.Reset();
        DebugPaused?.Invoke();
        while (!_debugContinue.IsSet)
        {
            if (_stopRequested) { _debugContinue.Set(); return; }
            _debugContinue.Wait(50);
        }
    }

    public Interpreter(IActionBackend? actionBackend = null, Action<string>? outputFn = null, double speedMultiplier = 1.0)
    {
        ActionBackend = actionBackend;
        OutputFn = outputFn;
        SpeedMultiplier = Math.Max(0.01, speedMultiplier);
        GlobalEnv = new Environment();
        StdLib.BuildGlobals(GlobalEnv, this);
    }

    public void RequestStop() => _stopRequested = true;

    
    public bool StopRequested => _stopRequested;
    public void RequestPause(bool paused = true) => _pauseRequested = paused;

    internal void CheckControl()
    {
        if (_stopRequested)
            throw new VSRuntimeError("__STOP__", 0, "__STOP__");
        while (_pauseRequested && !_stopRequested)
            Thread.Sleep(50);
    }

    public void SleepScaled(double seconds)
    {
        double remaining = seconds / SpeedMultiplier;
        double step = 0.05;
        while (remaining > step)
        {
            CheckControl();
            Thread.Sleep(TimeSpan.FromSeconds(step));
            remaining -= step;
        }
        if (remaining > 0)
        {
            CheckControl();
            Thread.Sleep(TimeSpan.FromSeconds(remaining));
        }
    }

    public void Log(string text) => OutputFn?.Invoke(text);

    public void Run(Program program)
    {
        _stopRequested = false;
        _pauseRequested = false;
        _debugEnv = null;
        _callDepth = 0;
        _callFrames.Clear();
        _callFrames.Add(("<script>", 0));
        _debugContinue.Set();
        Stats["start_time"] = DateTime.Now;
        try
        {
            ExecBlockStatements(program.Statements, GlobalEnv);
        }
        catch (ReturnSignal) { }
        catch (VSRuntimeError e)
        {
            if (e.VsValue?.ToString() == "__STOP__")
                Log("Execution stopped by user.");
            else
                throw;
        }
    }

    private void ExecBlockStatements(List<AstNode> statements, Environment env)
    {
        foreach (var stmt in statements)
        {
            CheckControl();
            ExecStmt(stmt, env);
        }
    }

    private void ExecStmt(AstNode node, Environment env)
    {
        Stats["statements_executed"] = (int)Stats["statements_executed"] + 1;
        CurrentLine = node.Line;
        _debugEnv = env;
        DebugGate(node.Line);
        StatementEntered?.Invoke(node.Line);

        switch (node)
        {
            case LetStmt n: ExecLetStmt(n, env); break;
            case ExprStmt n: Eval(n.Expr, env); break;
            case IfStmt n: ExecIfStmt(n, env); break;
            case WhileStmt n: ExecWhileStmt(n, env); break;
            case ForStmt n: ExecForStmt(n, env); break;
            case Block n: ExecBlock(n, env); break;
            case FuncDecl n: ExecFuncDecl(n, env); break;
            case ReturnStmt n: ExecReturnStmt(n, env); break;
            case BreakStmt: throw new BreakSignal();
            case ContinueStmt: throw new ContinueSignal();
            case TryStmt n: ExecTryStmt(n, env); break;
            case ThrowStmt n: ExecThrowStmt(n, env); break;
            case ImportStmt: break; 
            default:
                throw new VSRuntimeError(
                    $"Internal error: No executor for statement type '{node.GetType().Name}' at line {node.Line}",
                    node.Line);
        }
    }

    private void ExecLetStmt(LetStmt n, Environment env)
    {
        object? value = n.Value != null ? Eval(n.Value, env) : null;
        env.Define(n.Name, value);
    }

    private void ExecIfStmt(IfStmt n, Environment env)
    {
        if (IsTruthy(Eval(n.Condition, env)))
            ExecBlock(n.ThenBranch, new Environment(env));
        else if (n.ElseBranch != null)
        {
            if (n.ElseBranch is IfStmt elseIf)
                ExecIfStmt(elseIf, env);
            else if (n.ElseBranch is Block elseBlock)
                ExecBlock(elseBlock, new Environment(env));
        }
    }

    private void ExecWhileStmt(WhileStmt n, Environment env)
    {
        int guard = 0;
        while (IsTruthy(Eval(n.Condition, env)))
        {
            CheckControl();
            guard++;
            if (guard > 5_000_000)
                throw new VSRuntimeError("Possible infinite loop (5,000,000 iteration safety limit)", n.Line);
            try
            {
                ExecBlock(n.Body, new Environment(env));
            }
            catch (BreakSignal) { break; }
            catch (ContinueSignal) { continue; }
        }
    }

    private void ExecForStmt(ForStmt n, Environment env)
    {
        object? iterable = Eval(n.Iterable, env);
        List<object?> items = iterable switch
        {
            List<object?> lst => lst,
            System.Collections.IDictionary dict => dict.Keys.Cast<object?>().ToList(),
            string s => s.Select(c => (object?)c.ToString()!).ToList(),
            _ => throw new VSRuntimeError($"Cannot iterate over {VsTypeName(iterable)}. Expected list, map, string, or range.", n.Line),
        };

        foreach (var item in items)
        {
            CheckControl();
            var loopEnv = new Environment(env);
            loopEnv.Define(n.VarName, item);
            try
            {
                ExecBlock(n.Body, loopEnv);
            }
            catch (BreakSignal) { break; }
            catch (ContinueSignal) { continue; }
        }
    }

    private void ExecBlock(Block n, Environment env)
    {
        ExecBlockStatements(n.Statements, env);
    }

    private void ExecFuncDecl(FuncDecl n, Environment env)
    {
        var func = new VsFunction(n.Name, n.Params, n.Body, env, true);
        env.Define(n.Name, func);
    }

    private void ExecReturnStmt(ReturnStmt n, Environment env)
    {
        object? value = n.Value != null ? Eval(n.Value, env) : null;
        throw new ReturnSignal(value);
    }

    private void ExecTryStmt(TryStmt n, Environment env)
    {
        try
        {
            ExecBlock(n.TryBlock, new Environment(env));
        }
        catch (VSRuntimeError e)
        {
            if (e.VsValue?.ToString() == "__STOP__") throw;
            var catchEnv = new Environment(env);
            if (n.CatchVar != null)
                catchEnv.Define(n.CatchVar, e.VsValue ?? e.Message);
            ExecBlock(n.CatchBlock, catchEnv);
        }
        catch (BreakSignal) { throw; }
        catch (ContinueSignal) { throw; }
        catch (ReturnSignal) { throw; }
        catch (Exception e)
        {
            var catchEnv = new Environment(env);
            if (n.CatchVar != null)
                catchEnv.Define(n.CatchVar, e.Message);
            ExecBlock(n.CatchBlock, catchEnv);
        }
    }

    private void ExecThrowStmt(ThrowStmt n, Environment env)
    {
        object? value = Eval(n.Value, env);
        throw new VSRuntimeError(VsRepr(value), n.Line, value);
    }

    public object? Eval(AstNode node, Environment env)
    {
        return node switch
        {
            NumberLit n => (object)n.Value,
            StringLit n => n.Value,
            BoolLit n => n.Value,
            NullLit => null,
            ListLit n => (object)n.Elements.Select(e => Eval(e, env)).ToList(),
            MapLit n => (object)n.Keys.Zip(n.Values, (k, v) => (Key: Eval(k, env), Value: Eval(v, env)))
                        .ToDictionary(x => x.Key!, x => x.Value),
            Identifier n => env.Get(n.Name, n.Line),
            FuncExpr n => new VsFunction("<anonymous>", n.Params, n.Body, env, n.IsBlock),
            Assign n => EvalAssign(n, env),
            BinaryOp n => EvalBinaryOp(n, env),
            UnaryOp n => EvalUnaryOp(n, env),
            LogicalOp n => EvalLogicalOp(n, env),
            Index n => EvalIndex(n, env),
            MemberAccess n => EvalMemberAccess(n, env),
            Call n => EvalCall(n, env),
            _ => throw new VSRuntimeError(
                $"Internal error: No evaluator for expression type '{node.GetType().Name}' at line {node.Line}",
                node.Line),
        };
    }

    private object? EvalAssign(Assign n, Environment env)
    {
        object? value;
        if (n.Op == "=")
            value = Eval(n.Value, env);
        else
        {
            object? current = Eval(n.Target, env);
            object? rhs = Eval(n.Value, env);
            value = Binary(n.Op == "+=" ? "+" : "-", current, rhs, n.Line);
        }

        if (n.Target is Identifier id)
            env.Set(id.Name, value, n.Line);
        else if (n.Target is Index idx)
        {
            object? obj = Eval(idx.Obj, env);
            object? index = Eval(idx.IndexExpr, env);
            if (obj is List<object?> list && index is double di)
                list[(int)di] = value;
            else if (obj is System.Collections.IDictionary dict)
                dict[index!] = value;
            else
                throw new VSRuntimeError($"Cannot index-assign into {VsTypeName(obj)}", n.Line);
        }
        else if (n.Target is MemberAccess ma)
        {
            object? obj = Eval(ma.Obj, env);
            if (obj is System.Collections.IDictionary dict)
                dict[ma.Name] = value;
            else
                throw new VSRuntimeError($"Cannot set property on {VsTypeName(obj)}", n.Line);
        }
        else
            throw new VSRuntimeError("Invalid assignment target", n.Line);

        return value;
    }

    private object? EvalBinaryOp(BinaryOp n, Environment env)
    {
        object? left = Eval(n.Left, env);
        object? right = Eval(n.Right, env);
        return Binary(n.Op, left, right, n.Line);
    }

    private object? Binary(string op, object? left, object? right, int line)
    {
        try
        {
            return op switch
            {
                "+" => (left, right) switch
                {
                    (string s1, string s2) => s1 + s2,
                    (string s1, _) => s1 + VsRepr(right),
                    (_, string s2) => VsRepr(left) + s2,
                    (List<object?> l1, List<object?> l2) => (object?)l1.Concat(l2).ToList(),
                    (double d1, double d2) => d1 + d2,
                    (int i1, int i2) => (double)(i1 + i2),
                    (double d1, int i2) => d1 + i2,
                    (int i1, double d2) => i1 + d2,
                    _ => throw new Exception(),
                },
                "-" => ToDouble(left) - ToDouble(right),
                "*" => ToDouble(left) * ToDouble(right),
                "/" => ToDouble(right) == 0
                    ? throw new VSRuntimeError("Division by zero", line)
                    : ToDouble(left) / ToDouble(right),
                "%" => ToDouble(left) % ToDouble(right),
                "==" => Equals(left, right),
                "!=" => !Equals(left, right),
                "<" => ToDouble(left) < ToDouble(right),
                ">" => ToDouble(left) > ToDouble(right),
                "<=" => ToDouble(left) <= ToDouble(right),
                ">=" => ToDouble(left) >= ToDouble(right),
                _ => throw new VSRuntimeError($"Unknown operator '{op}'", line),
            };
        }
        catch (VSRuntimeError) { throw; }
        catch (Exception e)
        {
            throw new VSRuntimeError($"Type error in '{op}' operation: {e.Message}", line);
        }
    }

    private static double ToDouble(object? v) => v switch
    {
        double d => d,
        int i => i,
        _ => throw new Exception($"Cannot convert {VsTypeName(v)} to number"),
    };

    private object? EvalUnaryOp(UnaryOp n, Environment env)
    {
        object? val = Eval(n.Operand, env);
        if (n.Op == "-")
            return -(double)val!;
        if (n.Op == "not")
            return !IsTruthy(val);
        throw new VSRuntimeError($"Unknown unary operator {n.Op}", n.Line);
    }

    private object? EvalLogicalOp(LogicalOp n, Environment env)
    {
        object? left = Eval(n.Left, env);
        if (n.Op == "and")
            return IsTruthy(left) ? Eval(n.Right, env) : left;
        else
            return IsTruthy(left) ? left : Eval(n.Right, env);
    }

    private object? EvalIndex(Index n, Environment env)
    {
        object? obj = Eval(n.Obj, env);
        object? idx = Eval(n.IndexExpr, env);

        if (obj is List<object?> list && idx is double di)
        {
            int i = (int)di;
            if (i < 0 || i >= list.Count)
                throw new VSRuntimeError($"List index {di} out of range (len={list.Count})", n.Line);
            return list[i];
        }
        if (obj is System.Collections.IDictionary dict)
        {
            if (dict.Contains(idx!))
                return dict[idx!];
            throw new VSRuntimeError($"Key {VsRepr(idx)} not found in map", n.Line);
        }
        if (obj is string s && idx is double si)
        {
            int i = (int)si;
            if (i < 0 || i >= s.Length)
                throw new VSRuntimeError($"String index {si} out of range", n.Line);
            return s[i].ToString();
        }
        throw new VSRuntimeError($"Cannot index into {VsTypeName(obj)}", n.Line);
    }

    private object? EvalMemberAccess(MemberAccess n, Environment env)
    {
        object? obj = Eval(n.Obj, env);
        if (obj is System.Collections.IDictionary dict)
        {
            if (dict.Contains(n.Name))
                return dict[n.Name];
            throw new VSRuntimeError($"Map has no key '{n.Name}'", n.Line);
        }
        throw new VSRuntimeError($"Cannot access property '{n.Name}' on {VsTypeName(obj)}", n.Line);
    }

    private object? EvalCall(Call n, Environment env)
    {
        object? callee = Eval(n.Callee, env);
        var args = n.Args.Select(a => Eval(a, env)).ToList();

        if (callee is NativeFunction nf)
        {
            Stats["commands_run"] = (int)Stats["commands_run"] + 1;
            try { return nf.Fn(args.ToArray()); }
            catch (VSRuntimeError) { throw; }
            catch (Exception e) { throw new VSRuntimeError($"Error calling '{nf.Name}': {e.Message}", n.Line); }
        }
        if (callee is VsFunction vsf)
            return CallVsFunction(vsf, args, n.Line);

        throw new VSRuntimeError($"'{VsRepr(callee)}' is not callable", n.Line);
    }

    public object? CallVsFunction(VsFunction func, List<object?> args, int line)
    {
        var callEnv = new Environment(func.Closure);
        for (int i = 0; i < func.Params.Count; i++)
            callEnv.Define(func.Params[i], i < args.Count ? args[i] : null);

        if (DebugEnabled)
        {
            lock (_debugLock)
            {
                _callDepth++;
                _callFrames.Add((string.IsNullOrEmpty(func.Name) ? "<anonymous>" : func.Name, line));
            }
        }
        try
        {
            if (func.IsBlock)
            {
                try { ExecBlockStatements(((Block)func.Body).Statements, callEnv); }
                catch (ReturnSignal r) { return r.Value; }
                return null;
            }
            else
            {
                return Eval(func.Body, callEnv);
            }
        }
        finally
        {
            if (DebugEnabled)
            {
                lock (_debugLock)
                {
                    _callDepth--;
                    if (_callFrames.Count > 1) _callFrames.RemoveAt(_callFrames.Count - 1);
                }
            }
        }
    }

    
    public static string VsTypeName(object? value) => ValueHelpers.VsTypeName(value);
    public static bool IsTruthy(object? value) => ValueHelpers.IsTruthy(value);
    public static string VsRepr(object? value) => ValueHelpers.VsRepr(value);
}
