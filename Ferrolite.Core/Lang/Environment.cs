namespace Ferrolite.Core.Lang;

public class VSRuntimeError : Exception
{
    public override string Message { get; }
    public int Line { get; }
    public object? VsValue { get; }

    public VSRuntimeError(string message, int line = 0, object? vsValue = null)
        : base(message)
    {
        Message = message;
        Line = line;
        VsValue = vsValue ?? message;
    }
}

public class Environment
{
    private readonly Dictionary<string, object?> _vars = new();
    public Environment? Parent { get; }

    public Environment(Environment? parent = null)
    {
        Parent = parent;
    }

    public void Define(string name, object? value)
    {
        _vars[name] = value;
    }

    public object? Get(string name, int line = 0)
    {
        var env = this;
        while (env != null)
        {
            if (env._vars.TryGetValue(name, out object? val))
                return val;
            env = env.Parent;
        }
        throw new VSRuntimeError($"Undefined variable '{name}'", line);
    }

    public void Set(string name, object? value, int line = 0)
    {
        
        
        var env = this;
        while (env != null)
        {
            if (env._vars.ContainsKey(name))
            {
                env._vars[name] = value;
                return;
            }
            env = env.Parent;
        }
        
        _vars[name] = value;
    }

    public bool Has(string name)
    {
        var env = this;
        while (env != null)
        {
            if (env._vars.ContainsKey(name))
                return true;
            env = env.Parent;
        }
        return false;
    }

    
    public IReadOnlyCollection<KeyValuePair<string, object?>> LocalVars() => _vars;
}
