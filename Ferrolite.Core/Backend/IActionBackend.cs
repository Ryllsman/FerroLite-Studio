namespace Ferrolite.Core.Lang;

public interface IActionBackend
{
    
    void MouseMove(int x, int y, double duration);
    void MouseMoveRelative(double dx, double dy, double duration, int? steps);
    void MouseClick(int? x, int? y, string button);
    void MouseDoubleClick(int? x, int? y, string button);
    void MouseDown(string button);
    void MouseUp(string button);
    void MouseDrag(int x1, int y1, int x2, int y2, double duration, string button);
    void MouseScroll(int amount, int? x, int? y);
    (int X, int Y) MousePosition();
    bool IsKeyDown(string key);
    bool IsKeyToggled(string key);
    bool IsMouseDown(string button);

    
    void KeyPress(string key);
    void KeyDown(string key);
    void KeyUp(string key);
    void TypeText(string text, double interval);
    void Hotkey(List<string> keys);

    
    bool FocusWindow(string title);
    object? FindWindow(string title);
    object ListWindows();
    void MinimizeWindow(string title);
    void MaximizeWindow(string title);
    void CloseWindow(string title);
    object? GetActiveWindow();
    void MoveWindow(string title, int x, int y, int? w, int? h);

    
    List<object> GetPixelColor(int x, int y);
    (int Width, int Height) ScreenSize();
    string Screenshot(string? path);
    object? FindImageOnScreen(string imagePath, double confidence);
    bool WaitForPixelColor(int x, int y, List<object> color, double timeout);

    
    string ClipboardGet();
    void ClipboardSet(string text);

    
    object RunProcess(string path, List<string> args, bool wait);
    bool KillProcess(string nameOrPid);
    bool ProcessExists(string name);
    object ListProcesses();

    
    void Notify(string title, string message);
    void Beep(int freq, int duration);
}

public interface IControllerBackend
{
    bool IsGamepadConnected(int controllerId);
    void GamepadPress(int controllerId, string button);
    void GamepadRelease(int controllerId, string button);
    void GamepadStick(int controllerId, string stick, double x, double y);
    void GamepadTrigger(int controllerId, string trigger, double value);
    void GamepadDPad(int controllerId, string direction);
    
    bool IsGamepadButtonDown(int controllerId, string button);
    
    double GetStickX(int controllerId, string stick);
    double GetStickY(int controllerId, string stick);
    
    double GetTriggerValue(int controllerId, string trigger);
}







public interface IVirtualControllerBackend
{
    int VirtualDeviceCount { get; }
    bool IsAvailable { get; }
    bool SetEnabled(int deviceIndex, bool enabled);
    void SetState(int deviceIndex, VJoyState state);
}


public struct VJoyState
{
    public ushort wButtons;
    public byte bLeftTrigger, bRightTrigger;
    public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;

    public static VJoyState FromGamepad(ushort buttons, byte leftTrigger, byte rightTrigger,
        short leftThumbX, short leftThumbY, short rightThumbX, short rightThumbY)
    {
        return new VJoyState
        {
            wButtons = buttons,
            bLeftTrigger = leftTrigger,
            bRightTrigger = rightTrigger,
            sThumbLX = leftThumbX, sThumbLY = leftThumbY,
            sThumbRX = rightThumbX, sThumbRY = rightThumbY,
        };
    }
}
