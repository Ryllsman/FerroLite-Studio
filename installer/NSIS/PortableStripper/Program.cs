using System.Text;

var root = (string?)null;
var exts = new[] { "cs", "xaml", "csproj", "slnx" };
var quiet = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "/s" when i + 1 < args.Length:
            root = args[++i];
            break;
        case "/e" when i + 1 < args.Length:
            exts = args[++i].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            break;
        case "/q":
            quiet = true;
            break;
    }
}

if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
{
    Console.Error.WriteLine("Invalid or missing /s <root>.");
    return 2;
}

var processed = 0;
foreach (var ext in exts)
foreach (var file in Directory.EnumerateFiles(root!, "*." + ext,
             new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
{
    StripFile(file);
    processed++;
}

if (!quiet)
    Console.WriteLine($"Stripped comments from {processed} file(s).");
return 0;

static void StripFile(string path)
{
    var text = File.ReadAllText(path, Encoding.UTF8);
    var sb = new StringBuilder(text.Length);
    StripComments(text, sb);
    File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

static void StripComments(string src, StringBuilder dst)
{
    const int Code = 0, Line = 1, Block = 2, Xml = 3, StrDq = 4, StrSq = 5;
    var state = Code;
    var n = src.Length;

    for (var i = 0; i < n; i++)
    {
        var c = src[i];
        var nx = i + 1 < n ? src[i + 1] : '\0';
        var nx2 = i + 2 < n ? src[i + 2] : '\0';

        switch (state)
        {
            case Line:
                if (c == '\n') { dst.Append(c); state = Code; }
                break;

            case Block:
                if (c == '*' && nx == '/') { i++; state = Code; }
                break;

            case Xml:
                if (c == '-' && nx == '-' && nx2 == '>') { i += 2; state = Code; }
                break;

            case StrDq:
                dst.Append(c);
                if (c == '\\' && nx != '\0') { dst.Append(nx); i++; }
                else if (c == '"' || c == '\n') state = Code;
                break;

            case StrSq:
                dst.Append(c);
                if (c == '\\' && nx != '\0') { dst.Append(nx); i++; }
                else if (c == '\'' || c == '\n') state = Code;
                break;

            default:
                if (c == '/' && nx == '/') { state = Line; i++; break; }
                if (c == '/' && nx == '*') { state = Block; i++; break; }
                if (c == '<' && nx == '!' && nx2 == '-' && i + 3 < n && src[i + 3] == '-')
                {
                    dst.Append(' '); 
                    state = Xml;
                    i += 3;
                    break;
                }
                if (c == '"') { dst.Append(c); state = StrDq; break; }
                if (c == '\'') { dst.Append(c); state = StrSq; break; }
                dst.Append(c);
                break;
        }
    }
}
