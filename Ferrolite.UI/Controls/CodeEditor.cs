using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Indentation;
using ICSharpCode.AvalonEdit.Rendering;

namespace Ferrolite.UI.Controls;






public class CodeEditor : UserControl
{
    public TextEditor Editor { get; }
    public HashSet<int> Breakpoints { get; } = new();

    private readonly BreakpointMargin _bpMargin;
    private readonly FerroliteLineNumberMargin _lineMargin;
    private readonly DebugLineRenderer _debugLine = new();

    public event EventHandler? DocumentChanged;
    public event Action<int, int>? CaretMoved;

    public CodeEditor()
    {
        Editor = new TextEditor();
        Content = Editor;

        Editor.SyntaxHighlighting = FerroliteHighlighter.Definition;
        Editor.FontFamily = Palette.CodeFont;
        Editor.FontSize = 14;
        Editor.Background = Palette.Brush(Palette.EditorBg);
        Editor.Foreground = Palette.Brush(Palette.FgDefault);
        Editor.Padding = new Thickness(4, 4, 8, 4);
        Editor.Options = new TextEditorOptions
        {
            ConvertTabsToSpaces = true,
            IndentationSize = 4,
            HighlightCurrentLine = true,
            EnableRectangularSelection = true,
            EnableHyperlinks = false,
        };
        Editor.TextArea.TextView.CurrentLineBackground = Palette.Brush(Palette.LineHighlightBg);
        Editor.TextArea.TextView.CurrentLineBorder = null;
        Editor.TextArea.TextView.BackgroundRenderers.Add(_debugLine);

        
        
        Editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;

        
        Editor.TextArea.SelectionBrush = Palette.Brush(Palette.SelectionBg);
        Editor.TextArea.SelectionForeground = Palette.Brush(Palette.FgBright);
        Editor.TextArea.SelectionBorder = null;

        
        Editor.TextArea.Caret.CaretBrush = Palette.Brush(Palette.FgBright);

        
        var left = Editor.TextArea.LeftMargins;
        for (int i = left.Count - 1; i >= 0; i--)
            if (left[i] is LineNumberMargin) left.RemoveAt(i);

        _bpMargin = new BreakpointMargin(this);
        _lineMargin = new FerroliteLineNumberMargin(this);
        left.Insert(0, _lineMargin);
        left.Insert(0, _bpMargin);

        Editor.TextArea.IndentationStrategy = new FerroliteIndentationStrategy();

        Editor.TextChanged += (_, _) => DocumentChanged?.Invoke(this, EventArgs.Empty);
        Editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            var c = Editor.TextArea.Caret.Position;
            CaretMoved?.Invoke(c.Line, c.Column);
        };

        
        Editor.TextArea.TextEntering += OnTextEntering;

        
        Editor.PreviewMouseLeftButtonDown += OnGutterClick;
    }

    public string Text
    {
        get => Editor.Text;
        set => Editor.Text = value ?? "";
    }

    public int CaretLine => Editor.TextArea.Caret.Line;
    public int CaretColumn => Editor.TextArea.Caret.Column;

    public void GoToLine(int line)
    {
        if (line < 1 || Editor.Document == null) return;
        line = Math.Min(line, Editor.Document.LineCount);
        Editor.TextArea.Caret.Line = line;
        Editor.TextArea.Caret.Column = 1;
        Editor.ScrollToLine(line);
        Editor.Focus();
    }

    public void ClearUndoHistory()
    {
        Editor.Document.UndoStack.ClearAll();
        Editor.Document.UndoStack.MarkAsOriginalFile();
    }

    public void SetAutoIndent(bool enabled)
    {
        Editor.TextArea.IndentationStrategy = enabled ? new FerroliteIndentationStrategy() : null;
    }

    public void SetDebugLine(int? line)
    {
        _debugLine.Line = line;
        Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
    }

    public void RefreshBreakpointMargin()
    {
        _bpMargin.Refresh();
    }

    public void ToggleBreakpoint(int line)
    {
        if (line < 1) return;
        if (!Breakpoints.Remove(line))
            Breakpoints.Add(line);
        _bpMargin.Refresh();
    }

    public void SetBreakpoints(IEnumerable<int> lines)
    {
        Breakpoints.Clear();
        foreach (var l in lines) if (l > 0) Breakpoints.Add(l);
        _bpMargin.Refresh();
    }

    private void OnTextEntering(object? sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length != 1) return;
        char ch = e.Text[0];
        string closers = ")]}\"";
        string openers = "([{\"";
        int idx = openers.IndexOf(ch);
        if (idx < 0)
        {
            if (closers.Contains(ch))
            {
                
                int off = Editor.CaretOffset;
                if (off < Editor.Document.TextLength && Editor.Document.GetCharAt(off) == ch)
                {
                    e.Handled = true;
                    Editor.TextArea.Caret.Offset = off + 1;
                }
            }
            return;
        }

        
        if (ch == '"' && Editor.TextArea.Selection.IsEmpty)
        {
            int off = Editor.CaretOffset;
            if (off < Editor.Document.TextLength)
            {
                char next = Editor.Document.GetCharAt(off);
                if (char.IsLetterOrDigit(next)) return;
            }
        }

        
        var sel = Editor.TextArea.Selection;
        if (!sel.IsEmpty)
        {
            int start = sel.SurroundingSegment.Offset;
            int len = sel.SurroundingSegment.Length;
            string text = Editor.Document.GetText(start, len);
            e.Handled = true;
            Editor.Document.Insert(start, ch + text + closers[idx]);
            Editor.TextArea.ClearSelection();
            Editor.TextArea.Caret.Offset = start + 1 + len;
            return;
        }

        
        
        
        
        e.Handled = true;
        int off2 = Editor.CaretOffset;
        Editor.Document.Insert(off2, ch.ToString() + closers[idx]);
        Editor.TextArea.Caret.Offset = off2 + 1;
    }

    private void OnGutterClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var tv = Editor.TextArea.TextView;
        Point p = e.GetPosition(tv);
        if (p.X >= 0) return; 
        
        if (p.X < -(_bpMargin.Width + _lineMargin.Width)) return;

        var pos = tv.GetPosition(new Point(0, p.Y));
        if (pos == null) return;
        int line = pos.Value.Line;
        ToggleBreakpoint(line);
        e.Handled = true;
    }
}


public class BreakpointMargin : AbstractMargin
{
    private readonly CodeEditor _owner;
    public const double BpWidth = 18;

    public BreakpointMargin(CodeEditor owner)
    {
        _owner = owner;
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView != null)
        {
            oldTextView.VisualLinesChanged -= Redraw;
            oldTextView.ScrollOffsetChanged -= Redraw;
        }
        if (newTextView != null)
        {
            newTextView.VisualLinesChanged += Redraw;
            newTextView.ScrollOffsetChanged += Redraw;
        }
        base.OnTextViewChanged(oldTextView, newTextView);
    }

    private void Redraw(object? sender, EventArgs e) => InvalidateVisual();

    public void Refresh()
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(BpWidth, 0);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var tv = TextView;
        if (tv == null || Document == null || !tv.VisualLinesValid) return;
        var brush = Palette.Brush(Palette.Error);
        foreach (var vl in tv.VisualLines)
        {
            int ln = vl.FirstDocumentLine.LineNumber;
            if (!_owner.Breakpoints.Contains(ln)) continue;
            double y = vl.VisualTop - tv.VerticalOffset + vl.Height / 2;
            drawingContext.DrawEllipse(brush, null, new Point(BpWidth / 2, y), 4.5, 4.5);
        }
    }
}


public class FerroliteLineNumberMargin : AbstractMargin
{
    private readonly CodeEditor _owner;

    public FerroliteLineNumberMargin(CodeEditor owner)
    {
        _owner = owner;
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView != null)
        {
            oldTextView.VisualLinesChanged -= Redraw;
            oldTextView.ScrollOffsetChanged -= Redraw;
        }
        if (newTextView != null)
        {
            newTextView.VisualLinesChanged += Redraw;
            newTextView.ScrollOffsetChanged += Redraw;
        }
        base.OnTextViewChanged(oldTextView, newTextView);
    }

    protected override void OnDocumentChanged(TextDocument oldDocument, TextDocument newDocument)
    {
        if (oldDocument != null)
            oldDocument.TextChanged -= Redraw;
        if (newDocument != null)
            newDocument.TextChanged += Redraw;
        base.OnDocumentChanged(oldDocument, newDocument);
    }

    private void Redraw(object? sender, EventArgs e)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (TextView == null || Document == null) return new Size(0, 0);
        int maxLine = Math.Max(1, Document.LineCount);
        double width = MeasureTextWidth(maxLine.ToString());
        return new Size(width + 12, 0);
    }

    private double MeasureTextWidth(string text)
    {
        var tf = CreateFormattedText(text, Palette.Brush(Palette.FgLineNumber));
        return tf.WidthIncludingTrailingWhitespace;
    }

    private FormattedText CreateFormattedText(string text, Brush brush)
    {
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(Palette.CodeFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            14, brush, dpi);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var tv = TextView;
        if (tv == null || Document == null || !tv.VisualLinesValid) return;

        int caretLine = _owner.Editor.TextArea.Caret.Position.Line;
        foreach (var vl in tv.VisualLines)
        {
            int ln = vl.FirstDocumentLine.LineNumber;
            Brush brush = ln == caretLine
                ? Palette.Brush(Palette.FgLineNumberActive)
                : Palette.Brush(Palette.FgLineNumber);
            var text = CreateFormattedText(ln.ToString(), brush);
            double y = vl.VisualTop - tv.VerticalOffset + (vl.Height - text.Height) / 2;
            drawingContext.DrawText(text, new Point(ActualWidth - text.Width - 6, y));
        }
    }
}


public class DebugLineRenderer : IBackgroundRenderer
{
    public int? Line { get; set; }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Line == null || textView == null || !textView.VisualLinesValid) return;
        var brush = new SolidColorBrush(Color.FromArgb(70, 0xcc, 0xa7, 0x00));
        brush.Freeze();
        foreach (var vl in textView.VisualLines)
        {
            if (vl.FirstDocumentLine.LineNumber != Line.Value) continue;
            var rc = new Rect(0, vl.VisualTop - textView.VerticalOffset,
                textView.ActualWidth, vl.Height);
            drawingContext.DrawRectangle(brush, null, rc);
        }
    }
}





public class FerroliteIndentationStrategy : IIndentationStrategy
{
    private const string Unit = "    ";

    public void IndentLines(TextDocument document, int beginLine, int endLine)
    {
        for (int i = beginLine; i <= endLine; i++)
        {
            var line = document.GetLineByNumber(i);
            IndentLine(document, line);
        }
    }

    public void IndentLine(TextDocument document, DocumentLine line)
    {
        if (document == null || line == null) return;
        if (line.LineNumber <= 1)
        {
            document.Replace(line.Offset, line.Length, "");
            return;
        }
        var prev = document.GetLineByNumber(line.LineNumber - 1);
        string prevText = document.GetText(prev);
        string indent = "";
        foreach (char ch in prevText)
        {
            if (ch == ' ' || ch == '\t') indent += ch;
            else break;
        }
        string trimmed = prevText.Trim();
        if (trimmed.EndsWith("{")) indent += Unit;
        string currentText = document.GetText(line);
        
        int wsLen = 0;
        while (wsLen < currentText.Length && (currentText[wsLen] == ' ' || currentText[wsLen] == '\t'))
            wsLen++;
        document.Replace(line.Offset, wsLen, indent);
    }
}
