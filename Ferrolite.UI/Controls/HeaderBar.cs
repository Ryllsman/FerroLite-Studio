using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ferrolite.UI.Controls;






public class HeaderBar : UserControl
{
    private readonly Button _btnRun;
    private readonly Button _btnPause;
    private readonly Button _btnStop;
    private readonly Button _btnDebug;
    private readonly Button _btnRecord;
    private readonly Button _btnStopRecording;

    public event Action? NewClicked;
    public event Action? OpenClicked;
    public event Action? SaveClicked;
    public event Action? RunClicked;
    public event Action? StopClicked;
    public event Action? PauseClicked;
    public event Action? DebugClicked;
    public event Action? RecordClicked;
    public event Action? StopRecordingClicked;
    public event Action? ExportClicked;
    public event Action? RecordingsClicked;
    public event Action? ReferenceClicked;
    public event Action? SettingsClicked;
    public event Action<double>? SpeedChanged;

    public HeaderBar()
    {
        MinHeight = 48;
        var root = new DockPanel { Background = Palette.Brush(Palette.ToolbarBg) };
        var border = new Border
        {
            BorderBrush = Palette.Brush(Palette.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        border.Child = root;

        
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        DockPanel.SetDock(right, Dock.Right);
        root.Children.Add(right);
        right.Children.Add(Flat("Export", () => ExportClicked?.Invoke(), "Save a copy of the active script"));
        right.Children.Add(Flat("Recordings", () => RecordingsClicked?.Invoke(), "Manage saved recordings"));
        right.Children.Add(Flat("Reference", () => ReferenceClicked?.Invoke(), "Language reference (F1)"));
        right.Children.Add(Flat("Settings", () => SettingsClicked?.Invoke(), "Editor, execution and appearance settings"));

        
        var strip = new WrapPanel
        {
            Margin = new Thickness(10, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        root.Children.Add(strip);

        
        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var title = new TextBlock
        {
            Text = "Ferrolite",
            Foreground = Palette.Brush(Palette.AccentOrange),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var subtitle = new TextBlock
        {
            Text = "Macro Automation Studio",
            Foreground = Palette.Brush(Palette.FgMuted),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        brand.Children.Add(title);
        brand.Children.Add(subtitle);
        strip.Children.Add(brand);
        strip.Children.Add(Space(14));
        strip.Children.Add(VLine());
        strip.Children.Add(Space(8));

        
        strip.Children.Add(Flat("New", () => NewClicked?.Invoke(), "New script (Ctrl+N)"));
        strip.Children.Add(Flat("Open", () => OpenClicked?.Invoke(), "Open script (Ctrl+O)"));
        strip.Children.Add(Flat("Save", () => SaveClicked?.Invoke(), "Save script (Ctrl+S)"));
        strip.Children.Add(Space(10));
        strip.Children.Add(VLine());
        strip.Children.Add(Space(8));

        
        strip.Children.Add(SectionLabel("SCRIPT"));
        _btnRun = Action("▶ Run", () => RunClicked?.Invoke(), RunStyle.Green, "Run script (Ctrl+Shift+G)");
        _btnPause = Flat("Pause", () => PauseClicked?.Invoke(), "Pause / resume the running script");
        _btnPause.IsEnabled = false;
        _btnStop = Flat("Stop", () => StopClicked?.Invoke(), "Stop the running script (Ctrl+Shift+Backspace)");
        _btnStop.IsEnabled = false;
        _btnDebug = Flat("Debug", () => DebugClicked?.Invoke(), "Start debugging (F5)");
        _btnDebug.IsEnabled = true;
        strip.Children.Add(_btnRun);
        strip.Children.Add(_btnPause);
        strip.Children.Add(_btnStop);
        strip.Children.Add(_btnDebug);

        strip.Children.Add(Space(8));
        var speedLabel = new TextBlock
        {
            Text = "Speed:",
            Foreground = Palette.Brush(Palette.FgMuted),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0),
        };
        strip.Children.Add(speedLabel);
        var speedCombo = new ComboBox
        {
            Width = 62,
            Height = 28,
            FontSize = 12,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        foreach (var s in new[] { "0.25x", "0.5x", "1x", "2x", "4x" })
            speedCombo.Items.Add(s);
        speedCombo.SelectedIndex = 2;
        speedCombo.SelectionChanged += (_, _) =>
        {
            if (speedCombo.SelectedItem is string sel)
                SpeedChanged?.Invoke(double.Parse(sel.Replace("x", "")));
        };
        strip.Children.Add(speedCombo);
        strip.Children.Add(Space(10));
        strip.Children.Add(VLine());
        strip.Children.Add(Space(8));

        
        strip.Children.Add(SectionLabel("RECORD", accent: true));
        _btnRecord = Action("● Record", () => RecordClicked?.Invoke(), RunStyle.Record, "Record mouse/keyboard/controller (Ctrl+Shift+R)");
        _btnStopRecording = Flat("Stop", () => StopRecordingClicked?.Invoke(), "Stop recording");
        _btnStopRecording.IsEnabled = false;
        strip.Children.Add(_btnRecord);
        strip.Children.Add(_btnStopRecording);

        Content = border;
    }

    

    private enum RunStyle { None, Green, Record }

    private static Button Flat(string text, Action onClick, string? tip = null)
    {
        var b = new Button
        {
            Content = text,
            MinHeight = 28,
            Padding = new Thickness(10, 3, 10, 3),
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Foreground = Palette.Brush(Palette.FgDefault),
            BorderThickness = new Thickness(0),
            ToolTip = tip,
        };
        b.Click += (_, _) => onClick();
        b.MouseEnter += (_, _) => { if (b.IsEnabled) b.Background = Palette.Brush(Palette.HoverBg); };
        b.MouseLeave += (_, _) => { if (b.IsEnabled) b.Background = Brushes.Transparent; };
        b.IsEnabledChanged += (_, _) =>
        {
            if (!b.IsEnabled) b.Background = Brushes.Transparent;
        };
        return b;
    }

    private static Button Action(string text, Action onClick, RunStyle style, string? tip = null)
    {
        bool green = style == RunStyle.Green;
        bool record = style == RunStyle.Record;
        var b = new Button
        {
            Content = text,
            MinHeight = 28,
            Padding = new Thickness(12, 3, 12, 3),
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            ToolTip = tip,
        };
        ApplyAction(b, green, record, enabled: true);
        b.Click += (_, _) => onClick();
        b.MouseEnter += (_, _) =>
        {
            if (!b.IsEnabled) return;
            b.Background = green
                ? Palette.Brush(Palette.RunGreenHover)
                : Palette.Brush(Palette.FromArgb(0x22, 0xE5, 0x48, 0x4D)); 
        };
        b.MouseLeave += (_, _) => { if (!b.IsEnabled) return; ApplyAction(b, green, record, enabled: true); };
        b.IsEnabledChanged += (_, _) => ApplyAction(b, green, record, b.IsEnabled);
        return b;
    }

    private static void ApplyAction(Button b, bool green, bool record, bool enabled)
    {
        if (!enabled)
        {
            b.Background = Brushes.Transparent;
            b.Foreground = Palette.Brush(Palette.FgMuted);
            b.BorderThickness = new Thickness(0);
            return;
        }
        if (green)
        {
            b.Background = Palette.Brush(Palette.RunGreen);
            b.Foreground = Palette.Brush(Palette.FgBright);
            b.BorderThickness = new Thickness(0);
        }
        else if (record)
        {
            b.Background = Brushes.Transparent;
            b.Foreground = Palette.Brush(Palette.Error);
            b.BorderBrush = Palette.Brush(Palette.Error);
            b.BorderThickness = new Thickness(1);
        }
    }

    private static UIElement Space(double px) => new Border { Width = px };

    private static UIElement VLine()
    {
        return new Border
        {
            Width = 1,
            Height = 22,
            Background = Palette.Brush(Palette.Border),
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static TextBlock SectionLabel(string text, bool accent = false) => new()
    {
        Text = text,
        Foreground = Palette.Brush(accent ? Palette.AccentOrange : Palette.FgMuted),
        FontSize = 10,
        FontWeight = FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 0),
    };

    

    public void SetRunning(bool running)
    {
        _btnRun.IsEnabled = !running;
        _btnDebug.IsEnabled = !running;
        _btnStop.IsEnabled = running;
        _btnPause.IsEnabled = running;
        if (running)
        {
            _btnStop.Background = Palette.Brush(Palette.StopRed);
            _btnStop.Foreground = Palette.Brush(Palette.FgBright);
        }
    }

    public void SetPaused(bool paused)
    {
        _btnPause.Content = paused ? "Resume" : "Pause";
    }

    public void SetRecording(bool recording)
    {
        _btnRecord.IsEnabled = !recording;
        _btnStopRecording.IsEnabled = recording;
        if (recording)
        {
            _btnRecord.Background = Palette.Brush(Palette.Error);
            _btnRecord.Foreground = Palette.Brush(Palette.FgBright);
            _btnRecord.BorderThickness = new Thickness(0);
        }
        else
        {
            _btnRecord.Background = Brushes.Transparent;
            _btnRecord.Foreground = Palette.Brush(Palette.Error);
            _btnRecord.BorderBrush = Palette.Brush(Palette.Error);
            _btnRecord.BorderThickness = new Thickness(1);
        }
    }

}
