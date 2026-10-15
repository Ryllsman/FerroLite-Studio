using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ferrolite.UI.Controls;

namespace Ferrolite.UI.Views;

public class RefEntry
{
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public string Signature { get; set; } = "";
    public string Description { get; set; } = "";
    public string Example { get; set; } = "";
}



public class LanguageReferenceWindow : Window
{
    private readonly ListBox _list;
    private readonly TextBlock _detail;
    private readonly TextBlock _sig;
    private readonly TextBlock _example;
    private readonly TextBox _search;

    public LanguageReferenceWindow()
    {
        Title = "Ferrolite Language Reference";
        Width = 980;
        Height = 660;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        
        var left = new DockPanel { Margin = new Thickness(0, 0, 8, 0) };
        _search = new TextBox
        {
            Height = 26,
            Background = Palette.Brush(Palette.InputBg),
            Foreground = Palette.Brush(Palette.FgDefault),
            BorderBrush = Palette.Brush(Palette.Border),
            FontSize = 12,
            Padding = new Thickness(6, 3, 6, 3),
        };
        _search.TextChanged += (_, _) => ApplyFilter();
        DockPanel.SetDock(_search, Dock.Top);
        _list = new ListBox
        {
            Background = Palette.Brush(Palette.EditorBg),
            Foreground = Palette.Brush(Palette.FgDefault),
            BorderBrush = Palette.Brush(Palette.Border),
            FontFamily = Palette.CodeFont,
            FontSize = 12,
            Margin = new Thickness(0, 6, 0, 0),
            DisplayMemberPath = "Name",
        };
        _list.SelectionChanged += (_, _) => ShowDetail(_list.SelectedItem as RefEntry);
        left.Children.Add(_search);
        left.Children.Add(_list);
        root.Children.Add(left);

        
        var right = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var detail = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
        _detail = new TextBlock { Text = "Select a function…", Foreground = Palette.Brush(Palette.FgMuted), FontSize = 12, TextWrapping = TextWrapping.Wrap };
        _sig = new TextBlock { Foreground = Palette.Brush(Palette.SynFunction), FontFamily = Palette.CodeFont, FontSize = 13, TextWrapping = TextWrapping.Wrap };
        var exLabel = new TextBlock { Text = "Example", Foreground = Palette.Brush(Palette.FgMuted), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 4) };
        _example = new TextBlock { Foreground = Palette.Brush(Palette.SynString), FontFamily = Palette.CodeFont, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        detail.Children.Add(_sig);
        detail.Children.Add(_detail);
        detail.Children.Add(exLabel);
        detail.Children.Add(_example);
        right.Content = detail;
        Grid.SetColumn(right, 1);
        root.Children.Add(right);

        DialogShell.Apply(this, "Ferrolite Language Reference", root);
        _all = BuildCatalog();
        ApplyFilter();
        if (_all.Count > 0) _list.SelectedIndex = 0;
    }

    private List<RefEntry> _all = new();

    private void ApplyFilter()
    {
        string q = (_search.Text ?? "").Trim().ToLowerInvariant();
        var items = q.Length == 0
            ? _all
            : _all.Where(e => e.Name.ToLowerInvariant().Contains(q)
                              || e.Category.ToLowerInvariant().Contains(q)
                              || e.Description.ToLowerInvariant().Contains(q)).ToList();
        _list.Items.Clear();
        foreach (var it in items) _list.Items.Add(it);
        if (items.Count > 0) _list.SelectedIndex = 0;
    }

    private void ShowDetail(RefEntry? e)
    {
        if (e == null) { _detail.Text = "Select a function…"; _sig.Text = ""; _example.Text = ""; return; }
        _sig.Text = e.Signature;
        _detail.Text = $"[{e.Category}]\n\n{e.Description}";
        _example.Text = e.Example;
        _detail.Text = $"[{e.Category}]\n{e.Description}";
    }

    private static List<RefEntry> BuildCatalog()
    {
        var list = new List<RefEntry>();
        void Add(string cat, string name, string sig, string desc, string ex = "") =>
            list.Add(new RefEntry { Category = cat, Name = name, Signature = sig, Description = desc, Example = ex });

        Add("Basics", "print", "print(value, ...)", "Write values to the terminal.", "print(\"Hello\", 42)");
        Add("Basics", "let", "let name = value", "Declare a variable.", "let x = 10\nlet msg = \"hi\"");
        Add("Basics", "typeof", "typeof(value)", "Returns the type name of a value.", "typeof(42)        # \"number\"");
        Add("Basics", "len", "len(value)", "Length of a string, list or map.", "len(\"abc\")        # 3");
        Add("Control flow", "if / else", "if cond { ... } else { ... }", "Conditional execution.", "if x > 5 { print(\"big\") } else { print(\"small\") }");
        Add("Control flow", "while", "while cond { ... }", "Loop while a condition holds.", "while x < 10 { x = x + 1 }");
        Add("Control flow", "for", "for v in list { ... }", "Iterate a list, string or map.", "for n in [1,2,3] { print(n) }");
        Add("Control flow", "func", "func name(a, b) { ... }", "Declare a function (closures supported).", "func add(a, b) { return a + b }");
        Add("Control flow", "try/catch", "try { ... } catch e { ... }", "Catch runtime errors.", "try { risky() } catch e { print(e) }");
        Add("Control flow", "range", "range(n)", "Build a list of numbers 0..n-1.", "range(5)");
        Add("Math", "min / max / clamp", "min(a, b, ...)", "Minimum / maximum / clamped value.", "clamp(x, 0, 100)");
        Add("Math", "abs / round / floor / ceil / sqrt / pow", "abs(x)", "Basic math helpers.", "pow(2, 10)");
        Add("Math", "random_int / random_choice / random_float", "random_int(lo, hi)", "Random values and picks.", "random_int(1, 6)");
        Add("Math", "sum / avg / median / stdev", "sum(list)", "Aggregate helpers over lists of numbers.", "avg([1, 2, 3])");
        Add("Math", "gcd / lcm / factorial / is_prime", "gcd(a, b)", "Number theory helpers.", "gcd(12, 18)");
        Add("Math", "lerp / map_range", "lerp(a, b, t)", "Interpolation and range mapping.", "map_range(x, 0, 1, 0, 100)");
        Add("Strings", "upper / lower / trim", "upper(s)", "Case and whitespace helpers.", "upper(\"hi\")");
        Add("Strings", "split / join", "split(s, sep)", "Split a string into a list and join back.", "join(split(\"a,b\", \",\"), \"-\")");
        Add("Strings", "substring / char_at / index_of / contains", "contains(s, sub)", "Sub-string helpers.", "index_of(\"hello\", \"ll\")");
        Add("Strings", "replace / repeat / reverse_str / pad_left / mask", "replace(s, from, to)", "Transformation helpers.", "mask(\"123456\", \"*\")");
        Add("Strings", "regex_test / regex_match / regex_replace", "regex_test(s, pattern)", "Regular expression helpers.", "regex_test(\"abc123\", \"\\\\d+\")");
        Add("Lists", "push / pop / insert / remove_at", "push(list, value)", "In-place list mutation.", "push(items, 9)");
        Add("Lists", "sort / reverse / slice / unique / flatten", "sort(list)", "Ordering and slicing.", "slice(items, 1, 3)");
        Add("Lists", "map / filter / reduce / foreach", "map(list, fn)", "Higher-order list functions.", "map([1,2,3], func(x) => x * 2)");
        Add("Lists", "find / chunk / shuffle / rotate / sample", "find(list, predicate)", "Advanced list helpers.", "chunk([1,2,3,4], 2)");
        Add("Maps", "keys / values / has_key / delete_key / merge", "has_key(map, k)", "Map helpers.", "keys({ a: 1, b: 2 })");
        Add("Mouse", "move", "move(x, y, duration?)", "Move the mouse to screen coordinates.", "move(500, 400, 0.3)");
        Add("Mouse", "click / double_click / right_click", "click(x?, y?, button?)", "Click at a position (or current).", "click(500, 400, \"left\")");
        Add("Mouse", "drag / scroll / mouse_down / mouse_up", "drag(x1, y1, x2, y2)", "Drag and scroll actions.", "drag(100, 100, 300, 300, 0.5)");
        Add("Mouse", "is_mouse_down", "is_mouse_down(button)", "Returns true while a mouse button is held down. Valid buttons: \"left\", \"right\", \"middle\", \"mb4\" (first side button / XButton1), \"mb5\" (second side button / XButton2), plus aliases \"mouse4\", \"mouse5\", \"xbutton1\", \"xbutton2\", \"side_button_near\", \"side_button_far\".", "if is_mouse_down(\"mb5\") { click() }");
        Add("Mouse", "get_mouse_pos", "get_mouse_pos()", "Returns { x, y } of the cursor.", "let p = get_mouse_pos()");
        Add("Keyboard", "type_text / key_press / key_down / key_up", "type_text(text, interval?)", "Type text or tap keys.", "type_text(\"Hello!\")");
        Add("Keyboard", "hotkey / key_combo", "hotkey([\"ctrl\", \"c\"])", "Press a key combination.", "hotkey([\"ctrl\", \"c\"])");
        Add("Windows", "focus_window / find_window / list_windows", "focus_window(title)", "Manage windows by title.", "focus_window(\"Notepad\")");
        Add("Windows", "get_active_window / minimize_window / close_window", "get_active_window()", "Query the active window.", "get_active_window()");
        Add("Screen", "get_pixel_color", "get_pixel_color(x, y)", "Color of one pixel as {r,g,b}.", "get_pixel_color(10, 10)");
        Add("Screen", "find_color / wait_for_color / color_in_region", "find_color(color, region?)", "Locate a color on screen.", "find_color({r: 255, g: 0, b: 0})");
        Add("Screen", "screen_size / screenshot", "screen_size()", "Screen dimensions and captures.", "screen_size()");
        Add("Controller", "controller_connected", "controller_connected(index)", "Is a gamepad connected? (XInput).", "controller_connected(0)");
        Add("Controller", "controller_press / controller_release", "controller_press(index, button)", "Press/release a gamepad button.", "controller_press(0, \"gamepad_a\")");
        Add("Controller", "controller_stick / controller_trigger / controller_dpad", "controller_stick(index, stick, x, y)", "Move sticks, triggers and D-pad.", "controller_stick(0, \"left_stick\", 0, -1)");
        Add("Clipboard", "clipboard_get / clipboard_set", "clipboard_set(text)", "Read/write the clipboard.", "clipboard_set(\"copied!\")");
        Add("Files", "file_read / file_write / file_exists", "file_write(path, text)", "Read and write text files.", "file_write(\"out.txt\", \"data\")");
        Add("Files", "list_dir / make_dir / file_delete / csv_read", "list_dir(path)", "Directory helpers and CSV.", "list_dir(\".\")");
        Add("HTTP", "http_get / http_post / http_put / http_delete", "http_get(url)", "Web requests.", "http_get(\"https://api.example.com/data\")");
        Add("HTTP", "http_download", "http_download(url, path)", "Download a file.", "http_download(url, \"file.bin\")");
        Add("Database", "db_open / db_query / db_execute / db_close", "db_open(path)", "Open an SQLite database.", "db_open(\"app.db\")");
        Add("Encoding", "json_parse / json_stringify", "json_stringify(value)", "JSON conversion.", "json_parse(\"{\\\"a\\\": 1}\")");
        Add("Encoding", "base64_encode / hex_encode / md5 / sha256 / uuid", "md5(text)", "Encoding and hashing helpers.", "uuid()");
        Add("System", "notify / beep", "notify(title, message)", "Show a desktop notification.", "notify(\"Done\", \"Script finished\")");
        Add("System", "open_url / open_file", "open_url(url)", "Open a URL or file with the shell.", "open_url(\"https://ferrolite.dev\")");
        Add("Time", "wait / sleep / delay", "wait(seconds)", "Pause the script (respects speed + stop).", "wait(0.5)");
        Add("Time", "now / now_ms / format_date / date_diff", "now()", "Clock helpers.", "format_date(now(), \"%Y-%m-%d\")");

        
        Add("Friendly aliases", "reverse_text / reverse_str", "reverse_text(text)", "Short friendly alias for reverse_str — both names work.", "reverse_text(\"abc\")   # \"cba\"");
        Add("Friendly aliases", "ord / char_code", "ord(text)", "Character to its number code.", "ord(\"A\")   # 65");
        Add("Friendly aliases", "chr / char_from", "chr(number)", "Number code back to a character.", "chr(65)   # \"A\"");
        Add("Friendly aliases", "count_text / count_str", "count_text(text, part)", "How many times a piece of text appears.", "count_text(\"banana\", \"a\")   # 3");
        Add("Friendly aliases", "contains_text / contains_str", "contains_text(text, part)", "Does the text contain a piece?", "contains_text(\"hello\", \"ell\")   # true");
        Add("Friendly aliases", "map_values / map_map", "map_values(map, fn)", "Run a function over every value in a map.", "map_values({a: 1}, func(v) => v * 2)");
        Add("Friendly aliases", "filter_map / map_filter", "filter_map(map, fn)", "Keep only map entries whose value passes a test.", "filter_map({a: 1, b: 5}, func(v) => v > 3)");
        Add("Friendly aliases", "entries / to_pairs", "entries(map)", "Map as a list of [key, value] pairs.", "entries({a: 1})");
        Add("Friendly aliases", "remove_key / delete_key", "remove_key(map, key)", "Remove one key from a map.", "remove_key({a: 1, b: 2}, \"a\")");
        return list;
    }
}
