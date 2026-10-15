using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ferrolite.UI.Controls;






public static class DialogShell
{
    public static void Apply(Window w, string title, UIElement body, bool resizable = true)
    {
        w.WindowStyle = WindowStyle.None;
        w.ResizeMode = resizable ? ResizeMode.CanResize : ResizeMode.NoResize;
        w.Background = Palette.Brush(Palette.SidebarBg);
        w.Foreground = Palette.Brush(Palette.FgDefault);
        w.ShowInTaskbar = false;

        var chrome = new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 30,
            ResizeBorderThickness = resizable ? new Thickness(6) : new Thickness(0),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        };
        System.Windows.Shell.WindowChrome.SetWindowChrome(w, chrome);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(BuildHeader(w, title));
        var host = new Border { Child = body };
        Grid.SetRow(host, 1);
        root.Children.Add(host);
        w.Content = root;

        
        w.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) w.Close();
        };
    }

    private static UIElement BuildHeader(Window w, string title)
    {
        var bar = new Border
        {
            Background = Palette.Brush(Palette.TitleBarRowBg),
            BorderBrush = Palette.Brush(Palette.BorderStrong),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        var dock = new DockPanel { Background = Brushes.Transparent };
        bar.Child = dock;

        var bolt = BrandMark.Create(15);
        bolt.Margin = new Thickness(12, 0, 6, 0);
        var titleText = new TextBlock
        {
            Text = title,
            Foreground = Palette.Brush(Palette.FgMuted),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(bolt);
        left.Children.Add(titleText);
        dock.Children.Add(left);

        var close = new Button
        {
            Content = "✕",
            Width = 44,
            Height = 30,
            FontSize = 13,
            Foreground = Palette.Brush(Palette.FgDefault),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Focusable = false,
            Cursor = Cursors.Arrow,
        };
        close.Click += (_, _) => w.Close();
        close.MouseEnter += (_, _) => close.Background = Palette.Brush(Palette.CloseHover);
        close.MouseLeave += (_, _) => close.Background = Brushes.Transparent;
        DockPanel.SetDock(close, Dock.Right);
        dock.Children.Add(close);

        bar.MouseLeftButtonDown += (_, _) =>
        {
            if (w.WindowState != WindowState.Maximized) w.DragMove();
        };
        return bar;
    }
}
