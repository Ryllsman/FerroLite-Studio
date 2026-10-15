using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Ferrolite.UI.Controls;





public class TerminalPanel : UserControl
{
    private readonly RichTextBox _output;
    private const int MaxParagraphs = 5000;

    public event Action<int>? LineClicked;

    public TerminalPanel()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        
        var header = new Border
        {
            Background = Palette.Brush(Palette.PanelHeaderBg),
            BorderBrush = Palette.Brush(Palette.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 0, 8, 0),
        };
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock
        {
            Text = "TERMINAL",
            Foreground = Palette.Brush(Palette.FgMuted),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var clearBtn = MakeButton("Clear");
        clearBtn.Click += (_, _) => Clear();
        headerGrid.Children.Add(title);
        headerGrid.Children.Add(clearBtn);
        Grid.SetColumn(clearBtn, 1);
        header.Child = headerGrid;
        header.Height = 28;
        root.Children.Add(header);

        
        _output = new RichTextBox
        {
            Background = Palette.Brush(Palette.TerminalBg),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4),
            FontFamily = Palette.CodeFont,
            FontSize = 12,
            Foreground = Palette.Brush(Palette.FgDefault),
            IsReadOnly = true,
            IsDocumentEnabled = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _output.Document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            Background = Palette.Brush(Palette.TerminalBg),
        };
        _output.Document.Blocks.Add(WelcomeParagraph());
        Grid.SetRow(_output, 1);
        root.Children.Add(_output);

        Content = root;
    }

    private static Paragraph WelcomeParagraph() => Line("Ferrolite Terminal — ready.", Palette.FgMuted);

    private static Button MakeButton(string text)
    {
        var b = new Button
        {
            Content = text,
            Height = 20,
            FontSize = 11,
            Padding = new Thickness(10, 0, 10, 0),
            Background = Brushes.Transparent,
            Foreground = Palette.Brush(Palette.FgMuted),
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        b.MouseEnter += (_, _) => b.Background = Palette.Brush(Palette.HoverBg);
        b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
        return b;
    }

    public void Clear()
    {
        _output.Document.Blocks.Clear();
        _output.Document.Blocks.Add(WelcomeParagraph());
    }

    private static Paragraph Line(string text, Color color)
    {
        var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
        p.Inlines.Add(new Run(text) { Foreground = Palette.Brush(color) });
        return p;
    }

    
    public void Print(string text, string kind = "info")
    {
        Color color = kind switch
        {
            "success" => Palette.Success,
            "error" => Palette.Error,
            "warning" => Palette.Warning,
            "muted" => Palette.FgMuted,
            "action" => Palette.AccentLight,
            "debug" => Palette.FgMuted,
            _ => Palette.FgDefault,
        };

        
        if (text.StartsWith("[SIM]") || text.StartsWith("[ACTION]")) color = Palette.AccentLight;
        if (text.StartsWith("[DEBUG]") || text.StartsWith("[PROFILE]")) color = Palette.FgMuted;

        Paragraph p;
        if (kind == "error")
            p = ErrorParagraph(text, color);
        else
            p = Line(text, color);

        _output.Document.Blocks.Add(p);
        
        while (_output.Document.Blocks.Count > MaxParagraphs)
            _output.Document.Blocks.Remove(_output.Document.Blocks.FirstBlock!);

        _output.ScrollToEnd();
    }

    private Paragraph ErrorParagraph(string text, Color color)
    {
        var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
        
        var m = Regex.Match(text, @"(at )?line\s+(\d+)");
        if (m.Success)
        {
            int line = int.Parse(m.Groups[2].Value);
            string before = text[..m.Index];
            string rest = text[(m.Index + m.Length)..];
            if (before.Length > 0)
                p.Inlines.Add(new Run(before) { Foreground = Palette.Brush(color) });
            var link = new Hyperlink(new Run("line " + line))
            {
                Foreground = Palette.Brush(Palette.AccentOrange),
            };
            link.Click += (_, _) => LineClicked?.Invoke(line);
            p.Inlines.Add(link);
            if (rest.Length > 0)
                p.Inlines.Add(new Run(rest) { Foreground = Palette.Brush(color) });
        }
        else
        {
            p.Inlines.Add(new Run(text) { Foreground = Palette.Brush(color) });
        }
        return p;
    }
}
