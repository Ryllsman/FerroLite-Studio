using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ferrolite.UI.Controls;






public class OverlayWindow : Window
{
    public new string Title { get; }
    public new int Width { get; }
    public new int Height { get; }

    private readonly ConcurrentDictionary<int, UIElement> _elements = new();
    private readonly Panel _panel;
    private readonly ConcurrentDictionary<Button, int> _clicks = new();
    private int _nextId = 1;

    public OverlayWindow(string title, int width, int height)
    {
        Title = title;
        Width = width;
        Height = height;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        
        
        PreviewMouseDown += OnClickThrough;
        PreviewMouseMove += OnClickThrough;

        _panel = new Canvas
        {
            Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x1A, 0x1A, 0x1A)),
            Width = width,
            Height = height,
        };
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
            BorderThickness = new Thickness(1),
            Child = _panel,
        };
        Content = border;

        Width = width + 2;
        Height = height + 2;
    }

    private void OnClickThrough(object sender, MouseEventArgs e)
    {
        if (HitTestInteractive(e.GetPosition(this)) == null)
            e.Handled = true;
    }

    private UIElement? HitTestInteractive(Point pos)
    {
        if (!_panel.IsMouseOver)
            return null;
        var tt = VisualTreeHelper.HitTest(_panel, pos);
        if (tt?.VisualHit is UIElement el && IsInteractive(el))
            return el;
        return null;
    }

    private static bool IsInteractive(UIElement el) =>
        el is Button or CheckBox;

    public int AddLabel(string text, int x, int y)
    {
        var tb = MakeText(text, FontWeights.Normal);
        _elements.TryAdd(_nextId, tb);
        Place(tb, x, y);
        return _nextId++;
    }

    public int AddButton(string text, int x, int y)
    {
        var b = new Button
        {
            Content = text,
            Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x6A, 0xC8)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 3, 6, 3),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Cursor = Cursors.Hand,
            Focusable = false,
            Width = Math.Max(Math.Max(60, text.Length) * 7 + 16, 60),
            Height = 22,
        };
        b.Click += (_, _) => _clicks.AddOrUpdate(b, 1, (_, n) => n + 1);
        _elements.TryAdd(_nextId, b);
        Place(b, x, y);
        return _nextId++;
    }

    public int AddToggle(string text, int x, int y, bool @default = false)
    {
        var cb = new CheckBox
        {
            Content = text,
            IsChecked = @default,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 3, 6, 3),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Cursor = Cursors.Hand,
            Focusable = false,
            Margin = new Thickness(0, 2, 0, 0),
        };
        _elements.TryAdd(_nextId, cb);
        Place(cb, x, y);
        return _nextId++;
    }

    private static TextBlock MakeText(string text, FontWeight weight)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    private void Place(UIElement child, int x, int y)
    {
        Canvas.SetLeft(child, x);
        Canvas.SetTop(child, y);
        _panel.Children.Add(child);
    }

    public void SetText(int elementId, string text)
    {
        if (_elements.TryGetValue(elementId, out var el))
        {
            if (el is TextBlock tb) tb.Text = text;
            else if (el is Button b) b.Content = text;
            else if (el is CheckBox cb) cb.Content = text;
        }
    }

    public void SetElementPosition(int elementId, int x, int y)
    {
        if (_elements.TryGetValue(elementId, out var el))
        {
            Canvas.SetLeft(el, x);
            Canvas.SetTop(el, y);
        }
    }

    public void SetAlwaysOnTop(bool enabled) => Topmost = enabled;

    public void Move(int x, int y)
    {
        if (x < 0) x = 0;
        if (y < 0) y = 0;
        Left = x;
        Top = y;
    }

    public void SetVisible(bool visible)
    {
        if (visible)
            Show();
        else
            Hide();
    }

    public bool ButtonClicked(int elementId)
    {
        if (_elements.TryGetValue(elementId, out var el) && el is Button b)
            return _clicks.AddOrUpdate(b, 0, (_, n) => n + 1) > 0;
        return false;
    }

    public bool GetToggle(int elementId)
    {
        if (_elements.TryGetValue(elementId, out var el) && el is CheckBox cb)
            return cb.IsChecked == true;
        return false;
    }

    public void RemoveElement(int elementId)
    {
        if (_elements.TryRemove(elementId, out var el))
            _panel.Children.Remove(el);
    }

    public void CloseWindow()
    {
        Hide();
        _clicks.Clear();
    }

    
    
    

    private bool _dragging;
    private Point _dragStartScreen;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (HitTestInteractive(e.GetPosition(this)) != null)
            return;
        _dragging = true;
        _dragStartScreen = PointToScreen(e.GetPosition(this));
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        var curScreen = PointToScreen(e.GetPosition(this));
        var dx = curScreen.X - _dragStartScreen.X;
        var dy = curScreen.Y - _dragStartScreen.Y;
        Left = Math.Max(0, Left + dx);
        Top = Math.Max(0, Top + dy);
        _dragStartScreen = curScreen;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
        }
    }
}
