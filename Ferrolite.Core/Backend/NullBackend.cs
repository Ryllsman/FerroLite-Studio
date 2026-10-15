namespace Ferrolite.Core.Lang;

public class NullBackend : IActionBackend, IControllerBackend
{
    private readonly Action<string> _logFn;
    private readonly List<double> _fakeMouse = new() { 0, 0 };
    private string _clipboard = "";
    private readonly HashSet<string> _simulatedKeys = new();
    private readonly HashSet<string> _simulatedMouse = new();

    public NullBackend(Action<string>? logFn = null)
    {
        _logFn = logFn ?? Console.WriteLine;
    }

    private void Log(string msg) => _logFn($"[SIM] {msg}");

    public void MouseMove(int x, int y, double duration)
    {
        _fakeMouse[0] = x; _fakeMouse[1] = y;
        Log($"move mouse to ({x}, {y})");
    }
    public void MouseMoveRelative(double dx, double dy, double duration, int? steps) => Log($"move relative ({dx}, {dy})");
    public void MouseClick(int? x, int? y, string button) => Log($"{button} click");
    public void MouseDoubleClick(int? x, int? y, string button) => Log($"double {button} click");
    public void MouseDown(string button) { _simulatedKeys.Add(button.ToLower()); Log($"mouse down: {button}"); }
    public void MouseUp(string button) { _simulatedKeys.Remove(button.ToLower()); Log($"mouse up: {button}"); }
    public void MouseDrag(int x1, int y1, int x2, int y2, double duration, string button) => Log($"drag {button}");
    public void MouseScroll(int amount, int? x, int? y) => Log($"scroll {amount}");
    public (int X, int Y) MousePosition() => ((int)_fakeMouse[0], (int)_fakeMouse[1]);
    public bool IsKeyDown(string key) => _simulatedKeys.Contains(key.ToLower());
    public bool IsKeyToggled(string key) => false;
    public bool IsMouseDown(string button) => _simulatedKeys.Contains(button.ToLower());

    public void KeyPress(string key) => Log($"press key '{key}'");
    public void KeyDown(string key) { _simulatedKeys.Add(key.ToLower()); Log($"key down '{key}'"); }
    public void KeyUp(string key) { _simulatedKeys.Remove(key.ToLower()); Log($"key up '{key}'"); }
    public void TypeText(string text, double interval) => Log($"type text: {text}");
    public void Hotkey(List<string> keys) => Log($"hotkey: {string.Join("+", keys)}");

    public bool FocusWindow(string title) => true;
    public object? FindWindow(string title) => null;
    public object ListWindows() => new List<object>();
    public void MinimizeWindow(string title) { }
    public void MaximizeWindow(string title) { }
    public void CloseWindow(string title) { }
    public object? GetActiveWindow() => new Dictionary<object, object> { ["title"] = "Sim", ["x"] = 0, ["y"] = 0, ["width"] = 1920, ["height"] = 1080 };
    public void MoveWindow(string title, int x, int y, int? w, int? h) { }

    public List<object> GetPixelColor(int x, int y) => new() { 0, 0, 0 };
    public (int Width, int Height) ScreenSize() => (1920, 1080);
    public string Screenshot(string? path) => path ?? "sim.png";
    public object? FindImageOnScreen(string imagePath, double confidence) => null;
    public bool WaitForPixelColor(int x, int y, List<object> color, double timeout) { Thread.Sleep((int)(Math.Min(0.2, timeout) * 1000)); return false; }

    public string ClipboardGet() => _clipboard;
    public void ClipboardSet(string text) => _clipboard = text;

    public object RunProcess(string path, List<string> args, bool wait) => new Dictionary<object, object> { ["pid"] = -1, ["started"] = true };
    public bool KillProcess(string nameOrPid) => true;
    public bool ProcessExists(string name) => false;
    public object ListProcesses() => new List<object>();

    public void Notify(string title, string message) => Log($"notification: {title} - {message}");
    public void Beep(int freq, int duration) => Log($"beep {freq}Hz for {duration}ms");

    
    public bool IsGamepadConnected(int controllerId) => false;
    public bool IsGamepadButtonDown(int controllerId, string button) => false;
    public double GetStickX(int controllerId, string stick) => 0;
    public double GetStickY(int controllerId, string stick) => 0;
    public double GetTriggerValue(int controllerId, string trigger) => 0;
    public void GamepadPress(int controllerId, string button) => Log($"gamepad {controllerId} press {button}");
    public void GamepadRelease(int controllerId, string button) => Log($"gamepad {controllerId} release {button}");
    public void GamepadStick(int controllerId, string stick, double x, double y) => Log($"gamepad {controllerId} {stick} ({x:F2}, {y:F2})");
    public void GamepadTrigger(int controllerId, string trigger, double value) => Log($"gamepad {controllerId} {trigger} {value:F2}");
    public void GamepadDPad(int controllerId, string direction) => Log($"gamepad {controllerId} dpad {direction}");
}
