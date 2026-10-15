using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ferrolite.Core.Recorder;

public enum EventType
{
    MouseMove, MouseMoveRelative, MouseClick, MouseDown, MouseUp, MouseScroll,
    KeyDown, KeyUp,
    
    ControllerButtonPress, ControllerButtonRelease, ControllerStickMove, ControllerTriggerMove, ControllerDPadMove
}

public class RecordedEvent
{
    public EventType Type { get; set; }
    public double Timestamp { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public double? Dx { get; set; }
    public double? Dy { get; set; }
    public string? Button { get; set; }
    public string? Key { get; set; }
    public int? ScrollDy { get; set; }
    public bool Included { get; set; } = true;
    
    public int? ControllerId { get; set; }
    public double? StickX { get; set; }
    public double? StickY { get; set; }
    public double? TriggerValue { get; set; }
    public string? DPadDirection { get; set; }

    
    public string TypeName => Type switch
    {
        EventType.MouseMove => "mouse_move",
        EventType.MouseMoveRelative => "mouse_move_relative",
        EventType.MouseClick => "mouse_click",
        EventType.MouseDown => "mouse_down",
        EventType.MouseUp => "mouse_up",
        EventType.MouseScroll => "mouse_scroll",
        EventType.KeyDown => "key_down",
        EventType.KeyUp => "key_up",
        EventType.ControllerButtonPress => "controller_button_press",
        EventType.ControllerButtonRelease => "controller_button_release",
        EventType.ControllerStickMove => "controller_stick_move",
        EventType.ControllerTriggerMove => "controller_trigger_move",
        EventType.ControllerDPadMove => "controller_dpad_move",
        _ => Type.ToString(),
    };

    public Dictionary<string, object?> ToDict() => new()
    {
        ["type"] = TypeName,
        ["timestamp"] = Timestamp,
        ["x"] = X, ["y"] = Y, ["dx"] = Dx, ["dy"] = Dy,
        ["button"] = Button, ["key"] = Key, ["scroll_dy"] = ScrollDy,
        ["included"] = Included,
        ["controller_id"] = ControllerId,
        ["stick_x"] = StickX, ["stick_y"] = StickY,
        ["trigger_value"] = TriggerValue, ["dpad_direction"] = DPadDirection,
    };

    private static EventType ParseTypeName(string? s) => s switch
    {
        "mouse_move" => EventType.MouseMove,
        "mouse_move_relative" => EventType.MouseMoveRelative,
        "mouse_click" => EventType.MouseClick,
        "mouse_down" => EventType.MouseDown,
        "mouse_up" => EventType.MouseUp,
        "mouse_scroll" => EventType.MouseScroll,
        "key_down" => EventType.KeyDown,
        "key_up" => EventType.KeyUp,
        "controller_button_press" => EventType.ControllerButtonPress,
        "controller_button_release" => EventType.ControllerButtonRelease,
        "controller_stick_move" => EventType.ControllerStickMove,
        "controller_trigger_move" => EventType.ControllerTriggerMove,
        "controller_dpad_move" => EventType.ControllerDPadMove,
        null => EventType.MouseMove,
        _ => Enum.TryParse<EventType>(s, out var t) ? t : EventType.MouseMove, 
    };

    public static RecordedEvent FromDict(Dictionary<string, object?> d) => new()
    {
        Type = ParseTypeName(d["type"]?.ToString()),
        Timestamp = Convert.ToDouble(d["timestamp"] ?? 0),
        X = d.ContainsKey("x") && d["x"] != null ? Convert.ToInt32(d["x"]) : null,
        Y = d.ContainsKey("y") && d["y"] != null ? Convert.ToInt32(d["y"]) : null,
        Dx = d.ContainsKey("dx") && d["dx"] != null ? Convert.ToDouble(d["dx"]) : null,
        Dy = d.ContainsKey("dy") && d["dy"] != null ? Convert.ToDouble(d["dy"]) : null,
        Button = d["button"]?.ToString(),
        Key = d["key"]?.ToString(),
        ScrollDy = d.ContainsKey("scroll_dy") && d["scroll_dy"] != null ? Convert.ToInt32(d["scroll_dy"]) : null,
        Included = d.ContainsKey("included") ? Convert.ToBoolean(d["included"]) : true,
        ControllerId = d.ContainsKey("controller_id") && d["controller_id"] != null ? Convert.ToInt32(d["controller_id"]) : null,
        StickX = d.ContainsKey("stick_x") && d["stick_x"] != null ? Convert.ToDouble(d["stick_x"]) : null,
        StickY = d.ContainsKey("stick_y") && d["stick_y"] != null ? Convert.ToDouble(d["stick_y"]) : null,
        TriggerValue = d.ContainsKey("trigger_value") && d["trigger_value"] != null ? Convert.ToDouble(d["trigger_value"]) : null,
        DPadDirection = d["dpad_direction"]?.ToString(),
    };
}

public class InputRecorder
{
    
    private const int RIM_TYPEMOUSE = 0;
    private const int RIM_TYPEKEYBOARD = 1;
    private const int WM_INPUT = 0x00FF;
    private const uint GENERIC_READWRITE = 0x80000000;
    private const int joypadDeadzone = 8000;

    public event Action<RecordedEvent>? OnEvent;
    public bool IsRecording { get; private set; }
    public List<RecordedEvent> Events { get; } = new();

    
    
 
    
    
    public bool SkipInjected { get; set; } = true;

    
    public Func<uint, bool>? KeyFilter { get; set; }

    
    
    
    
    
    public string CaptureMode { get; set; } = "absolute";

    
    private const double MouseMoveMinInterval = 0.001;
    private double _lastMoveTimeSec = -1000;
    private double _pendingDx, _pendingDy;
    private double _rawStartSeconds;
    private RawMouseCapture? _rawMouse;

    
    public string? StartError { get; private set; }

    
    
    
    
    
    public volatile bool CapturePaused;

    private IntPtr _hookMouse = IntPtr.Zero;
    private IntPtr _hookKeyboard = IntPtr.Zero;
    private HOOKPROC _mouseProcDelegate;
    private HOOKPROC _keyboardProcDelegate;
    private Thread? _controllerThread;
    private CancellationTokenSource? _cts;
    private Stopwatch _sw = new();
    private readonly object _recordLock = new();
    private int _lastMoveX = int.MinValue, _lastMoveY = int.MinValue;
    private Dictionary<int, HashSet<ushort>> _prevButtons = new();  
    private bool _controllerBaselined;
    private int _prevDPad = -1;
    private double _prevLX, _prevLY, _prevRX, _prevRY, _prevLT, _prevRT;

    
    
    
    private static int XInputGetState(int dwUserIndex, out XINPUT_STATE pState)
    {
        int rc = Ferrolite.Core.Lang.WindowsControllerBackend.XInputGetState(dwUserIndex, out var unified);
        pState = new XINPUT_STATE
        {
            dwPacketNumber = unified.dwPacketNumber,
            Gamepad = new XINPUT_GAMEPAD
            {
                wButtons = unified.Gamepad.wButtons,
                bLeftTrigger = unified.Gamepad.bLeftTrigger,
                bRightTrigger = unified.Gamepad.bRightTrigger,
                sThumbLX = unified.Gamepad.sThumbLX,
                sThumbLY = unified.Gamepad.sThumbLY,
                sThumbRX = unified.Gamepad.sThumbRX,
                sThumbRY = unified.Gamepad.sThumbRY,
            },
        };
        return rc;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    private const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
    private const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
    private const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
    private const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
    private const ushort XINPUT_GAMEPAD_A = 0x1000;
    private const ushort XINPUT_GAMEPAD_B = 0x2000;
    private const ushort XINPUT_GAMEPAD_X = 0x4000;
    private const ushort XINPUT_GAMEPAD_Y = 0x8000;
    private const ushort XINPUT_GAMEPAD_START = 0x0010;
    private const ushort XINPUT_GAMEPAD_BACK = 0x0020;
    private const ushort XINPUT_GAMEPAD_LEFT_THUMB = 0x0040;
    private const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 0x0080;
    private const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
    private const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;

    
    private delegate IntPtr HOOKPROC(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HOOKPROC lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const int WH_MOUSE_LL = 14;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WM_XBUTTONUP = 0x020C;

    public IntPtr WindowHandle { get; set; }

    
    private const uint LLKHF_INJECTED = 0x10;

    public void Start()
    {
        if (IsRecording) return;
        StartError = null;
        lock (_recordLock) Events.Clear();
        _prevButtons.Clear();
        _controllerBaselined = false;
        _prevDPad = -1;
        _lastMoveTimeSec = -1000;
        _lastMoveX = int.MinValue; _lastMoveY = int.MinValue;
        _pendingDx = 0; _pendingDy = 0;
        _prevLX = _prevLY = _prevRX = _prevRY = _prevLT = _prevRT = 0;
        _sw.Restart();
        _rawStartSeconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        
        TimerPrecision.BeginHighResTimer(1);

        
        if (CaptureMode == "relative")
        {
            try
            {
                _rawMouse = new RawMouseCapture(OnRawDelta);
                _rawMouse.Start();
            }
            catch (Exception ex)
            {
                _rawMouse = null;
                StartError = "Relative capture failed: " + ex.Message;
                TimerPrecision.EndHighResTimer();
                return; 
            }
        }

        _mouseProcDelegate = MouseHookCallback;
        _keyboardProcDelegate = KeyboardHookCallback;
        IntPtr hModule = GetModuleHandle(Process.GetCurrentProcess().MainModule!.ModuleName);
        _hookMouse = SetWindowsHookEx(WH_MOUSE_LL, _mouseProcDelegate, hModule, 0);
        _hookKeyboard = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProcDelegate, hModule, 0);

        
        if (_hookMouse == IntPtr.Zero || _hookKeyboard == IntPtr.Zero)
        {
            StartError = _hookMouse == IntPtr.Zero && _hookKeyboard == IntPtr.Zero
                ? "Windows refused both input hooks (another tool may have them, or UIPI blocked the request)."
                : _hookMouse == IntPtr.Zero
                    ? "Mouse hook could not be installed — mouse will not be recorded."
                    : "Keyboard hook could not be installed — keys will not be recorded.";
            if (_hookMouse != IntPtr.Zero) { UnhookWindowsHookEx(_hookMouse); _hookMouse = IntPtr.Zero; }
            if (_hookKeyboard != IntPtr.Zero) { UnhookWindowsHookEx(_hookKeyboard); _hookKeyboard = IntPtr.Zero; }
            return; 
        }

        IsRecording = true;

        
        _cts = new CancellationTokenSource();
        _controllerThread = new Thread(ControllerPollLoop) { IsBackground = true };
        _controllerThread.Start();
    }

    public void Stop()
    {
        if (!IsRecording) return;
        IsRecording = false;
        _sw.Stop();
        if (_hookMouse != IntPtr.Zero) { UnhookWindowsHookEx(_hookMouse); _hookMouse = IntPtr.Zero; }
        if (_hookKeyboard != IntPtr.Zero) { UnhookWindowsHookEx(_hookKeyboard); _hookKeyboard = IntPtr.Zero; }
        _rawMouse?.Stop();
        _rawMouse = null;
        TimerPrecision.EndHighResTimer();
        _cts?.Cancel();
        try { _controllerThread?.Join(300); } catch { }
        _controllerThread = null;
    }

    private double Elapsed => _sw.Elapsed.TotalSeconds;

    private void Record(RecordedEvent e)
    {
        if (CapturePaused) return;
        lock (_recordLock) Events.Add(e);
        OnEvent?.Invoke(e);
    }

    private void RecordClick(EventType type, MSLLHOOKSTRUCT ms, string button)
    {
        bool coords = CaptureMode != "relative"; 
        Record(new RecordedEvent { Type = type, Timestamp = Elapsed, X = coords ? ms.pt.X : null, Y = coords ? ms.pt.Y : null, Button = button });
    }

    
    private void OnRawDelta(double dx, double dy, double ts)
    {
        if (!IsRecording || CapturePaused) return;
        _pendingDx += dx;
        _pendingDy += dy;
        if (ts - _lastMoveTimeSec < MouseMoveMinInterval) return;
        _lastMoveTimeSec = ts;
        Record(new RecordedEvent { Type = EventType.MouseMoveRelative, Timestamp = ts - _rawStartSeconds, Dx = _pendingDx, Dy = _pendingDy });
        _pendingDx = 0;
        _pendingDy = 0;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            
            if (SkipInjected && (ms.flags & LLKHF_INJECTED) != 0)
                return CallNextHookEx(_hookMouse, nCode, wParam, lParam);

            int msg = wParam.ToInt32();
            switch (msg)
            {
                case WM_MOUSEMOVE:
                    
                    
                    if (CaptureMode == "relative")
                        break;
                    
                    double nowSec = Elapsed;
                    bool moved = ms.pt.X != _lastMoveX || ms.pt.Y != _lastMoveY;
                    if (moved && nowSec - _lastMoveTimeSec >= MouseMoveMinInterval)
                    {
                        _lastMoveTimeSec = nowSec;
                        _lastMoveX = ms.pt.X; _lastMoveY = ms.pt.Y;
                        Record(new RecordedEvent { Type = EventType.MouseMove, Timestamp = nowSec, X = ms.pt.X, Y = ms.pt.Y });
                    }
                    break;
                case WM_LBUTTONDOWN: RecordClick(EventType.MouseDown, ms, "left"); break;
                case WM_LBUTTONUP: RecordClick(EventType.MouseUp, ms, "left"); break;
                case WM_RBUTTONDOWN: RecordClick(EventType.MouseDown, ms, "right"); break;
                case WM_RBUTTONUP: RecordClick(EventType.MouseUp, ms, "right"); break;
                case WM_MBUTTONDOWN: RecordClick(EventType.MouseDown, ms, "middle"); break;
                case WM_MBUTTONUP: RecordClick(EventType.MouseUp, ms, "middle"); break;
                case WM_MOUSEWHEEL:
                    int delta = (short)((ms.mouseData >> 16) & 0xFFFF);
                    
                    bool coords = CaptureMode != "relative";
                    Record(new RecordedEvent { Type = EventType.MouseScroll, Timestamp = Elapsed, X = coords ? ms.pt.X : null, Y = coords ? ms.pt.Y : null, ScrollDy = delta / 120 });
                    break;
                case WM_XBUTTONDOWN:
                    int xbtn = (int)((ms.mouseData >> 16) & 0xFFFF);
                    
                    
                    RecordClick(EventType.MouseDown, ms, xbtn == 1 ? "mb4" : "mb5");
                    break;
                case WM_XBUTTONUP:
                    int xbtnUp = (int)((ms.mouseData >> 16) & 0xFFFF);
                    RecordClick(EventType.MouseUp, ms, xbtnUp == 1 ? "mb4" : "mb5");
                    break;
            }
        }
        return CallNextHookEx(_hookMouse, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (SkipInjected && (kb.flags & LLKHF_INJECTED) != 0)
                return CallNextHookEx(_hookKeyboard, nCode, wParam, lParam);
            
            if (KeyFilter != null && KeyFilter(kb.vkCode))
                return CallNextHookEx(_hookKeyboard, nCode, wParam, lParam);

            var kb2 = kb;
            bool isUp = wParam.ToInt32() == 0x0101 || wParam.ToInt32() == 0x0105; 
            string key = ((VirtualKey)kb2.vkCode).ToString().ToLower();
            if (kb2.vkCode >= 0x30 && kb2.vkCode <= 0x39) key = ((char)kb2.vkCode).ToString();
            else if (kb2.vkCode >= 0x41 && kb2.vkCode <= 0x5A) key = ((char)(kb2.vkCode + 32)).ToString();

            Record(new RecordedEvent
            {
                Type = isUp ? EventType.KeyUp : EventType.KeyDown,
                Timestamp = Elapsed,
                Key = key
            });
        }
        return CallNextHookEx(_hookKeyboard, nCode, wParam, lParam);
    }

    private void ControllerPollLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            for (int i = 0; i < 4; i++)
            {
                if (XInputGetState(i, out var state) != 0) continue;

                var gp = state.Gamepad;

                
                HashSet<ushort> pressed = new();
                if ((gp.wButtons & XINPUT_GAMEPAD_A) != 0) pressed.Add(XINPUT_GAMEPAD_A);
                if ((gp.wButtons & XINPUT_GAMEPAD_B) != 0) pressed.Add(XINPUT_GAMEPAD_B);
                if ((gp.wButtons & XINPUT_GAMEPAD_X) != 0) pressed.Add(XINPUT_GAMEPAD_X);
                if ((gp.wButtons & XINPUT_GAMEPAD_Y) != 0) pressed.Add(XINPUT_GAMEPAD_Y);
                if ((gp.wButtons & XINPUT_GAMEPAD_START) != 0) pressed.Add(XINPUT_GAMEPAD_START);
                if ((gp.wButtons & XINPUT_GAMEPAD_BACK) != 0) pressed.Add(XINPUT_GAMEPAD_BACK);
                if ((gp.wButtons & XINPUT_GAMEPAD_LEFT_THUMB) != 0) pressed.Add(XINPUT_GAMEPAD_LEFT_THUMB);
                if ((gp.wButtons & XINPUT_GAMEPAD_RIGHT_THUMB) != 0) pressed.Add(XINPUT_GAMEPAD_RIGHT_THUMB);
                if ((gp.wButtons & XINPUT_GAMEPAD_LEFT_SHOULDER) != 0) pressed.Add(XINPUT_GAMEPAD_LEFT_SHOULDER);
                if ((gp.wButtons & XINPUT_GAMEPAD_RIGHT_SHOULDER) != 0) pressed.Add(XINPUT_GAMEPAD_RIGHT_SHOULDER);

                if (!_prevButtons.TryGetValue(i, out var prevSet))
                {
                    
                    _prevButtons[i] = pressed;
                }
                else
                {
                    string ButtonName(ushort b) => b switch
                    {
                        XINPUT_GAMEPAD_A => "gamepad_a",
                        XINPUT_GAMEPAD_B => "gamepad_b",
                        XINPUT_GAMEPAD_X => "gamepad_x",
                        XINPUT_GAMEPAD_Y => "gamepad_y",
                        XINPUT_GAMEPAD_START => "gamepad_start",
                        XINPUT_GAMEPAD_BACK => "gamepad_back",
                        XINPUT_GAMEPAD_LEFT_THUMB => "gamepad_l3",
                        XINPUT_GAMEPAD_RIGHT_THUMB => "gamepad_r3",
                        XINPUT_GAMEPAD_LEFT_SHOULDER => "gamepad_lb",
                        XINPUT_GAMEPAD_RIGHT_SHOULDER => "gamepad_rb",
                        _ => "unknown"
                    };

                    foreach (var b in pressed)
                        if (!prevSet.Contains(b))
                            Record(new RecordedEvent { Type = EventType.ControllerButtonPress, Timestamp = Elapsed, ControllerId = i, Button = ButtonName(b) });
                    foreach (var b in prevSet)
                        if (!pressed.Contains(b))
                            Record(new RecordedEvent { Type = EventType.ControllerButtonRelease, Timestamp = Elapsed, ControllerId = i, Button = ButtonName(b) });
                    _prevButtons[i] = pressed;
                }

                
                int dpad = gp.wButtons & 0xF;
                if (!_controllerBaselined)
                {
                    _prevDPad = dpad; 
                }
                else if (dpad != _prevDPad && i == 0)
                {
                    string dir = dpad switch
                    {
                        1 => "up", 2 => "down", 4 => "left", 8 => "right",
                        5 => "up_left", 6 => "down_right", 9 => "up_right", 10 => "down_left",
                        _ => "center"
                    };
                    if (dir != "center")
                        Record(new RecordedEvent { Type = EventType.ControllerDPadMove, Timestamp = Elapsed, ControllerId = i, DPadDirection = dir });
                    _prevDPad = dpad;
                }

                
                
                double lx = gp.sThumbLX / 32767.0, ly = gp.sThumbLY / 32767.0;
                double rx = gp.sThumbRX / 32767.0, ry = gp.sThumbRY / 32767.0;
                if (Math.Sqrt(lx * lx + ly * ly) > 0.02)
                {
                    if (Math.Abs(lx - _prevLX) > 0.01 || Math.Abs(ly - _prevLY) > 0.01)
                    {
                        Record(new RecordedEvent { Type = EventType.ControllerStickMove, Timestamp = Elapsed, ControllerId = i, StickX = lx, StickY = ly, Button = "left_stick" });
                        _prevLX = lx; _prevLY = ly;
                    }
                }
                else if (Math.Sqrt(_prevLX * _prevLX + _prevLY * _prevLY) > 0.02)
                {
                    Record(new RecordedEvent { Type = EventType.ControllerStickMove, Timestamp = Elapsed, ControllerId = i, StickX = 0.0, StickY = 0.0, Button = "left_stick" });
                    _prevLX = 0; _prevLY = 0;
                }

                if (Math.Sqrt(rx * rx + ry * ry) > 0.02)
                {
                    if (Math.Abs(rx - _prevRX) > 0.01 || Math.Abs(ry - _prevRY) > 0.01)
                    {
                        Record(new RecordedEvent { Type = EventType.ControllerStickMove, Timestamp = Elapsed, ControllerId = i, StickX = rx, StickY = ry, Button = "right_stick" });
                        _prevRX = rx; _prevRY = ry;
                    }
                }
                else if (Math.Sqrt(_prevRX * _prevRX + _prevRY * _prevRY) > 0.02)
                {
                    Record(new RecordedEvent { Type = EventType.ControllerStickMove, Timestamp = Elapsed, ControllerId = i, StickX = 0.0, StickY = 0.0, Button = "right_stick" });
                    _prevRX = 0; _prevRY = 0;
                }

                
                double lt = gp.bLeftTrigger / 255.0;
                double rt = gp.bRightTrigger / 255.0;
                if (Math.Abs(lt - _prevLT) > 0.02)
                {
                    Record(new RecordedEvent { Type = EventType.ControllerTriggerMove, Timestamp = Elapsed, ControllerId = i, TriggerValue = lt, Button = "left_trigger" });
                    _prevLT = lt;
                }
                if (Math.Abs(rt - _prevRT) > 0.02)
                {
                    Record(new RecordedEvent { Type = EventType.ControllerTriggerMove, Timestamp = Elapsed, ControllerId = i, TriggerValue = rt, Button = "right_trigger" });
                    _prevRT = rt;
                }
            }
            _controllerBaselined = true;
            Thread.Sleep(8); 
        }
    }

    
    public List<RecordedEvent> Snapshot()
    {
        lock (_recordLock) return new List<RecordedEvent>(Events);
    }
}

public enum VirtualKey : uint
{
    LBUTTON = 0x01, RBUTTON = 0x02, CANCEL = 0x03, MBUTTON = 0x04,
    XBUTTON1 = 0x05, XBUTTON2 = 0x06, BACK = 0x08, TAB = 0x09,
    CLEAR = 0x0C, RETURN = 0x0D, SHIFT = 0x10, CONTROL = 0x11, MENU = 0x12,
    PAUSE = 0x13, CAPITAL = 0x14, ESCAPE = 0x1B, SPACE = 0x20,
    PRIOR = 0x21, NEXT = 0x22, END = 0x23, HOME = 0x24,
    LEFT = 0x25, UP = 0x26, RIGHT = 0x27, DOWN = 0x28,
    SNAPSHOT = 0x2C, INSERT = 0x2D, DELETE = 0x2E,
    KEY_0 = 0x30, KEY_1 = 0x31, KEY_2 = 0x32, KEY_3 = 0x33, KEY_4 = 0x34,
    KEY_5 = 0x35, KEY_6 = 0x36, KEY_7 = 0x37, KEY_8 = 0x38, KEY_9 = 0x39,
    A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45, F = 0x46, G = 0x47,
    H = 0x48, I = 0x49, J = 0x4A, K = 0x4B, L = 0x4C, M = 0x4D, N = 0x4E,
    O = 0x4F, P = 0x50, Q = 0x51, R = 0x52, S = 0x53, T = 0x54, U = 0x55,
    V = 0x56, W = 0x57, X = 0x58, Y = 0x59, Z = 0x5A,
    LWIN = 0x5B, RWIN = 0x5C, APPS = 0x5D, NUMPAD0 = 0x60,
    F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73, F5 = 0x74, F6 = 0x75,
    F7 = 0x76, F8 = 0x77, F9 = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B,
    NUMLOCK = 0x90, SCROLL = 0x91,
    LSHIFT = 0xA0, RSHIFT = 0xA1, LCONTROL = 0xA2, RCONTROL = 0xA3,
    LMENU = 0xA4, RMENU = 0xA5,
}
