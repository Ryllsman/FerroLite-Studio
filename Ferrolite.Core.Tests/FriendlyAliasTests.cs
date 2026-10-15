using Ferrolite.Core.Lang;

namespace Ferrolite.Core.Tests;

public class FriendlyAliasTests
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
    public void AllFriendlyAliases_ResolveToFunctions()
    {
        
        var (output, ok) = Run(
            "print(typeof(reverse_text))\n" +
            "print(typeof(ord))\n" +
            "print(typeof(chr))\n" +
            "print(typeof(count_text))\n" +
            "print(typeof(contains_text))\n" +
            "print(typeof(map_values))\n" +
            "print(typeof(filter_map))\n" +
            "print(typeof(entries))\n" +
            "print(typeof(remove_key))");
        Assert.True(ok, output);
        string[] lines = output.Split('\n');
        Assert.Equal(9, lines.Length);
        foreach (var line in lines)
            Assert.Equal("function", line);
    }

    [Fact]
    public void FriendlyAliases_ActuallyRun()
    {
        var (output, ok) = Run(
            "print(reverse_text(\"abc\"))\n" +                 
            "print(ord(\"A\"))\n" +                            
            "print(chr(66))\n" +                               
            "print(contains_text(\"hello\", \"ell\"))\n" +     
            "let doubled = map_values({a: 1, b: 2}, func(v) => v * 10)\n" +
            "print(doubled[\"b\"])\n" +                        
            "let m = {a: 1, b: 2}\n" +
            "remove_key(m, \"a\")\n" +
            "print(has_key(m, \"a\"))\n" +                      
            "print(len(entries({x: 1, y: 2})))\n" +            
            "print(count_text(\"banana\", \"a\"))");           
        Assert.True(ok, output);
        string[] lines = output.Split('\n');
        Assert.Contains("cba", lines);
        Assert.Contains("65", lines);
        Assert.Contains("B", lines);
        Assert.Contains("true", lines);
        Assert.Contains("20", lines);
        Assert.Contains("false", lines);
        Assert.Contains("2", lines);
        Assert.Contains("3", lines);
    }
}
