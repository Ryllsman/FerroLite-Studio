using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ferrolite.UI.Controls;

public class TimelineItem
{
    public bool Included { get; set; } = true;
    public string Text { get; set; } = "";
    public double Timestamp { get; set; }
    public int Index { get; set; } = -1;
}


public class TimelinePanel : UserControl
{
    private readonly ObservableCollection<TimelineItem> _items = new();
    private readonly ListBox _list;
    private readonly ComboBox _filter;
    private readonly TextBlock _count;
    private List<TimelineItem> _all = new();

    public event Action? ClearClicked;


    public TimelinePanel()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Border
        {
            Background = Palette.Brush(Palette.PanelHeaderBg),
            BorderBrush = Palette.Brush(Palette.BorderStrong),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 4, 8, 4),
        };
        var hGrid = new Grid();
        hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock
        {
            Text = "TIMELINE",
            Foreground = Palette.Brush(Palette.Error),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _count = new TextBlock
        {
            Foreground = Palette.Brush(Palette.FgMuted),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        var hStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hStack.Children.Add(title);
        hStack.Children.Add(_count);
        hGrid.Children.Add(hStack);
        header.Child = hGrid;
        root.Children.Add(header);

        
        var filterRow = new DockPanel { Margin = new Thickness(8, 6, 8, 4) };
        _filter = new ComboBox
        {
            Width = 110,
            FontSize = 12,
            Background = Palette.Brush(Palette.InputBg),
            Foreground = Palette.Brush(Palette.FgDefault),
            BorderBrush = Palette.Brush(Palette.Border),
        };
        _filter.Items.Add("All events");
        _filter.Items.Add("Mouse only");
        _filter.Items.Add("Keyboard only");
        _filter.Items.Add("Controller only");
        _filter.SelectedIndex = 0;
        _filter.SelectionChanged += (_, _) => ApplyFilter();
        var lbl = new TextBlock
        {
            Text = "Show:",
            Foreground = Palette.Brush(Palette.FgMuted),
            FontSize = 11,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(lbl, Dock.Left);
        filterRow.Children.Add(lbl);
        DockPanel.SetDock(_filter, Dock.Right);
        filterRow.Children.Add(_filter);
        Grid.SetRow(filterRow, 1);
        root.Children.Add(filterRow);

        _list = new ListBox
        {
            Background = Palette.Brush(Palette.SidebarBg),
            Foreground = Palette.Brush(Palette.FgDefault),
            BorderThickness = new Thickness(0),
            FontFamily = Palette.CodeFont,
            FontSize = 11,
            ItemsSource = _items,
        };
        _list.ItemTemplate = BuildItemTemplate();
        Grid.SetRow(_list, 2);
        root.Children.Add(_list);

        
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 6, 8, 8),
        };
        var clear = new Button
        {
            Content = "Clear",
            FontSize = 11,
            Padding = new Thickness(8, 2, 8, 2),
            Background = Palette.Brush(Palette.ButtonSecondaryBg),
            Foreground = Palette.Brush(Palette.FgDefault),
            BorderBrush = Palette.Brush(Palette.Border),
            BorderThickness = new Thickness(1),
            ToolTip = "Discard the current timeline events",
        };
        clear.Click += (_, _) => ClearClicked?.Invoke();
        clear.MouseEnter += (_, _) => clear.Background = Palette.Brush(Palette.HoverBg);
        clear.MouseLeave += (_, _) => clear.Background = Palette.Brush(Palette.ButtonSecondaryBg);
        actions.Children.Add(clear);
        Grid.SetRow(actions, 3);
        root.Children.Add(actions);

        Content = root;
    }

    private static DataTemplate BuildItemTemplate()
    {
        var stack = new FrameworkElementFactory(typeof(StackPanel));
        stack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var check = new FrameworkElementFactory(typeof(CheckBox));
        check.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("Included"));
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.TextProperty, new System.Windows.Data.Binding("Text"));
        text.SetValue(TextBlock.ForegroundProperty, Palette.Brush(Palette.FgDefault));
        text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        stack.AppendChild(check);
        stack.AppendChild(text);
        return new DataTemplate { VisualTree = stack };
    }

    public void SetItems(List<TimelineItem> items, bool live = false)
    {
        _all = items;
        _items.Clear();
        foreach (var it in items) _items.Add(it);
        if (!live) _count.Text = items.Count == 0 ? "" : $"{items.Count} events";
        else _count.Text = $"{items.Count}";
        ApplyFilter();
    }

    public void AppendLive(TimelineItem item)
    {
        _all.Add(item);
        if (PassesFilter(item))
        {
            _items.Add(item);
            _list.ScrollIntoView(item); 
        }
        _count.Text = _all.Count.ToString();
    }

    private bool PassesFilter(TimelineItem it)
    {
        string sel = _filter.SelectedItem as string ?? "All events";
        return sel switch
        {
            "Mouse only" => it.Text.StartsWith("MOUSE"),
            "Keyboard only" => it.Text.StartsWith("KEY"),
            "Controller only" => it.Text.StartsWith("PAD"),
            _ => true,
        };
    }

    private void ApplyFilter()
    {
        var visible = _all.Where(PassesFilter).ToList();
        _items.Clear();
        foreach (var it in visible) _items.Add(it);
    }

    public List<TimelineItem> IncludedItems() => _all.Where(i => i.Included).ToList();
    public void ClearList()
    {
        _all.Clear();
        _items.Clear();
        _count.Text = "";
    }

    
    public void SetLiveCount(int count)
    {
        _count.Text = count == 0 ? "" : $"{count} events";
    }
}
