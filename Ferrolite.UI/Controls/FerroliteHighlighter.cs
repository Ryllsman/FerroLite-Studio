using System.Text.RegularExpressions;
using System.Windows;
using ICSharpCode.AvalonEdit.Highlighting;

namespace Ferrolite.UI.Controls;







public static class FerroliteHighlighter
{
    private static IHighlightingDefinition? _cached;

    private static readonly string[] Keywords =
    {
        "let", "if", "else", "while", "for", "in", "func", "return", "break", "continue",
        "try", "catch", "throw", "true", "false", "null", "import", "and", "or", "not"
    };

    private static readonly string[] Builtins =
    {
        "print", "log", "debug", "typeof", "str", "num", "bool", "len", "assert", "range",
        "wait", "sleep", "delay", "now", "now_ms", "play_recording",
        "move", "mouse_move", "move_relative", "mouse_move_relative", "click", "double_click",
        "right_click", "mouse_down", "mouse_up", "drag", "scroll", "get_mouse_pos", "mouse_position",
        "key_press", "press_key", "key_down", "key_up", "key_hold", "type_text", "type", "hotkey",
        "key_combo", "focus_window", "find_window", "list_windows", "window_exists", "minimize_window",
        "maximize_window", "close_window", "get_active_window", "move_window", "get_pixel_color",
        "screen_size", "screenshot", "find_image_on_screen", "wait_for_pixel_color", "clipboard_get",
        "clipboard_set", "run_process", "launch", "kill_process", "process_exists", "list_processes",
        "notify", "show_notification", "beep", "env_get", "file_read", "file_write", "file_exists",
        "dir_exists", "file_delete", "make_dir", "list_dir", "file_append_line", "csv_read", "csv_write",
        "http_get", "http_post", "url_encode",
        "abs", "floor", "ceil", "round", "sqrt", "pow", "min", "max", "sum", "avg",
        "sin", "cos", "tan", "clamp", "random", "random_int", "random_choice", "random_float",
        "upper", "lower", "trim", "ltrim", "rtrim", "split", "join", "replace", "contains",
        "starts_with", "ends_with", "substring", "char_at", "index_of", "repeat", "pad_left",
        "pad_right", "format_number", "to_string", "regex_match", "regex_match_all", "regex_replace",
        "regex_test", "push", "append", "pop", "insert", "remove", "remove_at", "sort", "reverse",
        "slice", "unique", "flatten", "zip_lists", "map", "filter", "reduce", "foreach", "keys",
        "values", "has_key", "delete_key", "merge", "json_parse", "json_stringify",
        
        "controller_connected", "controller_press", "controller_release", "controller_stick",
        "controller_trigger", "controller_dpad", "controller_vibrate",
        "get_pixel", "get_pixel_r", "get_pixel_g", "get_pixel_b", "get_pixel_block", "color_distance",
        "wait_for_color", "wait_for_color_gone", "color_in_region", "find_color", "find_color_region",
        "save_screenshot", "compare_images",
        "acos", "asin", "atan", "atan2", "sinh", "cosh", "tanh", "gcd", "lcm", "factorial",
        "is_prime", "lerp", "map_range", "median", "stdev",
        "reverse_str", "count_str", "title_case", "camel_case", "snake_case", "is_number",
        "is_alpha", "is_digit", "char_code", "char_from", "format", "escape_html", "unescape_html",
        "mask", "find", "find_index", "group_by", "chunk", "flatten_deep", "take", "drop",
        "rotate", "shuffle", "sample", "map_map", "map_filter", "invert", "pick", "to_pairs",
        "format_date", "today", "timestamp", "date_diff", "timer",
        "base64_encode", "base64_decode", "hex_encode", "hex_decode", "md5", "sha256", "uuid",
        "encode_url", "decode_url", "open_url", "open_file", "desktop_path", "documents_path",
        "downloads_path", "temp_path", "cpu_usage", "memory_info",
        "http_put", "http_delete", "http_download", "db_open", "db_query", "db_execute", "db_close",
    };

    public static IHighlightingDefinition Definition
    {
        get
        {
            if (_cached != null) return _cached;
            _cached = Build();
            return _cached;
        }
    }

    private static HighlightingColor Color(string hex, bool italic = false)
    {
        var c = new HighlightingColor
        {
            Foreground = new SimpleHighlightingBrush(Palette.Hex(hex)),
        };
        if (italic) c.FontStyle = FontStyles.Italic;
        return c;
    }

    private static HighlightingRule Rule(string pattern, HighlightingColor color)
    {
        return new HighlightingRule
        {
            Regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant),
            Color = color,
        };
    }

    private static IHighlightingDefinition Build()
    {
        var ruleSet = new HighlightingRuleSet();

        var kw = string.Join("|", Keywords);
        ruleSet.Rules.Add(Rule($@"\b(?:{kw})\b", Color(Palette.SynKeyword.ToStringHex())));

        ruleSet.Rules.Add(Rule(@"\b\d+(?:\.\d+)?\b", Color(Palette.SynNumber.ToStringHex())));

        
        var ordered = Builtins.OrderByDescending(b => b.Length).ToArray();
        var builtin = string.Join("|", ordered);
        ruleSet.Rules.Add(Rule($@"\b(?:{builtin})(?=\s*\()", Color(Palette.SynBuiltin.ToStringHex())));

        
        ruleSet.Rules.Add(Rule(@"\b[A-Za-z_][A-Za-z0-9_]*(?=\s*\()", Color(Palette.SynFunction.ToStringHex())));

        
        ruleSet.Rules.Add(Rule(@"(?<=\blet\s+)[A-Za-z_][A-Za-z0-9_]*", Color(Palette.SynVariable.ToStringHex())));

        
        ruleSet.Rules.Add(Rule(@"[+\-*/%=<>!]+", Color(Palette.SynOperator.ToStringHex())));

        
        ruleSet.Rules.Add(Rule(@"#.*$|//.*$", Color(Palette.SynComment.ToStringHex(), italic: true)));

        
        ruleSet.Rules.Add(Rule(@"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'", Color(Palette.SynString.ToStringHex())));

        return new FerroliteHighlightingDefinition(ruleSet);
    }
}


public class FerroliteHighlightingDefinition : IHighlightingDefinition
{
    public string Name { get; } = "Ferrolite";
    public HighlightingRuleSet MainRuleSet { get; }
    public IEnumerable<HighlightingColor> NamedHighlightingColors => Enumerable.Empty<HighlightingColor>();
    public System.Collections.ObjectModel.ReadOnlyCollection<HighlightingColor> Colors =>
        new(Array.Empty<HighlightingColor>());

    public FerroliteHighlightingDefinition(HighlightingRuleSet ruleSet)
    {
        MainRuleSet = ruleSet;
    }

    public HighlightingRuleSet? GetNamedRuleSet(string name) => null;
    public HighlightingColor? GetNamedColor(string name) => null;
    public System.Windows.Media.Brush? GetNamedBrush(string name) => null;
    public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();
}

internal static class ColorExt
{
    public static string ToStringHex(this System.Windows.Media.Color c) =>
        $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
