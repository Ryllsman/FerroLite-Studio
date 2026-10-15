using Ferrolite.Core.Lang;

namespace Ferrolite.UI.Services;






public class LoggingBackend : IActionBackend, IControllerBackend
{
    private readonly IActionBackend _actions;
    private readonly IControllerBackend _gamepad;
    private readonly Action<string> _log;

    public LoggingBackend(IActionBackend actions, Action<string> log, IControllerBackend? gamepad = null)
    {
        _actions = actions;
        _gamepad = gamepad ?? new WindowsControllerBackend();
        _log = log;
    }

    private void Do(Action act, string message)
    {
        _log("[ACTION] " + message);
        act();
    }

    
    public void MouseMove(int x, int y, double duration) => Do(() => _actions.MouseMove(x, y, duration), $"move mouse to ({x}, {y})");
    public void MouseMoveRelative(double dx, double dy, double duration, int? steps) => Do(() => _actions.MouseMoveRelative(dx, dy, duration, steps), $"move relative ({dx:F1}, {dy:F1})");
    public void MouseClick(int? x, int? y, string button) => Do(() => _actions.MouseClick(x, y, button), $"click {button}" + (x.HasValue ? $" at ({x}, {y})" : ""));
    public void MouseDoubleClick(int? x, int? y, string button) => Do(() => _actions.MouseDoubleClick(x, y, button), $"double-click {button}");
    public void MouseDown(string button) => Do(() => _actions.MouseDown(button), $"mouse down: {button}");
    public void MouseUp(string button) => Do(() => _actions.MouseUp(button), $"mouse up: {button}");
    public void MouseDrag(int x1, int y1, int x2, int y2, double duration, string button) => Do(() => _actions.MouseDrag(x1, y1, x2, y2, duration, button), $"drag {button} ({x1},{y1}) -> ({x2},{y2})");
    public void MouseScroll(int amount, int? x, int? y) => Do(() => _actions.MouseScroll(amount, x, y), $"scroll {amount}");
    public (int X, int Y) MousePosition() => _actions.MousePosition();
    public bool IsKeyDown(string key) => _actions.IsKeyDown(key);
    public bool IsKeyToggled(string key) => _actions.IsKeyToggled(key);
    public bool IsMouseDown(string button) => _actions.IsMouseDown(button);

    
    public void KeyPress(string key) => Do(() => _actions.KeyPress(key), $"press key '{key}'");
    public void KeyDown(string key) => Do(() => _actions.KeyDown(key), $"key down '{key}'");
    public void KeyUp(string key) => Do(() => _actions.KeyUp(key), $"key up '{key}'");
    public void TypeText(string text, double interval) => Do(() => _actions.TypeText(text, interval), $"type text: {text}");
    public void Hotkey(List<string> keys) => Do(() => _actions.Hotkey(keys), $"hotkey: {string.Join("+", keys)}");

    
    public bool FocusWindow(string title) => _actions.FocusWindow(title);
    public object? FindWindow(string title) => _actions.FindWindow(title);
    public object ListWindows() => _actions.ListWindows();
    public void MinimizeWindow(string title) => Do(() => _actions.MinimizeWindow(title), $"minimize window '{title}'");
    public void MaximizeWindow(string title) => Do(() => _actions.MaximizeWindow(title), $"maximize window '{title}'");
    public void CloseWindow(string title) => Do(() => _actions.CloseWindow(title), $"close window '{title}'");
    public object? GetActiveWindow() => _actions.GetActiveWindow();
    public void MoveWindow(string title, int x, int y, int? w, int? h) => Do(() => _actions.MoveWindow(title, x, y, w, h), $"move window '{title}'");

    
    public List<object> GetPixelColor(int x, int y) => _actions.GetPixelColor(x, y);
    public (int Width, int Height) ScreenSize() => _actions.ScreenSize();
    public string Screenshot(string? path) => _actions.Screenshot(path);
    public object? FindImageOnScreen(string imagePath, double confidence) => _actions.FindImageOnScreen(imagePath, confidence);
    public bool WaitForPixelColor(int x, int y, List<object> color, double timeout) => _actions.WaitForPixelColor(x, y, color, timeout);

    
    public string ClipboardGet() => _actions.ClipboardGet();
    public void ClipboardSet(string text) => _actions.ClipboardSet(text);
    public object RunProcess(string path, List<string> args, bool wait) => _actions.RunProcess(path, args, wait);
    public bool KillProcess(string nameOrPid) => _actions.KillProcess(nameOrPid);
    public bool ProcessExists(string name) => _actions.ProcessExists(name);
    public object ListProcesses() => _actions.ListProcesses();
    public void Notify(string title, string message) => Do(() => _actions.Notify(title, message), $"notification: {title} - {message}");
    public void Beep(int freq, int duration) => Do(() => _actions.Beep(freq, duration), $"beep {freq}Hz for {duration}ms");

    
    public bool IsGamepadButtonDown(int controllerId, string button) => _gamepad.IsGamepadButtonDown(controllerId, button);
    public double GetStickX(int controllerId, string stick) => _gamepad.GetStickX(controllerId, stick);
    public double GetStickY(int controllerId, string stick) => _gamepad.GetStickY(controllerId, stick);
    public double GetTriggerValue(int controllerId, string trigger) => _gamepad.GetTriggerValue(controllerId, trigger);
    public bool IsGamepadConnected(int controllerId) => _gamepad.IsGamepadConnected(controllerId);
    public void GamepadPress(int controllerId, string button) => Do(() => _gamepad.GamepadPress(controllerId, button), $"gamepad {controllerId} press {button}");
    public void GamepadRelease(int controllerId, string button) => Do(() => _gamepad.GamepadRelease(controllerId, button), $"gamepad {controllerId} release {button}");
    public void GamepadStick(int controllerId, string stick, double x, double y) => Do(() => _gamepad.GamepadStick(controllerId, stick, x, y), $"gamepad {controllerId} {stick} ({x:F2}, {y:F2})");
    public void GamepadTrigger(int controllerId, string trigger, double value) => Do(() => _gamepad.GamepadTrigger(controllerId, trigger, value), $"gamepad {controllerId} {trigger} {value:F2}");
    public void GamepadDPad(int controllerId, string direction) => Do(() => _gamepad.GamepadDPad(controllerId, direction), $"gamepad {controllerId} dpad {direction}");
}
