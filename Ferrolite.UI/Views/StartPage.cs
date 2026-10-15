using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ferrolite.UI.Controls;
using Ferrolite.UI.Services;

namespace Ferrolite.UI.Views;





public class StartPage : UserControl
{
    public event Action? NewClicked;
    public event Action? OpenClicked;
    public event Action<string>? FileChosen;   

    private TextBlock? _recentHeader;
    private StackPanel? _recentList;

    public StartPage()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = Palette.Brush(Palette.EditorBg),
        };
        Content = scroll;

        var outer = new StackPanel
        {
            MaxWidth = 860,
            Margin = new Thickness(0, 40, 0, 30),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        scroll.Content = outer;

        
        var mark = BrandMark.Create(72);
        mark.HorizontalAlignment = HorizontalAlignment.Center;
        mark.Margin = new Thickness(0, 0, 0, 14);
        outer.Children.Add(mark);

        var title = new TextBlock
        {
            Text = "FerroLite Studio",
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Foreground = Palette.Brush(Palette.FgBright),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        outer.Children.Add(title);

        var subtitle = new TextBlock
        {
            Text = "Macro Automation Studio",
            FontSize = 13,
            Foreground = Palette.Brush(Palette.FgMuted),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
        };
        outer.Children.Add(subtitle);

        
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 22, 0, 0),
        };
        actions.Children.Add(ActionCard("\uE7C3", "New Script", "Ctrl+N", () => NewClicked?.Invoke()));
        actions.Children.Add(ActionCard("\uE8E5", "Open File", "Ctrl+O", () => OpenClicked?.Invoke()));
        outer.Children.Add(actions);

        
        var recentBlock = new StackPanel { Margin = new Thickness(0, 30, 0, 0), MaxWidth = 520 };
        recentBlock.HorizontalAlignment = HorizontalAlignment.Center;
        outer.Children.Add(recentBlock);

        _recentHeader = SectionHeader("Recent");
        recentBlock.Children.Add(_recentHeader);
        _recentList = new StackPanel();
        recentBlock.Children.Add(_recentList);
    }

    
    public void LoadRecents()
    {
        if (_recentList == null || _recentHeader == null) return;
        _recentList.Children.Clear();

        var recents = AppStorage.LoadRecentFiles();
        if (recents.Count == 0)
        {
            _recentHeader.Visibility = Visibility.Collapsed;
            _recentList.Children.Add(new TextBlock
            {
                Text = "No recent files yet.",
                Foreground = Palette.Brush(Palette.FgFaint),
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 2),
            });
            return;
        }
        _recentHeader.Visibility = Visibility.Visible;
        foreach (var path in recents.Take(6))
        {
            var p = path;
            bool missing = !File.Exists(p);
            var row = new Button
            {
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 5, 6, 5),
                Cursor = Cursors.Hand,
                ToolTip = missing ? "File no longer exists" : p,
            };
            
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new TextBlock
            {
                Text = "\uE8A5 ",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Foreground = Palette.Brush(missing ? Palette.FgFaint : Palette.AccentOrange),
                VerticalAlignment = VerticalAlignment.Center,
            });
            var col = new StackPanel();
            col.Children.Add(new TextBlock
            {
                Text = Path.GetFileName(p),
                Foreground = Palette.Brush(missing ? Palette.FgFaint : Palette.FgBright),
                FontSize = 12.5,
            });
            col.Children.Add(new TextBlock
            {
                Text = missing ? p + "  (missing)" : p,
                Foreground = Palette.Brush(Palette.FgFaint),
                FontSize = 10.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 340,
            });
            stack.Children.Add(col);
            row.Content = stack;
            row.Click += (_, _) => FileChosen?.Invoke(p);
            row.MouseEnter += (_, _) => row.Background = Palette.Brush(Palette.HoverBg);
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
            _recentList.Children.Add(row);
        }
    }

    

    private static TextBlock SectionHeader(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = Palette.Brush(Palette.FgMuted),
        Margin = new Thickness(0, 0, 0, 6),
    };

    private Button ActionCard(string glyph, string title, string shortcut, Action onClick)
    {
        var b = new Button
        {
            Background = Palette.Brush(Palette.SidebarBg),
            BorderBrush = Palette.Brush(Palette.BorderStrong),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 12, 18, 12),
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = Cursors.Hand,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 20,
            Foreground = Palette.Brush(Palette.AccentOrange),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            Foreground = Palette.Brush(Palette.FgBright),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
        });
        stack.Children.Add(new TextBlock
        {
            Text = shortcut,
            FontSize = 10.5,
            Foreground = Palette.Brush(Palette.FgFaint),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0),
        });
        b.Content = stack;
        b.Click += (_, _) => onClick();
        b.MouseEnter += (_, _) => b.Background = Palette.Brush(Palette.ActiveBg);
        b.MouseLeave += (_, _) => b.Background = Palette.Brush(Palette.SidebarBg);
        return b;
    }

}
