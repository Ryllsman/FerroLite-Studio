using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Ferrolite.Core.Lang;
using Ferrolite.Core.Recorder;
using Ferrolite.UI.Controls;
using Ferrolite.UI.Services;
using Microsoft.Win32;

namespace Ferrolite.UI.Views;

public class EditorDocument
{
    public string? FilePath { get; set; }
    public CodeEditor Editor { get; } = new();
    public bool Dirty { get; set; }
    public string DisplayTitle { get; set; } = "Untitled";
    public bool IsStartPage { get; set; }
}

public class MainWindow : Window
{
    
    private const int HOTKEY_RUN = 1;
    private const int HOTKEY_RECORD = 2;
    private const int HOTKEY_STOP = 3;
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

    private readonly List<EditorDocument> _docs = new();
    private int _activeIdx = -1;
    private bool _suppressDirty;

    private readonly ScriptRunner _runner = new(ScriptGuiBridge.Current);
    private readonly RawPlaybackService _player = new();
    private readonly RecorderController _recorderController;
    private List<RecordedEvent> _recording => _recorderController.Events;
    private bool _isRecording => _recorderController.IsRecording;
    private bool _paused;
    private double _speed = 1.0;
    private bool _wasMaximized;

    
    
    
    private bool _restoringSession;
    private DispatcherTimer? _sessionSaveTimer;
    private bool _sessionDirty;

    private readonly AppSettings _settings = AppStorage.LoadSettings();

    
    private readonly Panel _tabStrip = new StackPanel { Orientation = Orientation.Horizontal };
    private readonly Grid _editorHost = new();
    private readonly TerminalPanel _terminal = new();
    private readonly TimelinePanel _timeline = new();
    private readonly DebuggerPanel _debugger = new();
    private readonly Border _statusBar = new();
    private readonly TextBlock _titleBarText = new();
    private readonly TextBlock _statusText = new();
    private readonly TextBlock _statsText = new();
    private readonly TextBlock _posText = new();
    private readonly Border _armedChip = new();
    private readonly TextBlock _armedChipText = new();
    private readonly DispatcherTimer _armedTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Border _sidePanel = new();
    private readonly FindReplaceBar _findBar = new();
    private readonly StartPage _startPage = new();
    private readonly RowDefinition _terminalRow = new() { Height = new GridLength(250) };
    private CodeEditor? ActiveEditor => _activeIdx >= 0 && _activeIdx < _docs.Count ? _docs[_activeIdx].Editor : null;
    private EditorDocument? ActiveDoc => _activeIdx >= 0 && _activeIdx < _docs.Count ? _docs[_activeIdx] : null;

    public MainWindow()
    {
        Title = "FerroLite Studio";
        Width = 1280;
        Height = 820;
        MinWidth = 1024;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Palette.Brush(Palette.EditorBg);
        FontFamily = Palette.UiFont;
        LoadIcon();

        var chrome = new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        };
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });   
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });      
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });      
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });      
        var titleBar = BuildTitleBar();
        root.Children.Add(titleBar);
        Grid.SetRow(titleBar, 0);
        var menuStrip = BuildMenuStrip();
        root.Children.Add(menuStrip);
        Grid.SetRow(menuStrip, 1);
        var header = BuildHeaderBar();
        root.Children.Add(header);
        Grid.SetRow(header, 2);
        var contentGrid = BuildContent();
        root.Children.Add(contentGrid);
        Grid.SetRow(contentGrid, 3);
        var statusBar = BuildStatusBar();
        root.Children.Add(statusBar);
        Grid.SetRow(statusBar, 4);
        Content = root;

        _recorderController = new RecorderController(
            this, _terminal, _timeline, canStart: () => !_player.IsPlaying);
        
        _timeline.ClearClicked += () =>
        {
            _recorderController.Reset();
            _timeline.ClearList();
            SetStatus("Timeline cleared");
            SetStatusNeutral();
        };
        
        
        
        
        
        if (!RestoreSessionTabs())
            CreateNewTab();
        WireRunner();
        WireTerminal();
        WirePlayer();
        PreviewKeyDown += OnLocalKey;
        
        
        
        
        _armedTimer.Tick += (_, _) => RefreshArmedChip();
        _armedTimer.Start();
        WireSessionPersistence();
        Closed += (_, _) =>
        {
            _sessionSaveTimer?.Stop();
            SaveSessionNow();
            _runner.StopWait();
            _player.Stop();
            _recorderController.Dispose();
            UnregisterHotkeys();
        };
    }

    

    
    
    
    
    
    
    
    private void WireSessionPersistence()
    {
        _sessionSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _sessionSaveTimer.Tick += (_, _) =>
        {
            _sessionSaveTimer.Stop();
            if (_sessionDirty) SaveSessionNow();
        };
        LocationChanged += (_, _) => SaveSessionDebounced();
        SizeChanged += (_, _) => SaveSessionDebounced();
        StateChanged += (_, _) => SaveSessionDebounced();
    }

    
    private void SaveSessionDebounced()
    {
        if (_restoringSession) return;
        _sessionDirty = true;
        var timer = _sessionSaveTimer;
        if (timer == null) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => SaveSessionDebounced()); return; }
        if (!timer.IsEnabled) timer.Start();
    }

    
    private void SaveSessionNow()
    {
        _sessionDirty = false;
        var s = new SessionState
        {
            WindowMaximized = WindowState == WindowState.Maximized,
            TerminalVisible = _terminalRow.Height.Value > 10,
            ActiveTab = Math.Max(0, _activeIdx),
            Tabs = new List<SessionTab>(),
        };
        if (WindowState == WindowState.Normal)
        {
            s.WindowX = Left; s.WindowY = Top;
            s.WindowWidth = ActualWidth; s.WindowHeight = ActualHeight;
        }
        foreach (var d in _docs)
        {
            if (d.IsStartPage) continue;
            s.Tabs.Add(new SessionTab
            {
                FilePath = d.FilePath,
                
                
                
                UnsavedText = d.FilePath == null ? d.Editor.Text : null,
                CaretLine = d.Editor.CaretLine,
                CaretColumn = d.Editor.CaretColumn,
            });
        }
        if (s.Tabs.Count == 0) return;
        AppStorage.SaveSession(s);
    }

    
    
    
    
    
    
    
    
    private bool RestoreSessionTabs()
    {
        var s = AppStorage.LoadSession();
        if (s.Tabs.Count == 0) return false;

        if (s.WindowWidth >= MinWidth && s.WindowHeight >= MinHeight)
        {
            
            
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = s.WindowWidth;
            Height = s.WindowHeight;
            
            
            Left = Math.Max(-(Width - 120), Math.Min(s.WindowX, SystemParameters.WorkArea.Width - 120));
            Top = Math.Max(0, Math.Min(s.WindowY, SystemParameters.WorkArea.Height - 40));
        }
        if (!s.TerminalVisible) _terminalRow.Height = new GridLength(0);

        
        
        
        
        _restoringSession = true;
        try
        {
            foreach (var t in s.Tabs)
            {
                if (t.FilePath != null)
                {
                    string text;
                    try { text = File.ReadAllText(t.FilePath); }
                    catch { continue; } 
                    CreateNewTab(text, t.FilePath);
                }
                else
                {
                    CreateNewTab(t.UnsavedText ?? "");
                }
            }
        }
        finally { _restoringSession = false; }

        if (_docs.Count == 0)
        {
            CreateNewTab();
            return true;
        }

        int active = Math.Clamp(s.ActiveTab, 0, _docs.Count - 1);
        SelectDoc(active);

        
        
        for (int i = 0; i < _docs.Count && i < s.Tabs.Count; i++)
        {
            var t = s.Tabs[i];
            var ed = _docs[i].Editor.Editor;
            try
            {
                ed.TextArea.Caret.Line = Math.Clamp(t.CaretLine, 1, Math.Max(1, ed.LineCount));
                ed.TextArea.Caret.Column = Math.Max(1, t.CaretColumn);
            }
            catch { }
        }
        return true;
    }

    

    
    private void OpenPath(string path)
    {
        var existing = _docs.FirstOrDefault(d => d.FilePath != null &&
            string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            SelectDoc(_docs.IndexOf(existing));
            return;
        }
        string text;
        try { text = File.ReadAllText(path); }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open file:\n{ex.Message}", "FerroLite Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        CreateNewTab(text, path);
        SetStatus($"Opened {Path.GetFileName(path)}");
    }

    

    private UIElement BuildMenuStrip()
    {
        var strip = new Border
        {
            Background = Palette.Brush(Palette.ToolbarBg),
            BorderBrush = Palette.Brush(Palette.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0) };
        strip.Child = panel;

        void Top(string header, System.Collections.Generic.List<object> items)
        {
            var mi = new MenuItem { Header = header, FontSize = 12 };
            foreach (var it in items) mi.Items.Add(it);
            panel.Children.Add(mi);
        }
        MenuItem Item(string header, Action click, string? input = null, bool enabled = true)
        {
            var mi = new MenuItem { Header = header, InputGestureText = input ?? "", IsEnabled = enabled };
            mi.Click += (_, _) => click();
            return mi;
        }

        var fileItems = new List<object>
        {
            Item("New", NewFile, "Ctrl+N"),
            Item("Open…", OpenFile, "Ctrl+O"),
            Item("Save", SaveFile, "Ctrl+S"),
            Item("Export…", ExportFile),
            new Separator(),
            Item("Close Tab", () => CloseDoc(_activeIdx), "Ctrl+W"),
        };
        Top("File", fileItems);

        var editItems = new List<object>
        {
            Item("Find…", () => OpenFind(false), "Ctrl+F"),
            Item("Replace…", () => OpenFind(true), "Ctrl+H"),
            new Separator(),
            Item("Cut", () => ActiveEditor?.Editor.Cut(), "Ctrl+X"),
            Item("Copy", () => ActiveEditor?.Editor.Copy(), "Ctrl+C"),
            Item("Paste", () => ActiveEditor?.Editor.Paste(), "Ctrl+V"),
            Item("Select All", () => ActiveEditor?.Editor.SelectAll(), "Ctrl+A"),
        };
        Top("Edit", editItems);

        Top("View", new System.Collections.Generic.List<object>
        {
            Item("Toggle Terminal", ToggleTerminalVisibility, "Ctrl+`"),
        });
        Top("Help", new System.Collections.Generic.List<object>
        {
            Item("Language Reference", OpenReference, "F1"),
            Item("Settings…", OpenSettings),
        });
        return strip;
    }

    private void ToggleTerminalVisibility()
    {
        _terminalRow.Height = _terminalRow.Height.Value > 10
            ? new GridLength(0)
            : new GridLength(250);
        SaveSessionDebounced();
    }

    private UIElement BuildTitleBar()
    {
        var bar = new Border
        {
            Background = Palette.Brush(Palette.TitleBarRowBg),
            BorderBrush = Palette.Brush(Palette.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        var dock = new DockPanel { Background = Brushes.Transparent, LastChildFill = false };
        bar.Child = dock;

        var bolt = BrandMark.Create(16);
        bolt.Margin = new Thickness(10, 0, 6, 0);
        _titleBarText.Text = "FerroLite Studio";
        _titleBarText.Foreground = Palette.Brush(Palette.FgMuted);
        _titleBarText.FontSize = 12;
        _titleBarText.VerticalAlignment = VerticalAlignment.Center;
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(bolt);
        left.Children.Add(_titleBarText);
        dock.Children.Add(left);

        
        
        var mdl2 = new FontFamily("Segoe MDL2 Assets");
        Button? maxBtn = null;

        void CaptionBtn(string glyph, string tip, Action click, bool danger = false)
        {
            var b = new Button
            {
                Content = glyph,
                Width = 46,
                Height = 30,
                FontFamily = mdl2,
                FontSize = 11,
                Foreground = Palette.Brush(danger ? Palette.FgBright : Palette.FgDefault),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Focusable = false,
                Cursor = Cursors.Arrow,
                ToolTip = tip,
            };
            b.Click += (_, _) => click();
            b.MouseEnter += (_, _) =>
            {
                b.Background = danger ? Palette.Brush(Palette.CloseHover) : Palette.Brush(Palette.HoverBg);
                if (danger) b.Foreground = Brushes.White;
            };
            b.MouseLeave += (_, _) =>
            {
                b.Background = Brushes.Transparent;
                b.Foreground = Palette.Brush(danger ? Palette.FgBright : Palette.FgDefault);
            };
            if (tip == "Maximize") maxBtn = b;
            DockPanel.SetDock(b, Dock.Right);
            dock.Children.Add(b);
        }

        void UpdateMaxGlyph()
        {
            if (maxBtn != null)
                maxBtn.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        }

        CaptionBtn("\uE8BB", "Close (Alt+F4)", Close, danger: true);
        CaptionBtn("\uE922", "Maximize", ToggleMaximize);
        CaptionBtn("\uE921", "Minimize", () => WindowState = WindowState.Minimized);
        UpdateMaxGlyph();
        StateChanged += (_, _) => UpdateMaxGlyph();

        bar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) ToggleMaximize();
            else if (WindowState != WindowState.Maximized) DragMove();
        };
        return bar;
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private UIElement BuildHeaderBar()
    {
        var header = new HeaderBar();
        header.NewClicked += NewFile;
        header.OpenClicked += OpenFile;
        header.SaveClicked += SaveFile;
        header.RunClicked += () => StartRun(false);
        header.DebugClicked += () => StartRun(true);
        header.StopClicked += StopEverything;
        header.PauseClicked += TogglePause;
        header.RecordClicked += StartRecording;
        header.StopRecordingClicked += StopRecording;
        header.ExportClicked += ExportFile;
        header.RecordingsClicked += OpenRecordingsManager;
        header.ReferenceClicked += OpenReference;
        header.SettingsClicked += OpenSettings;
        header.SpeedChanged += speed =>
        {
            _speed = speed;
            if (_runner.IsRunning) _runner.SetSpeed(speed);
            if (_player.IsPlaying) _player.Pause(false); 
        };
        return header;
    }

    private UIElement BuildContent()
    {
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) }); 
        left.RowDefinitions.Add(_terminalRow);                                    

        var tabHost = new Border
        {
            Background = Palette.Brush(Palette.SidebarBg),
            BorderBrush = Palette.Brush(Palette.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        var tabScroller = new ScrollViewer
        {
            Content = _tabStrip,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        tabHost.Child = tabScroller;
        left.Children.Add(tabHost);

        _editorHost.Background = Palette.Brush(Palette.EditorBg);
        Grid.SetRow(_editorHost, 1);
        left.Children.Add(_editorHost);

        
        _editorHost.Children.Add(_findBar);
        Panel.SetZIndex(_findBar, 10);

        var splitter = new GridSplitter
        {
            Height = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Rows,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Background = Palette.Brush(Palette.SidebarBg),
        };
        Grid.SetRow(splitter, 2);
        left.Children.Add(splitter);
        splitter.DragDelta += (_, e) =>
        {
            double h = _terminalRow.Height.Value + e.VerticalChange;
            _terminalRow.Height = new GridLength(Math.Clamp(h, 120, 700));
        };

        var terminalBorder = new Border
        {
            Background = Palette.Brush(Palette.PanelBg),
            BorderBrush = Palette.Brush(Palette.BorderStrong),
            BorderThickness = new Thickness(0, 0, 0, 0),
        };
        terminalBorder.Child = _terminal;
        Grid.SetRow(terminalBorder, 3);
        left.Children.Add(terminalBorder);

        content.Children.Add(left);

        
        _sidePanel.Width = 320;
        _sidePanel.BorderBrush = Palette.Brush(Palette.BorderStrong);
        _sidePanel.BorderThickness = new Thickness(1, 0, 0, 0);
        _sidePanel.Child = _timeline;
        _sidePanel.Visibility = Visibility.Collapsed;
        Grid.SetColumn(_sidePanel, 1);
        content.Children.Add(_sidePanel);
        return content;
    }

    private UIElement BuildStatusBar()
    {
        _statusBar.Background = Palette.Brush(Palette.StatusbarBg);
        _statusBar.BorderBrush = Palette.Brush(Palette.Border);
        _statusBar.BorderThickness = new Thickness(0, 1, 0, 0);
        _statusBar.Height = 24;
        var dock = new DockPanel { Margin = new Thickness(12, 0, 12, 0) };
        _statusBar.Child = dock;

        _statusDot = new System.Windows.Shapes.Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = Palette.Brush(Palette.FgMuted),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        dock.Children.Add(_statusDot);

        _statusText.Text = "Ready";
        _statusText.Foreground = Palette.Brush(Palette.StatusTextNormal);
        _statusText.FontSize = 11;
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        dock.Children.Add(_statusText);

        
        _armedChip.CornerRadius = new CornerRadius(3);
        _armedChip.Padding = new Thickness(7, 2, 7, 2);
        _armedChip.Margin = new Thickness(12, 0, 0, 0);
        _armedChip.VerticalAlignment = VerticalAlignment.Center;
        _armedChip.Visibility = Visibility.Collapsed;
        _armedChipText.FontSize = 10;
        _armedChipText.FontWeight = FontWeights.SemiBold;
        _armedChipText.Foreground = Palette.Brush(Palette.Canvas);
        _armedChipText.Text = "CONTROLLER ARMED";
        _armedChip.Child = _armedChipText;
        DockPanel.SetDock(_armedChip, Dock.Right);
        dock.Children.Add(_armedChip);

        var right = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(right, Dock.Right);
        _statsText.Foreground = Palette.Brush(Palette.FgMuted);
        _statsText.FontSize = 11;
        _statsText.VerticalAlignment = VerticalAlignment.Center;
        var sep = new Border
        {
            Width = 1,
            Height = 14,
            Background = Palette.Brush(Palette.BorderStrong),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0),
        };
        _posText.Text = "Ln 1, Col 1";
        _posText.Foreground = Palette.Brush(Palette.FgMuted);
        _posText.FontSize = 11;
        _posText.VerticalAlignment = VerticalAlignment.Center;
        right.Children.Add(_statsText);
        right.Children.Add(sep);
        right.Children.Add(_posText);
        dock.Children.Add(right);
        return _statusBar;
    }

    private void LoadIcon()
    {
        try
        {
            var stream = Application.GetResourceStream(new Uri("Resources/ferrolite.ico", UriKind.Relative))?.Stream;
            if (stream != null)
            {
                Icon = System.Windows.Media.Imaging.BitmapFrame.Create(stream);
                stream.Dispose();
            }
        }
        catch { }
    }

    

    private void CreateNewTab(string? content = null, string? filePath = null)
    {
        
        if (content == null && filePath == null)
        {
            var sp = _docs.FirstOrDefault(d => d.IsStartPage);
            if (sp != null) { SelectDoc(_docs.IndexOf(sp)); return; }
        }

        var doc = new EditorDocument { FilePath = filePath };
        if (content != null && filePath == null)
        {
            
            _suppressDirty = true;
            doc.Editor.Text = content;
            _suppressDirty = false;
            doc.Editor.ClearUndoHistory();
            doc.Dirty = false;
            doc.DisplayTitle = "Untitled";
            int n = _docs.Count(d => !d.IsStartPage && d.FilePath == null);
            if (n > 1) doc.DisplayTitle = $"Untitled {n - 1}";
        }
        else if (content != null)
        {
            _suppressDirty = true;
            doc.Editor.Text = content;
            _suppressDirty = false;
            doc.Editor.ClearUndoHistory();
            doc.Dirty = false;
            doc.DisplayTitle = Path.GetFileName(filePath) ?? "Untitled";
        }
        else
        {
            int n = _docs.Count(d => d.FilePath == null);
            doc.DisplayTitle = n == 0 ? "Untitled" : $"Untitled {n}";
        }
        doc.Editor.DocumentChanged += (s, _) =>
        {
            if (_suppressDirty || s != ActiveEditor) return;
            var d = _docs.FirstOrDefault(x => x.Editor == s);
            if (d != null && !d.Dirty)
            {
                d.Dirty = true;
                RefreshTabStrip();
                RefreshTitle();
            }
            SaveSessionDebounced();
        };
        doc.Editor.CaretMoved += (line, col) =>
        {
            _posText.Text = $"Ln {line}, Col {col}";
        };
        doc.Editor.Editor.FontSize = _settings.FontSize;
        doc.Editor.SetAutoIndent(_settings.AutoIndent);
        doc.Editor.Editor.TextChanged += (_, _) => _findBar.RecomputeMatches();
        doc.Editor.ContextMenu = BuildEditorMenu();

        
        if (filePath != null) AppStorage.AddRecentFile(filePath);
        doc.Editor.ContextMenu = BuildEditorMenu();
        doc.Editor.DocumentChanged += (_, _) => SaveSessionDebounced();
        _docs.Add(doc);
        SelectDoc(_docs.Count - 1);
    }

    private void SelectDoc(int idx)
    {
        if (idx < 0 || idx >= _docs.Count) return;
        _activeIdx = idx;
        if (!_restoringSession) SaveSessionDebounced();
        var doc = _docs[idx];
        _editorHost.Children.Clear();
        if (doc.IsStartPage)
        {
            _startPage.LoadRecents();
            _editorHost.Children.Add(_startPage);
            Title = "FerroLite Studio";
            _titleBarText.Text = "FerroLite Studio";
        }
        else
        {
            _editorHost.Children.Add(doc.Editor);
            RefreshTitle();
            doc.Editor.Focus();
        }
        RefreshTabStrip();
    }

    private void CloseDoc(int idx)
    {
        if (idx < 0 || idx >= _docs.Count) return;
        var doc = _docs[idx];
        bool wasActive = idx == _activeIdx;
        if (doc.Dirty)
        {
            var r = MessageBox.Show($"Save changes to \"{doc.DisplayTitle}\"?", "FerroLite Studio",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (r == MessageBoxResult.Cancel) return;
            if (r == MessageBoxResult.Yes)
            {
                if (!SaveDoc(doc)) return;
            }
        }
        _docs.RemoveAt(idx);
        SaveSessionDebounced();
        if (_docs.Count == 0)
        {
            CreateNewTab();
            return;
        }
        if (wasActive)
        {
            SelectDoc(Math.Clamp(idx, 0, _docs.Count - 1));
        }
        else
        {
            if (_activeIdx > idx) _activeIdx--;
            RefreshTabStrip();
        }
    }

    private void RefreshTabStrip()
    {
        _tabStrip.Children.Clear();
        for (int i = 0; i < _docs.Count; i++)
        {
            int idx = i;
            var doc = _docs[i];
            bool active = i == _activeIdx;

            var capsule = new Border
            {
                Background = Palette.Brush(active ? Palette.EditorBg : Colors.Transparent),
                Padding = new Thickness(14, 7, 6, 7),
                Cursor = Cursors.Hand,
            };
            if (active)
            {
                capsule.BorderBrush = Palette.Brush(Palette.AccentOrange);
                capsule.BorderThickness = new Thickness(0, 2, 0, 0);
            }

            var inner = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var dot = new TextBlock
            {
                Text = doc.Dirty ? "● " : "",
                Foreground = Palette.Brush(Palette.AccentOrange),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var label = new TextBlock
            {
                Text = doc.DisplayTitle,
                Foreground = Palette.Brush(active
                    ? (doc.Dirty ? Palette.AccentOrange : Palette.FgBright)
                    : (doc.Dirty ? Palette.AccentOrangeDim : Palette.TabInactiveFg)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var close = new Button
            {
                Content = "✕",
                Width = 20,
                Height = 20,
                FontSize = 9,
                Background = Brushes.Transparent,
                Foreground = Palette.Brush(active ? Palette.FgMuted : Palette.FgMuted),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = Cursors.Arrow,
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center,
            };
            close.MouseEnter += (_, _) => { close.Background = Palette.Brush(Palette.CloseHover); close.Foreground = Palette.Brush(Palette.FgBright); };
            close.MouseLeave += (_, _) => { close.Background = Brushes.Transparent; close.Foreground = Palette.Brush(Palette.FgMuted); };
            close.Click += (_, _) => CloseDoc(idx);
            inner.Children.Add(dot);
            inner.Children.Add(label);
            inner.Children.Add(close);
            capsule.Child = inner;

            capsule.ContextMenu = BuildTabMenu(idx, doc);

            capsule.MouseDown += (_, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle) CloseDoc(idx);
                else SelectDoc(idx);
            };
            capsule.MouseEnter += (_, _) =>
            {
                if (!active) capsule.Background = Palette.Brush(Palette.HoverBg);
            };
            capsule.MouseLeave += (_, _) =>
            {
                if (!active) capsule.Background = Brushes.Transparent;
            };
            _tabStrip.Children.Add(capsule);
        }

        
        var plus = new Button
        {
            Content = "+",
            Padding = new Thickness(7, 0, 7, 0),
            Background = Brushes.Transparent,
            Foreground = Palette.Brush(Palette.FgMuted),
            BorderThickness = new Thickness(0),
            FontSize = 14,
            Margin = new Thickness(2, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "New script (Ctrl+N)",
        };
        plus.Click += (_, _) => CreateNewTab();
        _tabStrip.Children.Add(plus);
    }

    private void RefreshTitle()
    {
        var doc = ActiveDoc;
        if (doc == null) return;
        string name = Path.GetFileName(doc.FilePath) ?? doc.DisplayTitle;
        Title = $"{(doc.Dirty ? "● " : "")}{name} - FerroLite Studio";
        _titleBarText.Text = $"{(doc.Dirty ? "● " : "")}{name} - FerroLite Studio";
    }

    

    private ContextMenu BuildTabMenu(int idx, EditorDocument doc)
    {
        var menu = new ContextMenu();
        void Add(string header, Action action, bool enabled = true)
        {
            var mi = new MenuItem { Header = header, IsEnabled = enabled };
            mi.Click += (_, _) => action();
            menu.Items.Add(mi);
        }
        Add("Save", () => { if (SaveDoc(doc)) SetStatus($"Saved {doc.DisplayTitle}"); });
        Add("Rename…", () => RenameDoc(idx));
        Add("Duplicate", () => DuplicateDoc(idx));
        menu.Items.Add(new Separator());
        Add("Close", () => CloseDoc(idx));
        Add("Close Others", () => CloseOthers(idx), _docs.Count > 1);
        Add("Close All", CloseAllTabs);
        if (doc.FilePath != null)
        {
            menu.Items.Add(new Separator());
            Add("Copy Full Path", () =>
            {
                Clipboard.SetText(doc.FilePath!);
                SetStatus("Path copied");
            });
        }
        return menu;
    }

    private void RenameDoc(int idx)
    {
        if (idx < 0 || idx >= _docs.Count) return;
        var doc = _docs[idx];
        var dlg = new TextInputWindow("Rename Tab", "New tab name:", doc.DisplayTitle) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        string name = dlg.Value.Trim();
        if (name.Length == 0 || name == doc.DisplayTitle) return;

        if (doc.FilePath != null)
        {
            
            string dir = Path.GetDirectoryName(doc.FilePath) ?? "";
            string ext = Path.GetExtension(doc.FilePath);
            string newName = Path.HasExtension(name) ? name : name + ext;
            string dest = Path.Combine(dir, newName);
            try
            {
                if (!string.Equals(dest, doc.FilePath, StringComparison.OrdinalIgnoreCase))
                    File.Move(doc.FilePath, dest);
                doc.FilePath = dest;
                doc.DisplayTitle = Path.GetFileName(dest);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not rename the file:\n{ex.Message}", "Rename",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        else
        {
            doc.DisplayTitle = name;
        }
        RefreshTabStrip();
        RefreshTitle();
        SetStatus($"Renamed to {doc.DisplayTitle}");
    }

    private void DuplicateDoc(int idx)
    {
        if (idx < 0 || idx >= _docs.Count) return;
        var src = _docs[idx];
        string baseName = src.FilePath != null
            ? Path.GetFileNameWithoutExtension(src.FilePath)
            : src.DisplayTitle;
        CreateNewTab(src.Editor.Text);
        var copy = _docs[^1];
        copy.DisplayTitle = baseName + " copy";
        copy.Dirty = true;
        RefreshTabStrip();
        RefreshTitle();
        SetStatus("Tab duplicated");
    }

    private void CloseOthers(int keepIdx)
    {
        while (_docs.Count > 1)
        {
            int idx = _docs.Count - 1 == keepIdx ? 0 : _docs.Count - 1;
            if (idx == keepIdx) break;
            CloseDoc(idx);
        }
    }

    private void CloseAllTabs()
    {
        while (_docs.Count > 1) CloseDoc(_docs.Count - 1);
        CloseDoc(0);
    }

    

    private void NewFile()
    {
        var doc = ActiveDoc;
        if (doc != null && !doc.IsStartPage && !doc.Dirty && string.IsNullOrEmpty(doc.Editor.Text.Trim()))
        {
            doc.Editor.Text = "";
            return;
        }
        CreateNewTab();
    }

    private void OpenFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Ferrolite Script (*.frl)|*.frl|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            Title = "Open Script",
        };
        if (dlg.ShowDialog(this) != true) return;

        var existing = _docs.FirstOrDefault(d => d.FilePath != null &&
            string.Equals(d.FilePath, dlg.FileName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            SelectDoc(_docs.IndexOf(existing));
            return;
        }
        string text;
        try { text = File.ReadAllText(dlg.FileName); }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open file:\n{ex.Message}", "FerroLite Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        CreateNewTab(text, dlg.FileName);
        AppStorage.AddRecentFile(dlg.FileName);
        SetStatus($"Opened {Path.GetFileName(dlg.FileName)}");
    }

    private bool SaveDoc(EditorDocument doc)
    {
        if (doc.FilePath == null)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Ferrolite Script (*.frl)|*.frl|All Files (*.*)|*.*",
                FileName = doc.DisplayTitle.EndsWith(".frl") ? doc.DisplayTitle : doc.DisplayTitle + ".frl",
            };
            if (dlg.ShowDialog(this) != true) return false;
            doc.FilePath = dlg.FileName;
        }
        try
        {
            File.WriteAllText(doc.FilePath, doc.Editor.Text);
            doc.Dirty = false;
            doc.DisplayTitle = Path.GetFileName(doc.FilePath);
            AppStorage.AddRecentFile(doc.FilePath);
            RefreshTabStrip();
            RefreshTitle();
            SaveSessionDebounced();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save file:\n{ex.Message}", "FerroLite Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void SaveFile()
    {
        if (ActiveDoc != null && SaveDoc(ActiveDoc!))
            SetStatus($"Saved {ActiveDoc!.DisplayTitle}");
    }

    private void ExportFile()
    {
        if (ActiveDoc == null) return;
        var dlg = new SaveFileDialog
        {
            Filter = "Ferrolite Script (*.frl)|*.frl|All Files (*.*)|*.*",
            FileName = (Path.GetFileName(ActiveDoc.FilePath) ?? ActiveDoc.DisplayTitle).Replace(".txt", ".frl"),
        };
        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                File.WriteAllText(dlg.FileName, ActiveEditor!.Text);
                _terminal.Print($"Exported script to {dlg.FileName}", "success");
                SetStatus("Exported");
                SaveSessionDebounced();
            }
            catch (Exception ex)
            {
                _terminal.Print($"Export failed: {ex.Message}", "error");
            }
        }
    }

    

    private void StartRun(bool debug)
    {
        if (_runner.IsRunning) return;
        if (ActiveEditor == null) return;
        string source = ActiveEditor.Text;
        if (string.IsNullOrWhiteSpace(source))
        {
            SetStatus("Nothing to run");
            return;
        }
        if (_settings.ConfirmOnRun)
        {
            var r = MessageBox.Show($"Run this script?{(debug ? " (debug)" : "")}", "FerroLite Studio",
                MessageBoxButton.OKCancel, MessageBoxImage.Information);
            if (r != MessageBoxResult.OK) return;
        }

        _terminal.Clear();
        _terminal.Print(new string('─', 60), "muted");
        _terminal.Print(debug ? "Debugging…" : "Running script…", "info");
        SetStatus(debug ? "Debugging…" : "Running…");
        if (debug)
            SetStatusColor(Palette.AccentOrange);
        else
            SetStatusColor(Palette.Success);

        ActiveEditor.SetDebugLine(null);
        _wasMaximized = WindowState == WindowState.Maximized;

        if (!debug)
        {
            
            
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (_runner.IsRunning) RecorderController.ActivateNextWindow(this);
            };
            timer.Start();
        }
        else
        {
            ShowDebugger(true);
        }

        Header().SetRunning(true);
        
        
        ViGEmVirtualBackend.UseDualShock4 =
            string.Equals(_settings.VirtualController, "ds4", StringComparison.OrdinalIgnoreCase);
        _runner.Start(source, debug, ActiveEditor.Breakpoints);
    }

    private void StopEverything()
    {
        
        if (_recorderController.IsRecording)
        {
            StopRecording();
            return;
        }
        if (_player.IsPlaying)
        {
            _player.Stop();
            SetStatus("Stopped");
            SetStatusNeutral();
            return;
        }
        _runner.Stop();
        if (_runner.IsDebug) ShowDebugger(false);
        SetStatus("Stopped");
        SetStatusNeutral();
        RestoreWindow();
    }

    private void TogglePause()
    {
        if (!_runner.IsRunning || _runner.IsDebug) return;
        _paused = !_paused;
        _runner.Pause(_paused);
        Header().SetPaused(_paused);
        SetStatus(_paused ? "Paused" : "Running…");
        SetStatusColor(_paused ? Palette.Warning : Palette.Success);
    }

    private void WireRunner()
    {
        _runner.Output += (text, kind) => Ui(() => _terminal.Print(text, kind));
        _runner.FinishedOk += (elapsed, commands) => Ui(() =>
        {
            Header().SetRunning(false);
            Header().SetPaused(false);
            _paused = false;
            SetStatus("Ready");
            SetStatusNeutral();
            _statsText.Text = $"{commands} commands · {elapsed:F2}s";
            if (_runner.IsDebug) ShowDebugger(false);
            else RestoreWindow();
        });
        _runner.FinishedError += (message, _) => Ui(() =>
        {
            Header().SetRunning(false);
            Header().SetPaused(false);
            _paused = false;
            RestoreWindow();
            if (_runner.IsDebug) ShowDebugger(false);
            if (message == "Stopped by user.")
            {
                SetStatus("Stopped");
                SetStatusNeutral();
            }
            else
            {
                SetStatus("Error");
                SetStatusColor(Palette.Error);
            }
        });
        _runner.DebugPaused += () => Ui(() => RefreshDebugger());
    }

    private void RefreshDebugger()
    {
        var interp = _runner.Interp;
        if (interp == null) return;
        int line = interp.CurrentLine;
        ActiveEditor?.SetDebugLine(line);
        _debugger.SetPosition($"Paused at line {line}");
        _debugger.SetVariables(interp.SnapshotScope());
        _debugger.SetCallStack(interp.GetCallFrames().Select(f => $"{f.Name} — line {f.Line}"));
    }

    private void ShowDebugger(bool show)
    {
        if (show)
        {
            _sidePanel.Child = _debugger;
            _sidePanel.Visibility = Visibility.Visible;
            _debugger.Visibility = Visibility.Visible;
            _timeline.Visibility = Visibility.Collapsed;
            _debugger.Clear();
        }
        else if (_sidePanel.Child == _debugger)
        {
            _sidePanel.Visibility = Visibility.Collapsed;
            ActiveEditor?.SetDebugLine(null);
        }
    }

    private void ShowTimeline(bool show)
    {
        if (show)
        {
            _sidePanel.Child = _timeline;
            _sidePanel.Visibility = Visibility.Visible;
            _timeline.Visibility = Visibility.Visible;
            _debugger.Visibility = Visibility.Collapsed;
        }
        else if (_sidePanel.Child == _timeline && !_isRecording)
        {
            _sidePanel.Visibility = Visibility.Collapsed;
        }
    }

    private void WireTerminal()
    {
        _terminal.LineClicked += line => Ui(() => ActiveEditor?.GoToLine(line));
    }

    

    private void StartRecording()
    {
        _recorderController.CaptureMode = string.Equals(_settings.CaptureMode, "relative", StringComparison.OrdinalIgnoreCase) ? "relative" : "absolute";
        _recorderController.Start();
        if (_isRecording)
        {
            Header().SetRecording(true);
            SetStatus("Recording…  (Ctrl+Shift+Backspace to stop)");
            SetStatusColor(Palette.Error);
            ShowTimeline(true);
        }
    }

    private void StopRecording()
    {
        _recorderController.Stop();
        Header().SetRecording(false);
        SetStatus(_recording.Count > 0 ? $"Recording stopped — {_recording.Count} events" : "Recording stopped");
        SetStatusNeutral();
        ShowTimeline(true);
        RestoreWindow(); 
        if (_recording.Count > 0) ShowRecordingStoppedDialog();
    }

    
    
    
    
    private void ShowRecordingStoppedDialog()
    {
        var dlg = new RecordingStoppedDialog(_recording.Count) { Owner = this };
        dlg.PlayRequested += () => PlayEvents(_recording);
        dlg.InsertRequested += () =>
        {
            try
            {
                if (dlg.InsertAsExactCall)
                {
                    string path = AppStorage.SaveRecording(_recording);
                    string name = Path.GetFileName(path);
                    string call = $"play_recording(\"{name}\")";
                    string code = dlg.Loop
                        ? (dlg.LoopCount.HasValue
                            ? $"for _rep in range({dlg.LoopCount.Value}) {{\n    {call}\n}}\n"
                            : $"while true {{\n    {call}\n}}\n")
                        : call + "\n";
                    InsertAtCaret(code);
                    _terminal.Print($"Recording saved: {path}", "success");
                    _terminal.Print("Inserted play_recording call — replays at exact timestamps.", "success");
                }
                else
                {
                    string code = Transpiler.Transpile(_recording, dlg.Loop, dlg.LoopCount,
                        collapseTyping: dlg.CollapseTyping);
                    InsertAtCaret(code);
                    _terminal.Print($"Inserted {_recording.Count} events as editable script.", "success");
                }
            }
            catch (Exception ex)
            {
                _terminal.Print($"Insert failed: {ex.Message}", "error");
            }
        };
        dlg.ShowDialog();
        if (dlg.Inserted) ShowTimeline(false);
    }

    private void InsertAtCaret(string code)
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        int caret = ed.Editor.CaretOffset;
        ed.Text = ed.Text.Insert(Math.Min(caret, ed.Text.Length), code);
        ed.Editor.CaretOffset = Math.Min(caret + code.Length, ed.Text.Length);
    }

    private void PlayLastRecording()
    {
        if (_recording.Count == 0) return;
        PlayEvents(_recording);
    }

    private void PlayEvents(List<RecordedEvent> events)
    {
        if (_player.IsPlaying) { _player.Stop(); return; }
        SetStatus("Playing recording…");
        Header().SetRunning(true);
        _terminal.Print("Playing recording…", "info");
        _player.Play(events);
    }

    private void WirePlayer()
    {
        _player.FinishedOk += (elapsed, count) => Ui(() =>
        {
            Header().SetRunning(false);
            SetStatus($"Playback finished — {count} events · {elapsed:F2}s");
            _terminal.Print($"Playback finished in {elapsed:F2}s ({count} events)", "success");
            RestoreWindow();
        });
        _player.FinishedError += msg => Ui(() =>
        {
            Header().SetRunning(false);
            SetStatus("Playback stopped");
            _terminal.Print(msg, "warning");
            RestoreWindow();
        });
    }

    

    private System.Windows.Shapes.Ellipse? _statusDot;

    private void SetStatus(string text) => _statusText.Text = text;

    
    
    
    
    private void RefreshArmedChip()
    {
        if (!ControllerPassthrough.Active)
        {
            if (_armedChip.Visibility != Visibility.Collapsed) _armedChip.Visibility = Visibility.Collapsed;
            return;
        }
        bool hidden = ControllerPassthrough.HidHidden;
        string pad = ControllerPassthrough.VirtualPadName;
        string text = hidden
            ? "CONTROLLER ARMED" + (pad == "" ? "" : " · " + pad.ToUpperInvariant())
            : "CONTROLLER ARMED · PHYSICAL PAD VISIBLE";
        if (_armedChipText.Text != text)
        {
            _armedChipText.Text = text;
            _armedChip.Background = Palette.Brush(hidden ? Palette.Accent : Palette.Error);
        }
        if (_armedChip.Visibility != Visibility.Visible) _armedChip.Visibility = Visibility.Visible;
    }

    
    private void SetStatusColor(Color c)
    {
        _statusDot!.Fill = Palette.Brush(c);
        _statusDot.Opacity = 1;
        _statusText.Foreground = Palette.Brush(Palette.StatusTextNormal);
    }

    private void SetStatusNeutral()
    {
        _statusText.Foreground = Palette.Brush(Palette.StatusTextNormal);
        if (_statusDot != null)
        {
            _statusDot.Fill = Palette.Brush(Palette.FgMuted);
            _statusDot.Opacity = 0.7;
        }
    }

    private void RestoreWindow()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = _wasMaximized ? WindowState.Maximized : WindowState.Normal;
        }
        
        
        
        try
        {
            SwitchToThisWindow(new WindowInteropHelper(this).Handle, true);
            Activate();
        }
        catch { }
    }

    private HeaderBar? _headerBar;
    private HeaderBar Header() => _headerBar ??= ((Grid)Content).Children.OfType<HeaderBar>().First();

    

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var src = HwndSource.FromHwnd(hwnd);
        src?.AddHook(WndProc);
        RegisterHotKey(hwnd, HOTKEY_RUN, 0x0002 | 0x0004, 0x47);        
        RegisterHotKey(hwnd, HOTKEY_RECORD, 0x0002 | 0x0004, 0x52);     
        RegisterHotKey(hwnd, HOTKEY_STOP, 0x0002 | 0x0004, 0x08);       
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_RUN) Ui(() => { if (!_runner.IsRunning) StartRun(false); });
            else if (id == HOTKEY_RECORD) Ui(() => { if (_isRecording) StopRecording(); else StartRecording(); });
            else if (id == HOTKEY_STOP) Ui(StopEverything);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void UnregisterHotkeys()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(hwnd, HOTKEY_RUN);
        UnregisterHotKey(hwnd, HOTKEY_RECORD);
        UnregisterHotKey(hwnd, HOTKEY_STOP);
    }

    private void OnLocalKey(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.S: SaveFile(); e.Handled = true; break;
                case Key.N: NewFile(); e.Handled = true; break;
                case Key.O: OpenFile(); e.Handled = true; break;
                case Key.W: if (_docs.Count > 0) CloseDoc(_activeIdx); e.Handled = true; break;
                case Key.Tab: SelectDoc((_activeIdx + 1) % _docs.Count); e.Handled = true; break;
                case Key.F: if (ActiveEditor != null) { OpenFind(false); e.Handled = true; } break;
                case Key.H: if (ActiveEditor != null) { OpenFind(true); e.Handled = true; } break;
                case Key.OemQuotes: ToggleTerminalVisibility(); e.Handled = true; break;
            }
            return;
        }
        switch (e.Key)
        {
            case Key.F2: RenameDoc(_activeIdx); e.Handled = true; break;
            case Key.F5 when _runner.IsDebug:
                if (_runner.Interp != null) { _runner.DebugContinue(); e.Handled = true; }
                break;
            case Key.F9:
                var ed = ActiveEditor;
                if (ed != null) { ed.ToggleBreakpoint(ed.CaretLine); e.Handled = true; }
                break;
            case Key.F10 when _runner.IsDebug:
                _runner.DebugStepOver(); e.Handled = true;
                break;
            case Key.F11 when _runner.IsDebug && Keyboard.Modifiers == ModifierKeys.Shift:
                _runner.DebugStepOut(); e.Handled = true;
                break;
            case Key.F11 when _runner.IsDebug:
                _runner.DebugStepInto(); e.Handled = true;
                break;
        }
    }

    

    private void OpenFind(bool withReplace)
    {
        var ed = ActiveEditor;
        if (ed == null) return;
        _findBar.Open(ed, withReplace);
    }

    

    private ContextMenu BuildEditorMenu()
    {
        var menu = new ContextMenu();
        void Add(string header, Action action, bool enabled = true)
        {
            var mi = new MenuItem { Header = header, IsEnabled = enabled };
            mi.Click += (_, _) => action();
            menu.Items.Add(mi);
        }
        Add("Run Script", () => StartRun(false), enabled: ActiveDoc?.IsStartPage != true);
        Add("Debug Script", () => StartRun(true), enabled: ActiveDoc?.IsStartPage != true);
        menu.Items.Add(new Separator());
        Add("Cut", () => ActiveEditor?.Editor.Cut(), ActiveEditor != null);
        Add("Copy", () => ActiveEditor?.Editor.Copy(), ActiveEditor != null);
        Add("Paste", () => ActiveEditor?.Editor.Paste(), ActiveEditor != null);
        Add("Select All", () => ActiveEditor?.Editor.SelectAll(), ActiveEditor != null);
        menu.Items.Add(new Separator());
        Add("Find…", () => OpenFind(false), ActiveEditor != null);
        Add("Replace…", () => OpenFind(true), ActiveEditor != null);
        menu.Items.Add(new Separator());
        Add("Toggle Breakpoint (F9)", () =>
        {
            var ed = ActiveEditor;
            if (ed != null) ed.ToggleBreakpoint(ed.CaretLine);
        }, ActiveEditor != null);
        return menu;
    }

    

    private void OpenSettings()
    {
        new SettingsWindow(this, _settings).ShowDialog();
        ApplySettings();
    }

    private void ApplySettings()
    {
        foreach (var d in _docs)
        {
            d.Editor.Editor.FontSize = _settings.FontSize;
            d.Editor.SetAutoIndent(_settings.AutoIndent);
        }
    }

    private void OpenReference() => new LanguageReferenceWindow().Show();

    private void OpenRecordingsManager()
    {
        var w = new RecordingsManagerWindow
        {
            Owner = this,
        };
        w.PlayRequested += PlayEvents;
        w.CodeRequested += code =>
        {
            InsertAtCaret(code);
            _terminal.Print("Inserted recording as editable code.", "success");
        };
        w.ShowDialog();
    }

    private void Ui(Action action)
    {
        if (Dispatcher.CheckAccess()) action();
        else Dispatcher.BeginInvoke(action);
    }
}
