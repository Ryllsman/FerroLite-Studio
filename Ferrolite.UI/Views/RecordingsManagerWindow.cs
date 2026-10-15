using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ferrolite.Core.Recorder;
using Ferrolite.UI.Controls;
using Ferrolite.UI.Services;

namespace Ferrolite.UI.Views;


public class RecordingsManagerWindow : Window
{
    private readonly ListBox _list;
    private readonly ObservableItems<RecordingInfo> _items = new();

    public List<RecordedEvent>? SelectedEvents { get; private set; }

    
    public event Action<List<RecordedEvent>>? PlayRequested;
    
    public event Action<string>? CodeRequested;

    public RecordingsManagerWindow()
    {
        Title = "Recordings Manager";
        Width = 620;
        Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MinWidth = 480;
        MinHeight = 340;

        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _list = new ListBox
        {
            Background = Palette.Brush(Palette.EditorBg),
            Foreground = Palette.Brush(Palette.FgDefault),
            BorderBrush = Palette.Brush(Palette.Border),
            FontSize = 12,
            ItemsSource = _items,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        _list.DisplayMemberPath = "Name";
        _list.SelectionChanged += (_, _) => { };
        _list.MouseDoubleClick += (_, _) => PlaySelected();
        _list.ContextMenu = BuildListMenu();
        root.Children.Add(_list);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(MakeBtn("Play", () => PlaySelected(), primary: true));
        buttons.Children.Add(MakeBtn("Insert Reference", () => InsertReferenceSelected()));
        buttons.Children.Add(MakeBtn("Rename", () => RenameSelected()));
        buttons.Children.Add(MakeBtn("Delete", () => DeleteSelected()));
        buttons.Children.Add(MakeBtn("Refresh", () => Refresh()));
        var close = MakeBtn("Close", () => Close());
        buttons.Children.Add(close);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);

        DialogShell.Apply(this, "Recordings Manager", root);
        Refresh();
    }

    private void Refresh()
    {
        _items.Clear();
        foreach (var r in AppStorage.ListRecordings())
        {
            _items.Add(new RecordingInfo
            {
                Path = r.Path,
                Name = $"{r.Name}  ·  {r.Count} events  ·  {r.Date:yyyy-MM-dd HH:mm}",
                Count = r.Count,
                Date = r.Date,
                Size = r.Size,
            });
        }
    }

    private RecordingInfo? Selected()
    {
        if (_list.SelectedItem is RecordingInfo r && File.Exists(r.Path)) return r;
        return null;
    }

    private void PlaySelected()
    {
        var rec = Selected();
        if (rec == null)
        {
            MessageBox.Show(this, "Select a recording first.", "Recordings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var events = AppStorage.LoadRecording(rec.Path);
            SelectedEvents = events;
            PlayRequested?.Invoke(events); 
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not load recording:\n{ex.Message}", "Recordings", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private ContextMenu BuildListMenu()
    {
        var menu = new ContextMenu();
        var play = new MenuItem { Header = "Play" };
        play.Click += (_, _) => PlaySelected();
        var decouple = new MenuItem { Header = "Decouple to code" };
        decouple.ToolTip = "Expands this recording into editable FerroScript lines (move/click/key calls) at the cursor.";
        decouple.Click += (_, _) => DecoupleSelected();
        var insertRef = new MenuItem { Header = "Insert play_recording(...) call" };
        insertRef.Click += (_, _) => InsertReferenceSelected();
        menu.Items.Add(play);
        menu.Items.Add(decouple);
        menu.Items.Add(insertRef);
        return menu;
    }

    
    private void DecoupleSelected()
    {
        var rec = Selected();
        if (rec == null)
        {
            MessageBox.Show(this, "Select a recording first.", "Recordings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var events = AppStorage.LoadRecording(rec.Path);
            string code = Transpiler.Transpile(events);
            CodeRequested?.Invoke(code);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not decouple recording:\n{ex.Message}", "Recordings", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    
    private void InsertReferenceSelected()
    {
        var rec = Selected();
        if (rec == null)
        {
            MessageBox.Show(this, "Select a recording first.", "Recordings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        CodeRequested?.Invoke($"play_recording(\"{rec.Name.Split('·')[0].Trim()}\")\n");
    }

    private void RenameSelected()
    {
        var rec = Selected();
        if (rec == null) return;
        var name = rec.Name.Split('·')[0].Trim();
        var dlg = new TextInputWindow("Rename recording", "New name (without .frr):", name)
        {
            Owner = this,
        };
        if (dlg.ShowDialog() != true) return;
        string newName = dlg.Value.Trim();
        if (newName.Length == 0) return;
        if (!newName.EndsWith(".frr", StringComparison.OrdinalIgnoreCase)) newName += ".frr";
        try
        {
            string dest = Path.Combine(AppStorage.RecordingsDir, newName);
            File.Move(rec.Path, dest, overwrite: true);
            Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Rename failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteSelected()
    {
        var rec = Selected();
        if (rec == null) return;
        var r = MessageBox.Show(this, $"Delete \"{rec.Name}\"?", "Recordings",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        try { File.Delete(rec.Path); Refresh(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Delete failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private Button MakeBtn(string text, Action click, bool primary = false)
    {
        var b = new Button
        {
            Content = text,
            FontSize = 12,
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 8, 0),
            Background = Palette.Brush(primary ? Palette.AccentOrange : Palette.ButtonSecondaryBg),
            Foreground = Palette.Brush(primary ? Palette.FgBright : Palette.FgDefault),
            BorderBrush = Palette.Brush(primary ? Palette.AccentOrange : Palette.Border),
            BorderThickness = new Thickness(primary ? 0 : 1),
            FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
        };
        b.Click += (_, _) => click();
        b.MouseEnter += (_, _) => b.Background = Palette.Brush(primary ? Palette.AccentLight : Palette.HoverBg);
        b.MouseLeave += (_, _) => b.Background = Palette.Brush(primary ? Palette.AccentOrange : Palette.ButtonSecondaryBg);
        return b;
    }

    private class ObservableItems<T> : System.Collections.ObjectModel.ObservableCollection<T> { }
}


public class TextInputWindow : Window
{
    public string Value { get; private set; } = "";

    public TextInputWindow(string title, string label, string initial = "")
    {
        Title = title;
        Width = 400;
        Height = 168;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var lbl = new TextBlock { Text = label, Foreground = Palette.Brush(Palette.FgDefault), Margin = new Thickness(0, 0, 0, 6) };
        var input = new TextBox
        {
            Text = initial,
            Background = Palette.Brush(Palette.InputBg),
            Foreground = Palette.Brush(Palette.FgDefault),
            BorderBrush = Palette.Brush(Palette.Border),
            Padding = new Thickness(4, 2, 4, 2),
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        void Btn(string t, RoutedEventHandler h, bool primary = false)
        {
            var b = new Button
            {
                Content = t,
                FontSize = 12,
                Padding = new Thickness(12, 3, 12, 3),
                Margin = new Thickness(0, 0, 8, 0),
                Background = Palette.Brush(primary ? Palette.AccentOrange : Palette.ButtonSecondaryBg),
                Foreground = Palette.Brush(primary ? Palette.FgBright : Palette.FgDefault),
                BorderBrush = Palette.Brush(primary ? Palette.AccentOrange : Palette.Border),
                BorderThickness = new Thickness(primary ? 0 : 1),
                FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
            };
            b.Click += h;
            row.Children.Add(b);
        }
        void Ok()
        {
            Value = input.Text;
            DialogResult = true;
            Close();
        }
        Btn("Cancel", (_, _) => Close());
        Btn("OK", (_, _) => Ok(), primary: true);
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Ok();
            if (e.Key == Key.Escape) Close();
        };
        root.Children.Add(lbl);
        root.Children.Add(input);
        Grid.SetRow(input, 1);
        root.Children.Add(row);
        Grid.SetRow(row, 2);
        DialogShell.Apply(this, title, root, resizable: false);
        input.Focus();
        input.SelectAll();
    }
}
