using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ferrolite.UI.Controls;
using Ferrolite.UI.Services;

namespace Ferrolite.UI.Views;



public class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly TextBox _fontSize;
    private readonly CheckBox _autoIndent;
    private readonly CheckBox _confirmRun;
    private readonly TextBox _moveThrottle;
    private readonly ComboBox _moveMode;
    private readonly ComboBox _virtualPad;

    public SettingsWindow(Window owner, AppSettings settings)
    {
        Owner = owner;
        _settings = settings;
        Title = "Settings";
        Width = 430;
        Height = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var grid = new Grid { Margin = new Thickness(18) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var form = new StackPanel();
        _fontSize = AddNumberRow(form, "Editor font size (px):");
        _moveThrottle = AddNumberRow(form, "Mouse-move capture interval (s):");
        _autoIndent = new CheckBox { Content = "Enable auto-indentation", Foreground = Palette.Brush(Palette.FgDefault), Margin = new Thickness(0, 10, 0, 0) };
        _confirmRun = new CheckBox { Content = "Confirm before running a script", Foreground = Palette.Brush(Palette.FgDefault), Margin = new Thickness(0, 8, 0, 0) };
        form.Children.Add(_autoIndent);
        form.Children.Add(_confirmRun);

        var moveModeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        moveModeRow.Children.Add(new TextBlock
        {
            Text = "Record mouse as:",
            Foreground = Palette.Brush(Palette.FgDefault),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        });
        _moveMode = new ComboBox { Width = 260, FontSize = 12 };
        _moveMode.Items.Add("Absolute positions — move(x, y)");
        _moveMode.Items.Add("Relative raw deltas — move_relative(dx, dy)");
        moveModeRow.Children.Add(_moveMode);
        form.Children.Add(moveModeRow);

        var padRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        padRow.Children.Add(new TextBlock
        {
            Text = "Virtual controller:",
            Foreground = Palette.Brush(Palette.FgDefault),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        });
        _virtualPad = new ComboBox { Width = 260, FontSize = 12 };
        _virtualPad.Items.Add("Xbox 360 — works everywhere (recommended)");
        _virtualPad.Items.Add("DualShock 4 — PlayStation-prompt games");
        padRow.Children.Add(_virtualPad);
        form.Children.Add(padRow);

        grid.Children.Add(form);

        var note = new TextBlock
        {
            Text = "Global recording captures mouse, keyboard and gamepad input while running.\n" +
                   "Hotkeys: Ctrl+Shift+G = Run · Ctrl+Shift+R = Record · Ctrl+Shift+Backspace = Stop\n" +
                   "Debug keys: F5 Continue · F10 Step Over · F11 Step Into · Shift+F11 Step Out · F9 Breakpoint",
            Foreground = Palette.Brush(Palette.FgMuted),
            FontSize = 11,
            Margin = new Thickness(0, 14, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(note, 1);
        grid.Children.Add(note);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var save = Btn("Save", () => SaveAndClose(), primary: true);
        var cancel = Btn("Cancel", Close);
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        Grid.SetRow(buttons, 4);
        grid.Children.Add(buttons);

        DialogShell.Apply(this, "Settings", grid, resizable: false);
        Load();
    }

    private TextBox AddNumberRow(Panel parent, string label)
    {
        var row = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        var lbl = new TextBlock { Text = label, Foreground = Palette.Brush(Palette.FgDefault), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var tb = new TextBox
        {
            Width = 90,
            TextAlignment = TextAlignment.Right,
            Background = Palette.Brush(Palette.InputBg),
            Foreground = Palette.Brush(Palette.FgDefault),
            BorderBrush = Palette.Brush(Palette.Border),
            FontSize = 12,
            Padding = new Thickness(4, 2, 4, 2),
        };
        DockPanel.SetDock(tb, Dock.Right);
        row.Children.Add(lbl);
        row.Children.Add(tb);
        parent.Children.Add(row);
        return tb;
    }

    private static Button Btn(string text, Action action, bool primary = false)
    {
        var b = new Button
        {
            Content = text,
            FontSize = 12,
            Padding = new Thickness(14, 4, 14, 4),
            Margin = new Thickness(0, 0, 8, 0),
            Background = Palette.Brush(primary ? Palette.AccentOrange : Palette.ButtonSecondaryBg),
            Foreground = Palette.Brush(primary ? Palette.FgBright : Palette.FgDefault),
            BorderBrush = Palette.Brush(primary ? Palette.AccentOrange : Palette.Border),
            BorderThickness = new Thickness(primary ? 0 : 1),
            FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
        };
        b.Click += (_, _) => action();
        b.MouseEnter += (_, _) => b.Background = Palette.Brush(primary ? Palette.AccentLight : Palette.HoverBg);
        b.MouseLeave += (_, _) => b.Background = Palette.Brush(primary ? Palette.AccentOrange : Palette.ButtonSecondaryBg);
        return b;
    }

    private void Load()
    {
        _fontSize.Text = _settings.FontSize.ToString("0");
        _moveThrottle.Text = _settings.MoveThrottle.ToString("0.00");
        _autoIndent.IsChecked = _settings.AutoIndent;
        _confirmRun.IsChecked = _settings.ConfirmOnRun;
        _moveMode.SelectedIndex = string.Equals(_settings.CaptureMode, "relative", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _virtualPad.SelectedIndex = string.Equals(_settings.VirtualController, "ds4", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private void SaveAndClose()
    {
        if (double.TryParse(_fontSize.Text, out double fs) && fs >= 8 && fs <= 32)
            _settings.FontSize = fs;
        if (double.TryParse(_moveThrottle.Text, out double mt) && mt >= 0.01 && mt <= 1.0)
            _settings.MoveThrottle = mt;
        _settings.AutoIndent = _autoIndent.IsChecked == true;
        _settings.ConfirmOnRun = _confirmRun.IsChecked == true;
        _settings.CaptureMode = _moveMode.SelectedIndex == 1 ? "relative" : "absolute";
        _settings.VirtualController = _virtualPad.SelectedIndex == 1 ? "ds4" : "x360";
        
        
        if (!AppStorage.SaveSettings(_settings))
        {
            MessageBox.Show(this,
                "Your settings could not be saved:\n\n" + (AppStorage.SettingsSaveError ?? "unknown error"),
                "Settings not saved", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
        Close();
    }
}
