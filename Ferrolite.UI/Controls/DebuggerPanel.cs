using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Ferrolite.UI.Controls;

public class WatchRow
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}


public class DebuggerPanel : UserControl
{
    private readonly ObservableCollection<WatchRow> _variables = new();
    private readonly ListBox _callStack;
    private readonly DataGrid _grid;
    private readonly TextBlock _position;

    public event Action? ContinueClicked;
    public event Action? StepOverClicked;
    public event Action? StepIntoClicked;
    public event Action? StepOutClicked;
    public event Action? StopClicked;

    public DebuggerPanel()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        
        var header = new Border
        {
            Background = Palette.Brush(Palette.PanelHeaderBg),
            BorderBrush = Palette.Brush(Palette.BorderStrong),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 4, 8, 4),
        };
        var title = new TextBlock
        {
            Text = "DEBUGGER",
            Foreground = Palette.Brush(Palette.SynFunction),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
        };
        header.Child = title;
        root.Children.Add(header);

        
        var ctl = new StackPanel { Margin = new Thickness(8, 6, 8, 4) };
        _position = new TextBlock
        {
            Text = "—",
            Foreground = Palette.Brush(Palette.FgMuted),
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 6),
        };
        ctl.Children.Add(_position);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        void Add(string label, System.Windows.RoutedEventHandler handler)
        {
            var b = new Button
            {
                Content = label,
                FontSize = 11,
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 6, 0),
                Background = Palette.Brush(Palette.ButtonSecondaryBg),
                Foreground = Palette.Brush(Palette.FgDefault),
                BorderBrush = Palette.Brush(Palette.Border),
                BorderThickness = new Thickness(1),
            };
            b.Click += handler;
            b.MouseEnter += (_, _) => b.Background = Palette.Brush(Palette.HoverBg);
            b.MouseLeave += (_, _) => b.Background = Palette.Brush(Palette.ButtonSecondaryBg);
            buttons.Children.Add(b);
        }
        Add("Continue", (_, _) => ContinueClicked?.Invoke());
        Add("Step Over", (_, _) => StepOverClicked?.Invoke());
        Add("Step Into", (_, _) => StepIntoClicked?.Invoke());
        Add("Step Out", (_, _) => StepOutClicked?.Invoke());
        Add("Stop", (_, _) => StopClicked?.Invoke());
        ctl.Children.Add(buttons);
        Grid.SetRow(ctl, 1);
        root.Children.Add(ctl);

        var varsLabel = new TextBlock
        {
            Text = "VARIABLES",
            Foreground = Palette.Brush(Palette.FgMuted),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(10, 6, 0, 2),
        };
        Grid.SetRow(varsLabel, 2);
        root.Children.Add(varsLabel);

        _grid = new DataGrid
        {
            ItemsSource = _variables,
            AutoGenerateColumns = false,
            Background = Palette.Brush(Palette.SidebarBg),
            Foreground = Palette.Brush(Palette.FgDefault),
            BorderThickness = new Thickness(0),
            RowBackground = Palette.Brush(Palette.SidebarBg),
            AlternatingRowBackground = Palette.Brush(Palette.SidebarBg),
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            RowHeight = 20,
            FontSize = 11,
        };
        var headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, Palette.Brush(Palette.TabInactiveBg)));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, Palette.Brush(Palette.FgMuted)));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, Palette.Brush(Palette.Border)));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(6, 2, 6, 2)));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, 10.0));
        _grid.ColumnHeaderStyle = headerStyle;

        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(DataGridRow.BackgroundProperty, Palette.Brush(Palette.SidebarBg)));
        var selTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
        selTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, Palette.Brush(Palette.HoverBg)));
        selTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, Palette.Brush(Palette.FgBright)));
        rowStyle.Triggers.Add(selTrigger);
        _grid.RowStyle = rowStyle;

        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new System.Windows.Data.Binding("Name"),
            Width = 110,
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Value",
            Binding = new System.Windows.Data.Binding("Value"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
        });
        Grid.SetRow(_grid, 3);
        root.Children.Add(_grid);

        var stackLabel = new TextBlock
        {
            Text = "CALL STACK",
            Foreground = Palette.Brush(Palette.FgMuted),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(10, 8, 0, 2),
        };
        Grid.SetRow(stackLabel, 4);
        root.Children.Add(stackLabel);

        _callStack = new ListBox
        {
            Background = Palette.Brush(Palette.SidebarBg),
            Foreground = Palette.Brush(Palette.FgDefault),
            BorderThickness = new Thickness(0),
            FontFamily = Palette.CodeFont,
            FontSize = 11,
        };
        Grid.SetRow(_callStack, 5);
        root.Children.Add(_callStack);

        Content = root;
    }

    public void SetPosition(string text) => _position.Text = text;

    public void SetVariables(IEnumerable<(string Name, string Value)> vars)
    {
        _variables.Clear();
        foreach (var (n, v) in vars) _variables.Add(new WatchRow { Name = n, Value = v });
    }

    public void SetCallStack(IEnumerable<string> frames)
    {
        _callStack.Items.Clear();
        foreach (var f in frames) _callStack.Items.Add(f);
    }

    public void Clear()
    {
        _variables.Clear();
        _callStack.Items.Clear();
        _position.Text = "—";
    }
}
