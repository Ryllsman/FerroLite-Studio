using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ferrolite.UI.Controls;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace Ferrolite.UI.Views;






public class FindReplaceBar : Border
{
    private readonly TextBox _searchBox = new() { Width = 220, Height = 24 };
    private readonly TextBox _replaceBox = new() { Width = 220, Height = 24 };
    private readonly TextBlock _matchInfo = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
    private readonly StackPanel _replaceRow;
    private CodeEditor? _editor;
    private readonly List<int> _matches = new();
    private int _matchIdx = -1;
    private string? _lastSearch;

    public event Action? Closed;

    public FindReplaceBar()
    {
        Background = Palette.Brush(Palette.PanelHeaderBg);
        BorderBrush = Palette.Brush(Palette.BorderStrong);
        BorderThickness = new Thickness(0, 0, 0, 1);
        Padding = new Thickness(10, 8, 10, 8);
        VerticalAlignment = VerticalAlignment.Top;
        HorizontalAlignment = HorizontalAlignment.Right;
        Visibility = Visibility.Collapsed;
        Focusable = false;

        var root = new StackPanel();
        Child = root;

        
        var findRow = new StackPanel { Orientation = Orientation.Horizontal };
        findRow.Children.Add(MakeLabel("Find:"));
        findRow.Children.Add(_searchBox);
        findRow.Children.Add(MakeBtn("\u2191", () => FindNext(false), "Previous match (Shift+Enter)"));
        findRow.Children.Add(MakeBtn("\u2193", () => FindNext(true), "Next match (Enter)"));
        findRow.Children.Add(MakeBtn("\u2715", () => Close(), "Close (Esc)"));
        _matchInfo.Margin = new Thickness(8, 0, 0, 0);
        _matchInfo.Foreground = Palette.Brush(Palette.FgMuted);
        findRow.Children.Add(_matchInfo);
        root.Children.Add(findRow);

        
        _replaceRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _replaceRow.Children.Add(MakeLabel("Replace:"));
        _replaceRow.Children.Add(_replaceBox);
        _replaceRow.Children.Add(MakeBtn("Replace", ReplaceOne, "Replace current match", double.NaN));
        _replaceRow.Children.Add(MakeBtn("All", ReplaceAll, "Replace all matches", double.NaN));
        root.Children.Add(_replaceRow);

        _searchBox.TextChanged += (_, _) => RecomputeMatches(selectCurrent: true);
        _searchBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { FindNext(Keyboard.Modifiers != ModifierKeys.Shift); e.Handled = true; }
            else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        };
        _replaceBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { ReplaceOne(); e.Handled = true; }
            else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        };
    }

    public bool ReplaceVisible
    {
        get => _replaceRow.Visibility == Visibility.Visible;
        set => _replaceRow.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Open(CodeEditor editor, bool withReplace, string? initialText = null)
    {
        _editor = editor;
        ReplaceVisible = withReplace;
        Visibility = Visibility.Visible;
        _searchBox.Text = initialText ?? (EditorHasSelection(out string sel) && sel.Length > 0 && !sel.Contains('\n') ? sel : _searchBox.Text);
        _searchBox.SelectAll();
        _searchBox.Focus();
        RecomputeMatches(selectCurrent: true);
    }

    public void Close()
    {
        Visibility = Visibility.Collapsed;
        _editor?.Editor.Focus();
        Closed?.Invoke();
    }

    private bool EditorHasSelection(out string sel)
    {
        sel = _editor?.Editor.SelectedText ?? "";
        return sel.Length > 0;
    }

    private void RecomputeMatches(bool selectCurrent)
    {
        _matches.Clear();
        _matchIdx = -1;
        _lastSearch = null;
        var ed = _editor;
        if (ed == null || _searchBox.Text.Length == 0) { UpdateInfo(); return; }

        string text = ed.Editor.Text, pat = _searchBox.Text;
        int idx = 0;
        while ((idx = text.IndexOf(pat, idx, StringComparison.Ordinal)) >= 0)
        {
            _matches.Add(idx);
            idx += pat.Length;
        }
        if (selectCurrent && _matches.Count > 0)
        {
            
            int caret = ed.Editor.CaretOffset;
            _matchIdx = _matches.FindIndex(o => o >= caret);
            if (_matchIdx < 0) _matchIdx = 0;
            HighlightCurrent();
        }
        UpdateInfo();
    }

    
    public void RecomputeMatches() => RecomputeMatches(selectCurrent: false);

    private void FindNext(bool forward)
    {
        if (_matches.Count == 0) { RecomputeMatches(selectCurrent: true); if (_matches.Count == 0) return; }
        if (_lastSearch != _searchBox.Text) RecomputeMatches(selectCurrent: true);
        if (_matches.Count == 0) return;

        _matchIdx = forward
            ? (_matchIdx + 1) % _matches.Count
            : (_matchIdx - 1 + _matches.Count) % _matches.Count;
        HighlightCurrent();
        UpdateInfo();
    }

    private void HighlightCurrent()
    {
        if (_editor == null || _matchIdx < 0 || _matchIdx >= _matches.Count) return;
        int off = _matches[_matchIdx];
        int len = _searchBox.Text.Length;
        _editor.Editor.TextArea.Selection = Selection.Create(
            _editor.Editor.TextArea, off, off + len);
        _editor.Editor.TextArea.Caret.Offset = off + len;
        _editor.Editor.ScrollTo(_editor.Editor.TextArea.Caret.Line,
            _editor.Editor.TextArea.Caret.Column);
    }

    private void ReplaceOne()
    {
        var ed = _editor;
        if (ed == null || _searchBox.Text.Length == 0) return;
        if (_lastSearch != _searchBox.Text) RecomputeMatches(selectCurrent: true);

        
        var sel = ed.Editor.TextArea.Selection;
        int selStart = ed.Editor.SelectionStart;
        int selLen = ed.Editor.SelectionLength;
        if (selLen == _searchBox.Text.Length && _matches.Contains(selStart))
        {
            int off = selStart;
            ed.Editor.Document.Replace(off, sel.Length, _replaceBox.Text);
            RecomputeMatches(selectCurrent: false);
            
            _matchIdx = _matches.FindIndex(o => o >= off);
            if (_matchIdx < 0) _matchIdx = 0;
            if (_matches.Count > 0) HighlightCurrent();
            UpdateInfo();
        }
        else
        {
            FindNext(true);
        }
        ed.Editor.Focus();
    }

    private void ReplaceAll()
    {
        var ed = _editor;
        if (ed == null || _searchBox.Text.Length == 0) return;
        int count = 0;
        ed.Editor.BeginChange();
        while (true)
        {
            string text = ed.Editor.Text;
            int off = text.IndexOf(_searchBox.Text, StringComparison.Ordinal);
            if (off < 0) break;
            ed.Editor.Document.Replace(off, _searchBox.Text.Length, _replaceBox.Text);
            count++;
            if (count > 100000) break; 
        }
        ed.Editor.EndChange();
        RecomputeMatches(selectCurrent: false);
        UpdateInfo();
        _editor?.Editor.Focus();
    }

    private void UpdateInfo()
    {
        if (_searchBox.Text.Length == 0) { _matchInfo.Text = ""; return; }
        _matchInfo.Text = _matches.Count == 0
            ? "no matches"
            : _matchIdx >= 0
                ? $"{_matchIdx + 1} of {_matches.Count}"
                : $"{_matches.Count} found";
        _matchInfo.Foreground = Palette.Brush(_matches.Count == 0 ? Palette.Error : Palette.FgMuted);
        _lastSearch = _searchBox.Text;
    }

    private static TextBlock MakeLabel(string text) => new()
    {
        Text = text,
        Foreground = Palette.Brush(Palette.FgMuted),
        FontSize = 12,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 6, 0),
    };

    private Button MakeBtn(string text, Action onClick, string? tip = null, double width = 26)
    {
        bool isText = text.Length > 2; 
        var b = new Button
        {
            Content = text,
            Width = isText ? double.NaN : width,
            MinWidth = isText ? 60 : 0,
            MinHeight = 24,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = isText ? new Thickness(10, 0, 10, 0) : new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Palette.Brush(Palette.FgDefault),
            BorderThickness = new Thickness(0),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = tip,
        };
        b.Click += (_, _) => onClick();
        b.MouseEnter += (_, _) => b.Background = Palette.Brush(Palette.HoverBg);
        b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
        return b;
    }
}
