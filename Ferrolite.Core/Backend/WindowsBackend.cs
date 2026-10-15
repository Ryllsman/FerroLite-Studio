using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace Ferrolite.Core.Lang;

public class WindowsBackend : IActionBackend
{
    
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_XUP = 0x0100;
    private const uint MOUSEEVENTF_XDOWN = 0x0080;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const int WHEEL_DELTA = 120;

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] private static extern short GetKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool CloseWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern UIntPtr GlobalSize(IntPtr hMem);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

    private const uint SRCCOPY = 0x00CC0020;

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    private static class Gdi32
    {
        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool BitBlt(IntPtr hdc, int x, int y, int nWidth, int nHeight, IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool DeleteObject(IntPtr hObject);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    
    private static readonly Dictionary<string, ushort> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["space"] = 0x20, ["spacebar"] = 0x20,
        ["enter"] = 0x0D, ["return"] = 0x0D,
        ["tab"] = 0x09,
        ["escape"] = 0x1B, ["esc"] = 0x1B,
        ["backspace"] = 0x08,
        ["delete"] = 0x2E, ["del"] = 0x2E,
        ["insert"] = 0x2D, ["ins"] = 0x2D,
        ["home"] = 0x24, ["end"] = 0x23,
        ["pageup"] = 0x21, ["pgup"] = 0x21,
        ["pagedown"] = 0x22, ["pgdn"] = 0x22,
        ["up"] = 0x26, ["down"] = 0x28, ["left"] = 0x25, ["right"] = 0x27,
        ["caps"] = 0x14, ["capslock"] = 0x14,
        ["numlock"] = 0x90, ["scrolllock"] = 0x91,
        ["pause"] = 0x13, ["printscreen"] = 0x2C, ["prtsc"] = 0x2C,
        ["shift"] = 0xA0, ["ctrl"] = 0xA2, ["control"] = 0xA2,
        ["alt"] = 0xA4, ["win"] = 0x5B, ["windows"] = 0x5B, ["super"] = 0x5B, ["cmd"] = 0x5B,
        ["lshift"] = 0xA0, ["rshift"] = 0xA1,
        ["lctrl"] = 0xA2, ["rctrl"] = 0xA3,
        ["lalt"] = 0xA4, ["ralt"] = 0xA5,
        ["f1"] = 0x70, ["f2"] = 0x71, ["f3"] = 0x72, ["f4"] = 0x73,
        ["f5"] = 0x74, ["f6"] = 0x75, ["f7"] = 0x76, ["f8"] = 0x77,
        ["f9"] = 0x78, ["f10"] = 0x79, ["f11"] = 0x7A, ["f12"] = 0x7B,
        
        
        
        ["["] = 0xDB, ["lbracket"] = 0xDB, ["oem4"] = 0xDB,
        ["]"] = 0xDD, ["rbracket"] = 0xDD, ["oem6"] = 0xDD,
        [";"] = 0xBA, ["semicolon"] = 0xBA, ["oem1"] = 0xBA,
        ["'"] = 0xDE, ["quote"] = 0xDE, ["oem7"] = 0xDE,
        [","] = 0xBC, ["comma"] = 0xBC, ["oemcomma"] = 0xBC,
        ["."] = 0xBE, ["period"] = 0xBE, ["oemperiod"] = 0xBE,
        ["/"] = 0xBF, ["slash"] = 0xBF, ["oem2"] = 0xBF,
        ["\\"] = 0xDC, ["backslash"] = 0xDC, ["oem5"] = 0xDC,
        ["-"] = 0xBD, ["minus"] = 0xBD, ["oemminus"] = 0xBD,
        ["="] = 0xBB, ["equals"] = 0xBB, ["oemplus"] = 0xBB, ["plus"] = 0xBB,
        ["`"] = 0xC0, ["backquote"] = 0xC0, ["backtick"] = 0xC0, ["oem3"] = 0xC0,
    };

    
    
    
    
    private static readonly Dictionary<string, int> MouseButtonFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["left"] = 0,
        ["right"] = 1,
        ["middle"] = 2,

        ["side_button_near"] = 3,
        ["mb4"] = 3,
        ["mouse4"] = 3, ["xbutton1"] = 3, ["x1"] = 3, ["extra1"] = 3,

        ["side_button_far"] = 4,
        ["mb5"] = 4,
        ["mouse5"] = 4, ["xbutton2"] = 4, ["x2"] = 4, ["extra2"] = 4,
    };

    [DllImport("user32.dll")] private static extern short VkKeyScanW(char ch);

    private ushort ResolveKey(string key)
    {
        if (NamedKeys.TryGetValue(key, out ushort vk)) return vk;
        string lower = key.ToLowerInvariant();
        if (NamedKeys.TryGetValue(lower, out vk)) return vk;
        if (key.Length == 1)
        {
            char c = key[0];
            
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) return (ushort)c;
            if (char.ToLower(c) >= 'a' && char.ToLower(c) <= 'z') return (ushort)char.ToUpper(c);
            
            
            short scanned = VkKeyScanW(c);
            if (scanned != -1) return (ushort)(scanned & 0xFF);
        }
        
        
        throw new Ferrolite.Core.Lang.VSRuntimeError(
            $"Unknown key name \"{key}\". Use a single character (like \"a\" or \"[\") or one of: " +
            string.Join(", ", NamedKeys.Keys.OrderBy(k => k).Take(60)) + " …");
    }

    private void SendInput(INPUT input)
    {
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private void SendKey(ushort vk, bool keyUp = false)
    {
        var input = new INPUT { type = INPUT_KEYBOARD };
        input.union.ki.wVk = vk;
        input.union.ki.wScan = (ushort)MapVirtualKey(vk, 0);
        input.union.ki.dwFlags = keyUp ? KEYEVENTF_KEYUP : 0;
        input.union.ki.time = 0;
        input.union.ki.dwExtraInfo = IntPtr.Zero;
        SendInput(input);
    }

    private void SendMouseButton(int buttonIndex, bool down)
    {
        uint flags = buttonIndex switch
        {
            0 => down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP,
            1 => down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
            2 => down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
            3 or 4 => down ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP,
            _ => down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP,
        };
        uint data = buttonIndex >= 3 ? (uint)(buttonIndex - 2) : 0;
        mouse_event(flags, 0, 0, data, IntPtr.Zero);
    }

    
    public void MouseMove(int x, int y, double duration)
    {
        if (duration > 0)
        {
            GetCursorPos(out POINT start);
            int steps = Math.Max(1, (int)(duration / 0.008));
            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps;
                int nx = start.X + (int)Math.Round((x - start.X) * t);
                int ny = start.Y + (int)Math.Round((y - start.Y) * t);
                SetCursorPos(nx, ny);
                if (i < steps) Thread.Sleep(TimeSpan.FromSeconds(duration / steps));
            }
        }
        SetCursorPos(x, y);
    }

    
    
    
    public void MouseMoveRelative(double dx, double dy, double duration, int? steps)
    {
        int stepsCount = steps.HasValue ? Math.Max(1, steps.Value)
            : duration > 0 ? Math.Max(1, (int)Math.Round(duration / 0.008)) : 1;
        double remX = dx, remY = dy;
        for (int i = 0; i < stepsCount; i++)
        {
            int stepX = i == stepsCount - 1 ? (int)Math.Round(remX) : (int)Math.Round(dx / stepsCount);
            int stepY = i == stepsCount - 1 ? (int)Math.Round(remY) : (int)Math.Round(dy / stepsCount);
            if (stepX == 0 && stepY == 0 && i < stepsCount - 1) { remX -= stepX; remY -= stepY; continue; }
            var input = new INPUT { type = INPUT_MOUSE };
            input.union.mi.dx = stepX;
            input.union.mi.dy = stepY;
            input.union.mi.dwFlags = MOUSEEVENTF_MOVE;
            SendInput(input);
            remX -= stepX; remY -= stepY;
            if (stepsCount > 1 && i < stepsCount - 1)
                Thread.Sleep(TimeSpan.FromSeconds(duration / stepsCount));
        }
    }

    public void MouseClick(int? x, int? y, string button)
    {
        if (x.HasValue && y.HasValue) SetCursorPos(x.Value, y.Value);
        int btnIdx = MouseButtonFlags.GetValueOrDefault(button, 0);
        if (btnIdx >= 3)
        {
            SendMouseButton(btnIdx, true);
            SendMouseButton(btnIdx, false);
        }
        else
        {
            SendMouseButton(btnIdx, true);
            SendMouseButton(btnIdx, false);
        }
    }

    public void MouseDoubleClick(int? x, int? y, string button)
    {
        MouseClick(x, y, button);
        Thread.Sleep(50);
        MouseClick(null, null, button);
    }

    public void MouseDown(string button)
    {
        int btnIdx = MouseButtonFlags.GetValueOrDefault(button, 0);
        SendMouseButton(btnIdx, true);
    }

    public void MouseUp(string button)
    {
        int btnIdx = MouseButtonFlags.GetValueOrDefault(button, 0);
        SendMouseButton(btnIdx, false);
    }

    public void MouseDrag(int x1, int y1, int x2, int y2, double duration, string button)
    {
        SetCursorPos(x1, y1);
        MouseDown(button);
        Thread.Sleep(50);
        int steps = Math.Max(1, (int)(duration / 0.008));
        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            SetCursorPos(x1 + (int)((x2 - x1) * t), y1 + (int)((y2 - y1) * t));
            Thread.Sleep(TimeSpan.FromSeconds(duration / steps));
        }
        MouseUp(button);
    }

    public void MouseScroll(int amount, int? x, int? y)
    {
        if (x.HasValue && y.HasValue) SetCursorPos(x.Value, y.Value);
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)(amount * WHEEL_DELTA), IntPtr.Zero);
    }

    public (int X, int Y) MousePosition()
    {
        GetCursorPos(out POINT p);
        return (p.X, p.Y);
    }

    public bool IsKeyDown(string key)
    {
        ushort vk = ResolveKey(key);
        return (GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    public bool IsKeyToggled(string key)
    {
        ushort vk = ResolveKey(key);
        
        
        
        
        
        return (GetKeyState(vk) & 0x0001) != 0;
    }

    public bool IsMouseDown(string button)
    {
        
        
        
        
        
        int btnIdx = MouseButtonFlags.GetValueOrDefault(button, 0);
        int vk = btnIdx switch
        {
            0 => 0x01,   
            1 => 0x02,   
            2 => 0x04,   
            3 => 0x05,   
            4 => 0x06,   
            _ => 0x01,
        };
        return (GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    
    public void KeyPress(string key)
    {
        ushort vk = ResolveKey(key);
        if (vk == 0) return;
        SendKey(vk, false);
        Thread.Sleep(30);
        SendKey(vk, true);
    }

    public void KeyDown(string key)
    {
        ushort vk = ResolveKey(key);
        if (vk != 0) SendKey(vk, false);
    }

    public void KeyUp(string key)
    {
        ushort vk = ResolveKey(key);
        if (vk != 0) SendKey(vk, true);
    }

    public void TypeText(string text, double interval)
    {
        foreach (char c in text)
        {
            var input = new INPUT { type = INPUT_KEYBOARD };
            input.union.ki.wVk = 0;
            input.union.ki.wScan = (ushort)c;
            input.union.ki.dwFlags = 0x0004; 
            SendInput(input);
            Thread.Sleep(10);
            input.union.ki.dwFlags = 0x0004 | KEYEVENTF_KEYUP;
            SendInput(input);
            if (interval > 0) Thread.Sleep(TimeSpan.FromSeconds(interval));
        }
    }

    public void Hotkey(List<string> keys)
    {
        var vks = keys.Select(ResolveKey).Where(v => v != 0).ToList();
        foreach (var vk in vks) SendKey(vk, false);
        foreach (var vk in vks.AsEnumerable().Reverse()) SendKey(vk, true);
    }

    
    public bool FocusWindow(string title)
    {
        IntPtr hWnd = FindWindow(null, title);
        if (hWnd == IntPtr.Zero) return false;
        SetForegroundWindow(hWnd);
        return true;
    }

    public object? FindWindow(string title)
    {
        IntPtr hWnd = FindWindow(null, title);
        if (hWnd == IntPtr.Zero) return null;
        int len = GetWindowTextLength(hWnd);
        var sb = new System.Text.StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        GetWindowRect(hWnd, out RECT rect);
        return new Dictionary<string, object>
        {
            ["title"] = sb.ToString(),
            ["x"] = rect.Left, ["y"] = rect.Top,
            ["width"] = rect.Right - rect.Left, ["height"] = rect.Bottom - rect.Top
        };
    }

    public object ListWindows()
    {
        var windows = new List<object>();
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            int len = GetWindowTextLength(hWnd);
            if (len == 0) return true;
            var sb = new System.Text.StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title)) return true;
            GetWindowRect(hWnd, out RECT rect);
            windows.Add(new Dictionary<string, object>
            {
                ["title"] = title,
                ["x"] = rect.Left, ["y"] = rect.Top,
                ["width"] = rect.Right - rect.Left, ["height"] = rect.Bottom - rect.Top
            });
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public void MinimizeWindow(string title)
    {
        IntPtr hWnd = FindWindow(null, title);
        if (hWnd != IntPtr.Zero) ShowWindow(hWnd, 6); 
    }

    public void MaximizeWindow(string title)
    {
        IntPtr hWnd = FindWindow(null, title);
        if (hWnd != IntPtr.Zero) ShowWindow(hWnd, 3); 
    }

    public void CloseWindow(string title)
    {
        IntPtr hWnd = FindWindow(null, title);
        if (hWnd != IntPtr.Zero) CloseWindow(hWnd);
    }

    public object? GetActiveWindow()
    {
        IntPtr hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return null;
        int len = GetWindowTextLength(hWnd);
        var sb = new System.Text.StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        GetWindowRect(hWnd, out RECT rect);
        return new Dictionary<string, object>
        {
            ["title"] = sb.ToString(),
            ["x"] = rect.Left, ["y"] = rect.Top,
            ["width"] = rect.Right - rect.Left, ["height"] = rect.Bottom - rect.Top
        };
    }

    public void MoveWindow(string title, int x, int y, int? w, int? h)
    {
        IntPtr hWnd = FindWindow(null, title);
        if (hWnd == IntPtr.Zero) return;
        GetWindowRect(hWnd, out RECT rect);
        MoveWindow(hWnd, x, y, w ?? rect.Right - rect.Left, h ?? rect.Bottom - rect.Top, true);
    }

    
    public List<object> GetPixelColor(int x, int y)
    {
        IntPtr hdc = GetDC(IntPtr.Zero);
        uint color = GetPixel(hdc, x, y);
        ReleaseDC(IntPtr.Zero, hdc);
        return new List<object> { (double)((color >> 16) & 0xFF), (double)((color >> 8) & 0xFF), (double)(color & 0xFF) };
    }

    public (int Width, int Height) ScreenSize()
    {
        return (GetSystemMetrics(0), GetSystemMetrics(1)); 
    }

    public string Screenshot(string? path)
    {
        
        string savePath = path ?? Path.Combine(Path.GetTempPath(), $"ferrolite_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        try
        {
            var (w, h) = ScreenSize();
            IntPtr hdcSrc = GetDC(IntPtr.Zero);
            IntPtr hdcDest = Gdi32.CreateCompatibleDC(hdcSrc);
            IntPtr hBmp = Gdi32.CreateCompatibleBitmap(hdcSrc, w, h);
            IntPtr hOld = Gdi32.SelectObject(hdcDest, hBmp);
            Gdi32.BitBlt(hdcDest, 0, 0, w, h, hdcSrc, 0, 0, SRCCOPY);
            Gdi32.SelectObject(hdcDest, hOld);
            Gdi32.DeleteDC(hdcDest);
            ReleaseDC(IntPtr.Zero, hdcSrc);

            using var bmp = Bitmap.FromHbitmap(hBmp);
            bmp.Save(savePath, System.Drawing.Imaging.ImageFormat.Png);
            Gdi32.DeleteObject(hBmp);
        }
        catch
        {
            
            if (!File.Exists(savePath))
                File.WriteAllText(savePath, string.Empty);
        }
        return savePath;
    }

    public object? FindImageOnScreen(string imagePath, double confidence)
    {
        if (!File.Exists(imagePath))
            return null;
        try
        {
            var (sw, sh) = ScreenSize();
            IntPtr hdcSrc = GetDC(IntPtr.Zero);
            IntPtr hdcDest = Gdi32.CreateCompatibleDC(hdcSrc);
            IntPtr hBmp = Gdi32.CreateCompatibleBitmap(hdcSrc, sw, sh);
            IntPtr hOld = Gdi32.SelectObject(hdcDest, hBmp);
            Gdi32.BitBlt(hdcDest, 0, 0, sw, sh, hdcSrc, 0, 0, SRCCOPY);
            Gdi32.SelectObject(hdcDest, hOld);
            Gdi32.DeleteDC(hdcDest);
            ReleaseDC(IntPtr.Zero, hdcSrc);

            using var screenBmp = Bitmap.FromHbitmap(hBmp);
            Gdi32.DeleteObject(hBmp);
            return MatchOnBitmap(screenBmp, imagePath, sw, sh, confidence);
        }
        catch
        {
            return null;
        }
    }

    
    
    
    
    public object? FindImageOnScreen(Bitmap screenBmp, string imagePath, double confidence)
    {
        if (!File.Exists(imagePath))
            return null;
        try
        {
            var (sw, sh) = (screenBmp.Width, screenBmp.Height);
            return MatchOnBitmap(screenBmp, imagePath, sw, sh, confidence);
        }
        catch
        {
            return null;
        }
    }

    private object? MatchOnBitmap(Bitmap screenBmp, string imagePath, int sw, int sh, double confidence)
    {
        using var template = new Bitmap(imagePath);
        if (template.Width > sw || template.Height > sh)
            return null;

        using var screenMat = OpenCvSharp.Extensions.BitmapConverter.ToMat(screenBmp);
        using var tmplMat = OpenCvSharp.Extensions.BitmapConverter.ToMat(template);
        using var result = new Mat();
        Cv2.MatchTemplate(screenMat, tmplMat, result, TemplateMatchModes.SqDiffNormed);
        Cv2.MinMaxLoc(result, out double minVal, out _, out OpenCvSharp.Point minLoc, out _);

        
        if (minVal <= (1.0 - confidence))
        {
            return new Dictionary<string, object>
            {
                ["x"] = minLoc.X, ["y"] = minLoc.Y,
                ["width"] = template.Width, ["height"] = template.Height,
                ["center_x"] = minLoc.X + template.Width / 2,
                ["center_y"] = minLoc.Y + template.Height / 2
            };
        }
        return null;
    }

    public bool WaitForPixelColor(int x, int y, List<object> color, double timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < timeout)
        {
            var actual = GetPixelColor(x, y);
            if (actual.Count >= 3 &&
                Math.Abs(Convert.ToDouble(actual[0]) - Convert.ToDouble(color[0])) < 5 &&
                Math.Abs(Convert.ToDouble(actual[1]) - Convert.ToDouble(color[1])) < 5 &&
                Math.Abs(Convert.ToDouble(actual[2]) - Convert.ToDouble(color[2])) < 5)
                return true;
            Thread.Sleep(50);
        }
        return false;
    }

    
    public string ClipboardGet()
    {
        if (!OpenClipboard(IntPtr.Zero)) return "";
        try
        {
            IntPtr hData = GetClipboardData(13); 
            if (hData == IntPtr.Zero) return "";
            IntPtr pText = GlobalLock(hData);
            if (pText == IntPtr.Zero) return "";
            string text = Marshal.PtrToStringUni(pText) ?? "";
            GlobalUnlock(hData);
            return text;
        }
        finally { CloseClipboard(); }
    }

    public void ClipboardSet(string text)
    {
        if (!OpenClipboard(IntPtr.Zero)) return;
        try
        {
            EmptyClipboard();
            int bytes = (text.Length + 1) * 2;
            IntPtr hGlobal = GlobalAlloc(0x0042, (UIntPtr)bytes); 
            IntPtr pGlobal = GlobalLock(hGlobal);
            Marshal.Copy(text.ToCharArray(), 0, pGlobal, text.Length);
            Marshal.WriteInt16(pGlobal + text.Length * 2, 0);
            GlobalUnlock(hGlobal);
            SetClipboardData(13, hGlobal); 
        }
        finally { CloseClipboard(); }
    }

    
    public object RunProcess(string path, List<string> args, bool wait)
    {
        try
        {
            var psi = new ProcessStartInfo(path)
            {
                Arguments = string.Join(" ", args.Select(a => $"\"{a}\"")),
                UseShellExecute = true,
            };
            var proc = Process.Start(psi);
            if (wait && proc != null) proc.WaitForExit();
            return new Dictionary<string, object> { ["pid"] = proc?.Id ?? -1, ["started"] = proc != null };
        }
        catch (Exception e)
        {
            return new Dictionary<string, object> { ["pid"] = -1, ["started"] = false, ["error"] = e.Message };
        }
    }

    public bool KillProcess(string nameOrPid)
    {
        if (int.TryParse(nameOrPid, out int pid))
        {
            try { var p = Process.GetProcessById(pid); p.Kill(); return true; } catch { return false; }
        }
        try
        {
            foreach (var p in Process.GetProcessesByName(nameOrPid))
                p.Kill();
            return true;
        }
        catch { return false; }
    }

    public bool ProcessExists(string name)
    {
        return Process.GetProcessesByName(name).Length > 0;
    }

    public object ListProcesses()
    {
        return Process.GetProcesses().Select(p => (object)new Dictionary<string, object>
        {
            ["name"] = p.ProcessName,
            ["pid"] = (double)p.Id,
        }).ToList();
    }

    
    public void Notify(string title, string message)
    {
        
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-Command \"[System.Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms'); [System.Windows.Forms.MessageBox]::Show('{message}', '{title}')\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        catch { }
    }

    public void Beep(int freq, int duration)
    {
        try
        {
            Console.Beep(freq, duration);
        }
        catch { }
    }
}
