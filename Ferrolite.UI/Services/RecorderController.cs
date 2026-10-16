using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Ferrolite.Core.Recorder;
using Ferrolite.UI.Controls;
using Ferrolite.UI.Services;

namespace Ferrolite.UI.Views;






public class RecorderController : IDisposable
{
    private readonly Window _owner;
    private readonly TerminalPanel _terminal;
    private readonly TimelinePanel _timeline;
    private readonly Func<bool> _canStart;

    private InputRecorder? _recorder;
    private List<RecordedEvent> _recording = new();
    private DispatcherTimer? _tabOutTimer;
    private bool _isRecording;

    
    public event Action<RecordedEvent>? EventCaptured;
    public event Action<bool>? RecordingChanged;

    public bool IsRecording => _isRecording;
    public List<RecordedEvent> Events => _recording;

    
    public string CaptureMode { get; set; } = "absolute";

    public RecorderController(Window owner, TerminalPanel terminal, TimelinePanel timeline, Func<bool> canStart)
    {
        _owner = owner;
        _terminal = terminal;
        _timeline = timeline;
        _canStart = canStart;
    }

    public void Toggle()
    {
        if (_isRecording) Stop();
        else Start();
    }

    
    private static readonly HashSet<uint> HotkeyKeys = new() { 0xA2, 0xA3, 0xA0, 0xA1, 0x08 }; 

    public void Start()
    {
        if (_isRecording || !_canStart()) return;

        _recorder = new InputRecorder();
        _recording = new List<RecordedEvent>();
        _isRecording = true;
        _timeline.ClearList();

        
        
        _recorder.OnEvent += ev =>
        {
            var d = _owner.Dispatcher;
            if (d.CheckAccess()) PushLive(ev);
            else d.BeginInvoke(() => PushLive(ev));
        };
        _recorder.CaptureMode = CaptureMode;
        
        _recorder.KeyFilter = vk => HotkeyKeys.Contains(vk);
        
        
        _recorder.CapturePaused = true;

        _recorder.Start();
        if (_recorder.StartError != null)
        {
            _isRecording = false;
            RecordingChanged?.Invoke(false);
            _terminal.Print("Recording failed to start: " + _recorder.StartError, "error");
            return;
        }

        RecordingChanged?.Invoke(true);
        _terminal.Print("Recording… move/type now. Ctrl+Shift+Backspace stops.", "info");

        
        _tabOutTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _tabOutTimer.Tick += (_, _) =>
        {
            _tabOutTimer.Stop();
            if (_isRecording)
            {
                ActivateNextWindow(_owner);
                _recorder.CapturePaused = false;
            }
        };
        _tabOutTimer.Start();
    }

    private int _pendingLiveMoves;

    private void PushLive(RecordedEvent ev)
    {
        if (!_isRecording) return;
        
        
        
        if (ev.Type == EventType.MouseMove || ev.Type == EventType.MouseMoveRelative)
        {
            if (System.Threading.Interlocked.Exchange(ref _pendingLiveMoves, 1) == 1) { EventCaptured?.Invoke(ev); return; }
        }
        var d = _owner.Dispatcher;
        void Render()
        {
            _timeline.AppendLive(DescribeEvent(ev));
            if (ev.Type == EventType.MouseMove || ev.Type == EventType.MouseMoveRelative)
                System.Threading.Interlocked.Exchange(ref _pendingLiveMoves, 0);
        }
        if (d.CheckAccess()) Render();
        else d.BeginInvoke(Render);
        EventCaptured?.Invoke(ev);
    }

    public void Stop()
    {
        if (!_isRecording) return;
        _isRecording = false;
        _tabOutTimer?.Stop();
        _tabOutTimer = null;
        _recorder?.Stop();

        _recording = _recorder?.Snapshot() ?? new List<RecordedEvent>();
        RecordingChanged?.Invoke(false);

        var items = new List<TimelineItem>();
        for (int i = 0; i < _recording.Count; i++)
        {
            var it = DescribeEvent(_recording[i]);
            it.Index = i;
            items.Add(it);
        }
        _timeline.SetItems(items);
        _terminal.Print($"Recording stopped: {_recording.Count} events captured.", _recording.Count > 0 ? "success" : "warning");
    }

    
    public void Reset()
    {
        _recording = new List<RecordedEvent>();
    }

    
    
    
    
    public static void ActivateNextWindow(Window owner)
    {
        try
        {
            IntPtr ours = new System.Windows.Interop.WindowInteropHelper(owner).Handle;
            IntPtr? target = null;
            EnumWindows((h, _) =>
            {
                if (h == ours) return true;
                if (!IsWindowVisible(h) || IsIconic(h)) return true;
                if (GetWindow(h, GW_OWNER) != IntPtr.Zero) return true;
                int len = GetWindowTextLength(h);
                if (len == 0) return true;
                var sb = new System.Text.StringBuilder(len + 1);
                GetWindowText(h, sb, sb.Capacity);
                string title = sb.ToString();
                if (title.StartsWith("Program Manager") || title.StartsWith("Windows Input Experience")) return true;
                
                GetWindowThreadProcessId(h, out uint pid);
                if (pid == Environment.ProcessId) return true;

                var cb = new System.Text.StringBuilder(256);
                GetClassName(h, cb, 256);
                string cls = cb.ToString();
                if (cls is "Shell_TrayWnd" or "Progman" or "WorkerW" or "SysListView32") return true;

                target ??= h;
                return target == null;
            }, IntPtr.Zero);

            if (target != null)
            {
                
                
                SwitchToThisWindow(target.Value, true);
                SetForegroundWindow(target.Value);
            }
            else
            {
                owner.WindowState = WindowState.Minimized;
            }
        }
        catch
        {
            owner.WindowState = WindowState.Minimized; 
        }
    }

    private static TimelineItem DescribeEvent(RecordedEvent ev)
    {
        string text = ev.Type switch
        {
            EventType.MouseMove => $"MOUSE move to ({ev.X}, {ev.Y})",
            EventType.MouseClick => $"MOUSE click {ev.Button} at ({ev.X}, {ev.Y})",
            EventType.MouseDown => $"MOUSE {ev.Button} down",
            EventType.MouseUp => $"MOUSE {ev.Button} up",
            EventType.MouseScroll => $"MOUSE scroll {ev.ScrollDy}",
            EventType.KeyDown => $"KEY {ev.Key} down",
            EventType.KeyUp => $"KEY {ev.Key} up",
            EventType.ControllerButtonPress => $"PAD {ev.ControllerId} {ev.Button} pressed",
            EventType.ControllerButtonRelease => $"PAD {ev.ControllerId} {ev.Button} released",
            EventType.ControllerStickMove => $"PAD {ev.ControllerId} {ev.Button} → ({ev.StickX:F2}, {ev.StickY:F2})",
            EventType.ControllerTriggerMove => $"PAD {ev.ControllerId} {ev.Button} → {ev.TriggerValue:F2}",
            EventType.ControllerDPadMove => $"PAD {ev.ControllerId} D-pad {ev.DPadDirection}",
            _ => $"event {ev.Type}",
        };
        return new TimelineItem
        {
            Included = true,
            Text = $"[+{ev.Timestamp:F2}s] {text}",
            Timestamp = ev.Timestamp,
        };
    }

    public void Dispose()
    {
        if (_isRecording) Stop();
        _tabOutTimer?.Stop();
    }

    
    private const uint GW_OWNER = 4;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
}
