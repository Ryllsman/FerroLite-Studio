namespace Ferrolite.Core.Lang;

public record VsFunction(string Name, List<string> Params, AstNode Body, Environment Closure, bool IsBlock = true);

public record NativeFunction(string Name, Func<object?[], object?> Fn, int? Arity = null);


public class BreakSignal : Exception { }

public class ContinueSignal : Exception { }

public class ReturnSignal : Exception
{
    public object? Value { get; }
    public ReturnSignal(object? value) { Value = value; }
}

public static class ValueHelpers
{
    public static string VsTypeName(object? value)
    {
        return value switch
        {
            null => "null",
            bool => "boolean",
            int or double => "number",
            string => "string",
            List<object?> => "list",
            Dictionary<object?, object?> => "map",
            VsFunction => "function",
            NativeFunction => "function",
            _ => value.GetType().Name,
        };
    }

    public static bool IsTruthy(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            int i => i != 0,
            double d => d != 0,
            string s => s.Length > 0,
            List<object?> l => l.Count > 0,
            Dictionary<object?, object?> m => m.Count > 0,
            _ => true,
        };
    }

    public static string VsRepr(object? value)
    {
        return value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            double d => d == (int)d ? ((int)d).ToString() : d.ToString(),
            int i => i.ToString(),
            List<object?> lst => "[" + string.Join(", ", lst.Select(VsRepr)) + "]",
            Dictionary<object?, object?> map => "{" + string.Join(", ", map.Select(kv => $"{VsRepr(kv.Key)}: {VsRepr(kv.Value)}")) + "}",
            VsFunction f => $"<function {f.Name}>",
            NativeFunction nf => $"<function {nf.Name}>",
            _ => value.ToString() ?? "",
        };
    }
}
