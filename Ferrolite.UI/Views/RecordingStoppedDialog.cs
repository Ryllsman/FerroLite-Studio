using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ferrolite.UI.Controls;

namespace Ferrolite.UI.Views;








public class RecordingStoppedDialog : Window
{
    private readonly RadioButton _once = new() { Content = "Insert once (no loop)", IsChecked = true, Foreground = Palette.Brush(Palette.FgDefault) };
    private readonly RadioButton _count = new() { Content = "Repeat a fixed number of times:", Foreground = Palette.Brush(Palette.FgDefault) };
    private readonly RadioButton _infinite = new() { Content = "Loop indefinitely (until the script is stopped)", Foreground = Palette.Brush(Palette.FgDefault) };
    private readonly TextBox _countBox = new() { Text = "5", Width = 70 };
    private readonly CheckBox _exact = new()
    {
        Content = "Save as a recording file + insert an exact playback call",
        ToolTip = "Instead of generating editable FerroScript for each action, saves the raw captured events to a .frr file and inserts a single play_recording(...) call that replays them at their exact original timestamps.",
        Foreground = Palette.Brush(Palette.FgDefault),
    };
    private readonly RadioButton _keys = new() { Content = "Individual key presses (default)", IsChecked = true, Foreground = Palette.Brush(Palette.FgDefault) };
    private readonly RadioButton _collapse = new() { Content = "Collapse fast typing into type_text() calls", Foreground = Palette.Brush(Palette.FgDefault) };
    private readonly Border _typingBox;

    
    public event Action? PlayRequested;
    
    public event Action? InsertRequested;

    public bool Loop { get; private set; }
    public int? LoopCount { get; private set; }
    public bool InsertAsExactCall => _exact.IsChecked == true;
    public bool CollapseTyping => _collapse.IsChecked == true;
    public bool Inserted { get; private set; }

    public RecordingStoppedDialog(int eventCount)
    {
        Width = 480;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var root = new StackPanel { Margin = new Thickness(14) };

        root.Children.Add(new TextBlock
        {
            Text = $"Captured {eventCount} events. Insert this recording into the editor as-is, or wrap it in a loop?",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Palette.Brush(Palette.FgDefault),
            Margin = new Thickness(0, 0, 0, 10),
        });

        root.Children.Add(GroupBorder(new[] { _once, CountRow(), _infinite }));
        root.Children.Add(_exact);
        root.Children.Add(new TextBlock
        {
            Text = "Typed text",
            Foreground = Palette.Brush(Palette.FgMuted),
            FontSize = 11,
            Margin = new Thickness(0, 10, 0, 4),
        });
        _typingBox = GroupBorder(new[] { _keys, _collapse });
        root.Children.Add(_typingBox);
        _exact.Checked += (_, _) => _typingBox.IsEnabled = false;
        _exact.Unchecked += (_, _) => _typingBox.IsEnabled = true;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0),
        };
        buttons.Children.Add(DialogButton("Play Exactly As Recorded", () => PlayRequested?.Invoke(), primary: false,
            "Replays the captured events directly, at their original timestamps, right now. Does not close this dialog."));
        buttons.Children.Add(DialogButton("Insert into Editor", () => { Collect(); Inserted = true; InsertRequested?.Invoke(); DialogResult = true; }, primary: true));
        buttons.Children.Add(DialogButton("Close", () => DialogResult = false, primary: false));
        root.Children.Add(buttons);

        DialogShell.Apply(this, "Recording Stopped", root, resizable: false);
    }

    private UIElement CountRow()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 2, 0, 2) };
        _countBox.IsEnabled = false;
        _count.Checked += (_, _) => _countBox.IsEnabled = true;
        _count.Unchecked += (_, _) => _countBox.IsEnabled = false;
        row.Children.Add(_countBox);
        return row;
    }

    private static Border GroupBorder(IEnumerable<UIElement> children)
    {
        var panel = new StackPanel();
        foreach (var c in children) panel.Children.Add(c);
        return new Border
        {
            Background = Palette.Brush(Palette.InputBg),
            BorderBrush = Palette.Brush(Palette.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 8),
            Child = panel,
        };
    }

    private static Button DialogButton(string text, Action click, bool primary, string? tip = null)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(6, 0, 0, 0),
            Background = Palette.Brush(primary ? Palette.AccentOrange : Palette.ButtonSecondaryBg),
            Foreground = Palette.Brush(primary ? Palette.EditorBg : Palette.FgDefault),
            BorderBrush = Palette.Brush(Palette.Border),
            ToolTip = tip,
        };
        b.Click += (_, _) => click();
        return b;
    }

    private void Collect()
    {
        if (_count.IsChecked == true && int.TryParse(_countBox.Text.Trim(), out int n) && n >= 1)
        {
            Loop = true;
            LoopCount = n;
        }
        else if (_infinite.IsChecked == true)
        {
            Loop = true;
            LoopCount = null;
        }
        else
        {
            Loop = false;
            LoopCount = null;
        }
    }
}
