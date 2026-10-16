using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ferrolite.Core.Recorder;







public class RawMouseCapture : IDisposable
{
    private readonly Action<double, double, double> _onDelta; 
    private Thread? _thread;
    private volatile bool _running;
    private IntPtr _hwnd;
    private WNDPROC? _wndProcRef; 
    public int MessagesReceived { get; private set; }
    public string? LastError { get; private set; }

    private const uint WM_INPUT = 0x00FF;
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_DESTROY = 0x0002;
    private const ushort USAGE_PAGE_GENERIC_DESKTOP = 0x01;
    private const ushort USAGE_MOUSE = 0x02;
    private const uint RIDEV_INPUTSINK = 0x00000100;
    private const uint RID_INPUT = 0x10000003;
    private const ushort MOUSE_MOVE_ABSOLUTE_FLAG = 0x0001;

    public RawMouseCapture(Action<double, double, double> onDelta) => _onDelta = onDelta;

    public void Start()
    {
        if (_thread != null) return;
        _running = true;
        MessagesReceived = 0;
        LastError = null;
        var ready = new ManualResetEventSlim(false);
        _thread = new Thread(() => Run(ready))
        {
            IsBackground = true,
            Name = "RawMouseCapture",
            Priority = ThreadPriority.Highest, 
        };
        _thread.Start();
        if (!ready.Wait(2000))
        {
            _running = false;
            throw new InvalidOperationException("Raw mouse capture failed to initialize.");
        }
        if (LastError != null)
        {
            _running = false;
            _thread = null;
            throw new InvalidOperationException(LastError);
        }
    }

    public void Stop()
    {
        _running = false;
        if (_hwnd != IntPtr.Zero)
        {
            PostMessageW(_hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
        _thread?.Join(1000);
        _thread = null;
        _hwnd = IntPtr.Zero;
    }

    private void Run(ManualResetEventSlim ready)
    {
        try
        {
            _wndProcRef = WndProc;
            var cls = new WNDCLASSW
            {
                lpfnWndProc = _wndProcRef,
                lpszClassName = "FerroLiteRawMouseCapture",
                hInstance = GetModuleHandleW(null),
            };
            if (RegisterClassW(ref cls) == 0)
            {
                LastError = "RegisterClassW failed for raw input window.";
                ready.Set();
                return;
            }

            
            _hwnd = CreateWindowExW(0, "FerroLiteRawMouseCapture", "", 0,
                0, 0, 0, 0, (IntPtr)(-3), IntPtr.Zero, cls.hInstance, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                LastError = "CreateWindowExW failed for raw input window.";
                ready.Set();
                return;
            }

            var rid = new RAWINPUTDEVICE
            {
                usUsagePage = USAGE_PAGE_GENERIC_DESKTOP,
                usUsage = USAGE_MOUSE,
                dwFlags = RIDEV_INPUTSINK, 
                hwndTarget = _hwnd,
            };
            if (RegisterRawInputDevices(new[] { rid }, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()) == 0)
            {
                LastError = "RegisterRawInputDevices failed (mouse raw input unavailable).";
                ready.Set();
                return;
            }

            ready.Set();

            while (_running && GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
            DestroyWindow(_hwnd);
            UnregisterClassW("FerroLiteRawMouseCapture", cls.hInstance);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            ready.Set();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_INPUT)
        {
            uint size = 0;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(lParam, RID_INPUT, buffer, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) > 0)
                {
                    var raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
                    if (raw.header.dwType == 0) 
                    {
                        
                        if ((raw.mouse.usFlags & MOUSE_MOVE_ABSOLUTE_FLAG) == 0)
                        {
                            MessagesReceived++;
                            double ts = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                            _onDelta(raw.mouse.lLastX, raw.mouse.lLastY, ts);
                        }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        else if (msg == WM_DESTROY)
        {
            _running = false;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public void Dispose() => Stop();

    
    private delegate IntPtr WNDPROC(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSW
    {
        public uint style;
        public WNDPROC? lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWMOUSE
    {
        public ushort usFlags;
        public ushort usButtonFlags;
        public short usButtonData;
        public uint ulRawButtons;
        public int lLastX;
        public int lLastY;
        public uint ulExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER header;
        public RAWMOUSE mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassW(ref WNDCLASSW lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int w, int h, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort UnregisterClassW(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);
}
