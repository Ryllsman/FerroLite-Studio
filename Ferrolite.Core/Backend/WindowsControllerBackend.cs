using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Ferrolite.Core.Lang;

public class WindowsControllerBackend : IControllerBackend
{
    
    
    
    private static readonly int _dllIndex;

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState_910(int dwUserIndex, out XINPUT_STATE pState);
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState_14(int dwUserIndex, out XINPUT_STATE pState);
    [DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState_13(int dwUserIndex, out XINPUT_STATE pState);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    static WindowsControllerBackend()
    {
        
        int[] order = { 1, 0, 2 }; 
        _dllIndex = 0;
        foreach (int i in order)
        {
            bool sees = false;
            for (int pad = 0; pad < 4 && !sees; pad++)
            {
                try
                {
                    int rc = i switch
                    {
                        1 => XInputGetState_14(pad, out _),
                        2 => XInputGetState_13(pad, out _),
                        _ => XInputGetState_910(pad, out _),
                    };
                    sees = rc == 0;
                }
                catch { sees = false; }
            }
            if (sees) { _dllIndex = i; break; }
        }
    }

    internal static int XInputGetState(int dwUserIndex, out XINPUT_STATE pState)
    {
        int rc = _dllIndex switch
        {
            1 => XInputGetState_14(dwUserIndex, out pState),
            2 => XInputGetState_13(dwUserIndex, out pState),
            _ => XInputGetState_910(dwUserIndex, out pState),
        };
        if (rc == 0) return 0;

        
        
        if (DualSenseHid.TryRead(dwUserIndex, out var ds))
        {
            pState = default;
            pState.Gamepad = new XINPUT_GAMEPAD
            {
                wButtons = ds.Buttons,
                bLeftTrigger = ds.Lt,
                bRightTrigger = ds.Rt,
                sThumbLX = ds.Lx,
                sThumbLY = ds.Ly,
                sThumbRX = ds.Rx,
                sThumbRY = ds.Ry,
            };
            return 0;
        }
        return rc;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct XINPUT_STATE { public uint dwPacketNumber; public XINPUT_GAMEPAD Gamepad; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger, bRightTrigger;
        public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_VIBRATION { public ushort wLeftMotorSpeed, wRightMotorSpeed; }

    private const ushort
        DPAD_UP = 0x0001, DPAD_DOWN = 0x0002, DPAD_LEFT = 0x0004, DPAD_RIGHT = 0x0008,
        START = 0x0010, BACK = 0x0020, LTHUMB = 0x0040, RTHUMB = 0x0080,
        LB = 0x0100, RB = 0x0200, A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;

    private readonly Dictionary<int, ushort> _buttonState = new();
    private readonly Dictionary<int, short> _stickLX = new(), _stickLY = new(), _stickRX = new(), _stickRY = new();
    private readonly Dictionary<int, byte> _trigL = new(), _trigR = new();
    private readonly Dictionary<int, ushort> _dpadState = new();
    private readonly Dictionary<int, Thread> _vibrationThreads = new();

    public bool IsGamepadConnected(int controllerId)
    {
        try { return XInputGetState(controllerId, out _) == 0; }
        catch { return false; }
    }

    public bool IsGamepadButtonDown(int controllerId, string button)
    {
        try
        {
            if (XInputGetState(controllerId, out var state) != 0) return false;
            ushort vk = ButtonToFlag(button);
            if (vk == 0) return false;
            return (state.Gamepad.wButtons & vk) != 0;
        }
        catch { return false; }
    }

    public void GamepadPress(int controllerId, string button)
    {
        if (ControllerPassthrough.Active) { ControllerPassthrough.PressButton(button); return; }
        ushort vk = ButtonToFlag(button);
        _buttonState.TryGetValue(controllerId, out ushort current);
        _buttonState[controllerId] = (ushort)(current | vk);
        ApplyState(controllerId);
    }

    public void GamepadRelease(int controllerId, string button)
    {
        if (ControllerPassthrough.Active) { ControllerPassthrough.ReleaseButton(button); return; }
        ushort vk = ButtonToFlag(button);
        _buttonState.TryGetValue(controllerId, out ushort current);
        _buttonState[controllerId] = (ushort)(current & ~vk);
        ApplyState(controllerId);
    }

    public void GamepadStick(int controllerId, string stick, double x, double y)
    {
        if (ControllerPassthrough.Active) { ControllerPassthrough.SetStickOffset(controllerId, stick, x, y); return; }
        short sx = (short)(Math.Clamp(x, -1.0, 1.0) * 32767);
        short sy = (short)(Math.Clamp(y, -1.0, 1.0) * 32767);
        if (stick == "left_stick") { _stickLX[controllerId] = sx; _stickLY[controllerId] = sy; }
        else { _stickRX[controllerId] = sx; _stickRY[controllerId] = sy; }
        ApplyState(controllerId);
    }

    public void GamepadTrigger(int controllerId, string trigger, double value)
    {
        if (ControllerPassthrough.Active) { ControllerPassthrough.SetTrigger(trigger, value); return; }
        byte val = (byte)(Math.Clamp(value, 0.0, 1.0) * 255);
        if (trigger == "left_trigger") _trigL[controllerId] = val;
        else _trigR[controllerId] = val;
        ApplyState(controllerId);
    }

    public void GamepadDPad(int controllerId, string direction)
    {
        if (ControllerPassthrough.Active) { ControllerPassthrough.SetDPad(direction); return; }
        ushort flag = direction switch
        {
            "up" => DPAD_UP, "down" => DPAD_DOWN, "left" => DPAD_LEFT, "right" => DPAD_RIGHT,
            "up_left" => (ushort)(DPAD_UP | DPAD_LEFT), "up_right" => (ushort)(DPAD_UP | DPAD_RIGHT),
            "down_left" => (ushort)(DPAD_DOWN | DPAD_LEFT), "down_right" => (ushort)(DPAD_DOWN | DPAD_RIGHT),
            _ => 0
        };
        _dpadState[controllerId] = flag;
        ApplyState(controllerId);
    }

    private static readonly VJoyVirtualControllerBackend? _vjoy = CreateVJoy();

    private static VJoyVirtualControllerBackend? CreateVJoy()
    {
        try { return VJoyVirtualControllerBackend.StaticIsAvailable() ? new VJoyVirtualControllerBackend() : null; }
        catch { return null; }
    }

    private static ViGEmVirtualBackend? _vigemStatic;
    private static readonly object _vbeLock = new();

    
    private static IVirtualControllerBackend? GetVirtualBackend()
    {
        if (ControllerPassthrough.Active) return null; 
        lock (_vbeLock)
        {
            if (_vigemStatic != null) return _vigemStatic.IsAvailable ? _vigemStatic : null;
            if (ViGEmVirtualBackend.StaticIsAvailable())
            {
                var v = new ViGEmVirtualBackend();
                if (v.IsAvailable) { _vigemStatic = v; return v; }
            }
            return _vjoy is { IsAvailable: true } ? _vjoy : null;
        }
    }

    private void ApplyState(int controllerId)
    {
        _buttonState.TryGetValue(controllerId, out ushort buttons);
        _dpadState.TryGetValue(controllerId, out ushort dpad);
        _stickLX.TryGetValue(controllerId, out short lx);
        _stickLY.TryGetValue(controllerId, out short ly);
        _stickRX.TryGetValue(controllerId, out short rx);
        _stickRY.TryGetValue(controllerId, out short ry);
        _trigL.TryGetValue(controllerId, out byte tl);
        _trigR.TryGetValue(controllerId, out byte tr);

        var state = new XINPUT_STATE();
        state.Gamepad.wButtons = (ushort)(buttons | dpad);
        state.Gamepad.sThumbLX = lx;
        state.Gamepad.sThumbLY = ly;
        state.Gamepad.sThumbRX = rx;
        state.Gamepad.sThumbRY = ry;
        state.Gamepad.bLeftTrigger = tl;
        state.Gamepad.bRightTrigger = tr;

        
        
        
        var vbe = GetVirtualBackend();
        if (vbe != null)
        {
            try
            {
                vbe.SetState(controllerId, VJoyState.FromGamepad(
                    (ushort)(buttons | dpad), tl, tr, lx, ly, rx, ry));
            }
            catch {  }
        }
        if (XInputGetState(controllerId, out _) == 0)
        {
            var vib = new XINPUT_VIBRATION();
            vib.wLeftMotorSpeed = tl > 0 ? (ushort)(tl * 257) : (ushort)0;
            vib.wRightMotorSpeed = tr > 0 ? (ushort)(tr * 257) : (ushort)0;
            XInputSetState(controllerId, ref vib);
        }
    }

    internal static ushort ButtonToFlag(string button) => button switch
    {
        "gamepad_a" => A, "gamepad_b" => B, "gamepad_x" => X, "gamepad_y" => Y,
        "gamepad_start" => START, "gamepad_back" => BACK,
        "gamepad_l3" => LTHUMB, "gamepad_r3" => RTHUMB,
        "gamepad_lb" => LB, "gamepad_rb" => RB,
        
        "a" => A, "b" => B, "x" => X, "y" => Y,
        "start" => START, "back" => BACK, "menu" => START, "view" => BACK,
        "l3" => LTHUMB, "r3" => RTHUMB, "ls" => LTHUMB, "rs" => RTHUMB,
        "lb" => LB, "rb" => RB, "l1" => LB, "r1" => RB,
        
        "up" => DPAD_UP, "down" => DPAD_DOWN, "left" => DPAD_LEFT, "right" => DPAD_RIGHT,
        "dpad_up" => DPAD_UP, "dpad_down" => DPAD_DOWN, "dpad_left" => DPAD_LEFT, "dpad_right" => DPAD_RIGHT,
        "up_left" => (ushort)(DPAD_UP | DPAD_LEFT), "up_right" => (ushort)(DPAD_UP | DPAD_RIGHT),
        "down_left" => (ushort)(DPAD_DOWN | DPAD_LEFT), "down_right" => (ushort)(DPAD_DOWN | DPAD_RIGHT),
        _ => 0
    };

    public double GetStickX(int controllerId, string stick)
    {
        try
        {
            if (XInputGetState(controllerId, out var state) != 0) return 0;
            return stick == "left_stick" ? state.Gamepad.sThumbLX / 32767.0 : state.Gamepad.sThumbRX / 32767.0;
        }
        catch { return 0; }
    }

    public double GetStickY(int controllerId, string stick)
    {
        try
        {
            if (XInputGetState(controllerId, out var state) != 0) return 0;
            return stick == "left_stick" ? state.Gamepad.sThumbLY / 32767.0 : state.Gamepad.sThumbRY / 32767.0;
        }
        catch { return 0; }
    }

    public double GetTriggerValue(int controllerId, string trigger)
    {
        try
        {
            if (XInputGetState(controllerId, out var state) != 0) return 0;
            return trigger == "left_trigger" ? state.Gamepad.bLeftTrigger / 255.0 : state.Gamepad.bRightTrigger / 255.0;
        }
        catch { return 0; }
    }
}

















public static class ControllerPassthrough
{
    private static IVirtualControllerBackend? _backend;
    private static Thread? _thread;
    private static volatile bool _running;
    private static int _physicalId;
    private static readonly object _lock = new();
    private static double _offsetLX, _offsetLY, _offsetRX, _offsetRY;
    private static ushort _buttonOverride;
    private static ushort _dpadOverride;
    private static double _ltOverride, _rtOverride;

    
    
    private static ushort _vButtons;
    private static byte _vLt, _vRt;
    private static short _vLx, _vLy, _vRx, _vRy;

    public static bool Active => _running;

    public static void Start(int physicalId)
    {
        if (!ViGEmVirtualBackend.StaticIsAvailable() && !VJoyVirtualControllerBackend.StaticIsAvailable())
            throw new VSRuntimeError("controller_passthrough needs a virtual-controller driver (ViGEmBus preferred, vJoy legacy). Reinstall FerroLite with the ViGEmBus component ticked.");
        Stop();
        _physicalId = physicalId;
        lock (_lock)
        {
            _offsetLX = _offsetLY = _offsetRX = _offsetRY = 0;
            _buttonOverride = 0; _dpadOverride = 0; _ltOverride = _rtOverride = 0;
            _vButtons = 0; _vLt = _vRt = 0; _vLx = _vLy = _vRx = _vRy = 0;
        }

        _running = true;
        
        
        
        
        
        
        var physicalSnapshot = DualSenseHid.EnumeratePhysicalControllerInstancePaths();

        
        
        
        
        
        
        
        
        
        
        
        _physicalXInputDevnodes = DualSenseHid.EnumeratePhysicalXInputInstancePaths();
        _physicalIsXInput = _physicalXInputDevnodes.Count > 0;
        if (_physicalIsXInput && ViGEmVirtualBackend.UseDualShock4)
        {
            ViGEmVirtualBackend.UseDualShock4 = false;
            _autoSwitchedPadType = true;
        }

        _backend = CreateVirtualBackend();

        
        
        
        
        
        
        HidHidden = false;
        var toHide = new List<string>(physicalSnapshot);
        if (_backend is ViGEmVirtualBackend)
            toHide.AddRange(DualSenseHid.EnumerateVJoyInstancePaths());

        if (toHide.Count > 0)
        {
            bool hidden = false;
            if (HidHideControl.EnsureWhitelisted())
            {
                
                hidden = HidHideControl.TryHide(toHide.ToArray());
            }
            if (!hidden && !System.Environment.IsPrivilegedProcess)
            {
                
                hidden = HidHideElevation.TryArmViaHelper(toHide.ToArray());
                _usedHelper = hidden;
            }
            if (hidden) HidHidden = true;
            else if (HidHideControl.LastHideFailure != null || HidHideElevation.LastError != null)
                PassthroughNote = "physical pad NOT hidden (" + (HidHideElevation.LastError ?? HidHideControl.LastHideFailure) + ") — the game may also see your real controller";
        }
        else
        {
            PassthroughNote = "no physical pad found to hide — wake it (PS/home button) and re-arm";
        }

        
        
        
        
        
        
        
        if (_physicalIsXInput)
        {
            const string xinputNote =
                "your physical controller presents as an Xbox/XInput pad: Windows exposes it through XInput, " +
                "which the cloak cannot mask, so the real pad stays listed in games. FerroLite takes XInput " +
                "player 1 for the virtual pad instead (parking yours for a moment), which is what decides " +
                "which pad a game reads first. For full isolation of a DualSense-style pad, use one that " +
                "presents as a HID device (DualShock/DualSense or PS mode), which the cloak does hide completely.";
            PassthroughNote = PassthroughNote == null ? xinputNote : PassthroughNote + " " + xinputNote;
        }

        if (_autoSwitchedPadType)
        {
            const string typeNote =
                "the virtual pad was switched to Xbox 360 for this run: a virtual DualShock 4 has no XInput presence, " +
                "so a game would never read it against an Xbox-mode controller.";
            PassthroughNote = PassthroughNote == null ? typeNote : PassthroughNote + " " + typeNote;
        }

        
        
        
        
        
        
        int ourSlot = VirtualXInputSlot;
        int physSlot = FirstLiveXInputSlotExcept(ourSlot);
        bool tookPlayerOne = false;
        if (_physicalIsXInput && ourSlot > 0 && physSlot >= 0 && physSlot < ourSlot)
        {
            tookPlayerOne = TakePlayerOneSlot(_physicalXInputDevnodes);
            ourSlot = VirtualXInputSlot;
            physSlot = FirstLiveXInputSlotExcept(ourSlot);
        }
        PhysicalXInputSlot = physSlot;
        _tookPlayerOne = tookPlayerOne;

        if (tookPlayerOne)
        {
            string slotNote =
                $"the virtual pad now owns XInput player {ourSlot + 1} (the physical pad was parked for a moment and " +
                "re-connected behind it), so a game reads the pad your scripts can change.";
            PassthroughNote = PassthroughNote == null ? slotNote : PassthroughNote + " " + slotNote;
        }
        else if (_physicalIsXInput && physSlot >= 0 && ourSlot >= 0 && physSlot < ourSlot)
        {
            string slotNote =
                $"your physical pad owns XInput player {physSlot + 1} while the virtual pad is player {ourSlot + 1}, " +
                "so a game reading the first pad reads the UNMODIFIED controller." +
                (SlotPriorityFailure != null
                    ? " Automatic player-1 priority did not apply: " + SlotPriorityFailure + "."
                    : "") +
                " XInput slots follow arrival order — arm the passthrough BEFORE connecting the controller and " +
                "the virtual pad takes player 1 without a fight.";
            PassthroughNote = PassthroughNote == null ? slotNote : PassthroughNote + " " + slotNote;
        }

        
        
        
        
        
        if (!tookPlayerOne && _slotParkBrokerUsed)
            SlotFailureNote =
                "the physical pad was parked and handed back, but the virtual pad did not end up on player 1; " +
                "re-arm the passthrough to try again (one UAC prompt)";

        _thread = new Thread(Loop) { IsBackground = true, Name = "ControllerPassthrough" };
        _hiddenPathsSnapshot = toHide;
        _thread.Start();
    }

    
    
    
    
    
    
    
    
    
    private static void RehideCheck()
    {
        if (!(HidHidden || HidHideControl.Active)) return;
        try
        {
            var baseline = _hiddenPathsSnapshot;
            if (baseline == null) return; 

            var now = DualSenseHid.EnumeratePhysicalControllerInstancePaths();
            
            
            
            
            bool changed = now.Count != baseline.Count ||
                           !now.All(baseline.Contains);
            if (!changed) return;

            var toHide = new List<string>(baseline);
            foreach (var p in now)
                if (!toHide.Contains(p, StringComparer.OrdinalIgnoreCase)) toHide.Add(p);
            
            
            if (_backend is ViGEmVirtualBackend)
                foreach (var v in DualSenseHid.EnumerateVJoyInstancePaths())
                    if (!toHide.Contains(v, StringComparer.OrdinalIgnoreCase)) toHide.Add(v);

            
            
            
            if (HidHideControl.EnsureWhitelisted() && HidHideControl.TryHide(toHide.ToArray()))
            {
                _hiddenPathsSnapshot = toHide;
            }
            else if (!System.Environment.IsPrivilegedProcess)
            {
                
                
                
                
                var paths = toHide.ToArray();
                if (Interlocked.CompareExchange(ref _helperBusy, 1, 0) == 0)
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            if (HidHideElevation.TryArmViaHelper(paths))
                            {
                                _usedHelper = true;
                                _hiddenPathsSnapshot = new List<string>(paths);
                            }
                        }
                        catch { }
                        finally { Interlocked.Exchange(ref _helperBusy, 0); }
                    });
                }
            }
        }
        catch {  }
    }

    private static List<string>? _hiddenPathsSnapshot;
    private static int _helperBusy;
    private static List<string> _physicalXInputDevnodes = new();
    private static bool _physicalIsXInput;
    private static bool _autoSwitchedPadType;
    private static bool _tookPlayerOne;
    
    private static bool _slotParkBrokerUsed;

    
    public static string? PassthroughNote { get; private set; }

    private static bool _usedHelper;

    
    public static bool HidHidden { get; private set; }

    
    public static string VirtualPadName => _backend is ViGEmVirtualBackend ? "vigem" : _backend is VJoyVirtualControllerBackend ? "vjoy" : "";

    
    public static int VirtualXInputSlot => _backend is ViGEmVirtualBackend v ? v.XInputSlot : -1;

    
    public static int PhysicalXInputSlot { get; private set; } = -1;

    
    
    
    
    
    
    
    
    
    private static int ResolvePhysicalSlot(IVirtualControllerBackend backend)
    {
        int ours = backend is ViGEmVirtualBackend v ? v.XInputSlot : -1;
        if (_physicalId != ours && WindowsControllerBackend.XInputGetState(_physicalId, out _) == 0) return _physicalId;
        for (int i = 0; i < 4; i++)
        {
            if (i == ours) continue;
            if (WindowsControllerBackend.XInputGetState(i, out _) == 0) return i;
        }
        return -1;
    }

    private static int FirstLiveXInputSlotExcept(int except)
    {
        for (int i = 0; i < 4; i++)
        {
            if (i == except) continue;
            if (WindowsControllerBackend.XInputGetState(i, out _) == 0) return i;
        }
        return -1;
    }

    
    public static bool TookPlayerOneSlot => _tookPlayerOne;

    
    public static string? SlotPriorityFailure { get; private set; }

    
    
    
    
    
    
    public static string? SlotFailureNote { get; private set; }

    
    public static bool AutoSwitchedPadType => _autoSwitchedPadType;

    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    private static bool TakePlayerOneSlot(List<string> physXInputDevnodes)
    {
        SlotPriorityFailure = null;
        if (_backend is not ViGEmVirtualBackend vigem)
        {
            SlotPriorityFailure = "the virtual pad is not a ViGEm XInput device";
            return false;
        }

        
        
        
        
        FileStream? parkSignal = null;
        string? parkRequest = null;
        if (!System.Environment.IsPrivilegedProcess)
        {
            if (!HidHideElevation.TryStartSlotParkBroker(physXInputDevnodes, out string brokerError, out parkRequest))
            {
                
                
                
                SlotPriorityFailure = brokerError;
                return false;
            }
            _slotParkBrokerUsed = true;
            parkSignal = HidHideElevation.OpenParkSignal(parkRequest);
        }
        else
        {
            XInputSlotPriority.DisableRunsInBroker = false;
            if (!XInputSlotPriority.Disable(physXInputDevnodes, out string reason))
            {
                SlotPriorityFailure = reason;
                return false;
            }
        }

        bool replugged = false;
        try
        {
            
            
            
            
            
            
            bool freed = false;
            for (int i = 0; i < 30; i++)
            {
                if (FirstLiveXInputSlotExcept(VirtualXInputSlot) < 0) { freed = true; break; }
                Thread.Sleep(100);
            }
            if (!freed)
            {
                SlotPriorityFailure =
                    "the physical pad never left XInput, so the virtual pad cannot take its index";
                return false;
            }

            
            
            
            
            vigem.Shutdown();
            Thread.Sleep(250);
            var fresh = new ViGEmVirtualBackend();
            if (!fresh.IsAvailable)
                throw new InvalidOperationException("re-plug failed");
            _backend = fresh;
            replugged = true;
        }
        catch
        {
            replugged = false;
        }
        finally
        {
            
            
            
            
            if (parkSignal != null)
            {
                parkSignal.Dispose();
                try { if (parkRequest != null && File.Exists(parkRequest)) File.Delete(parkRequest); } catch { }
                HidHideElevation.WaitSlotParkExit(15_000);
            }
            else
            {
                XInputSlotPriority.Enable(physXInputDevnodes);
            }
        }

        if (!replugged)
        {
            SlotPriorityFailure = "re-plugging the virtual pad failed (no ViGEm device)";
            return false;
        }

        
        
        
        
        int me = VirtualXInputSlot;
        for (int i = 0; i < 50 && FirstLiveXInputSlotExcept(me) < 0; i++) Thread.Sleep(100);
        if (me != 0)
        {
            SlotPriorityFailure = $"the virtual pad came back on XInput slot {me} instead of player 1";
            return false;
        }
        return true;
    }

    
    
    
    
    public static void RecoverParkedPads()
    {
        try { XInputSlotPriority.RecoverOnStartup(); } catch { }
    }

    
    
    
    
    
    
    public static IReadOnlyList<string> HiddenAtArm =>
        (IReadOnlyList<string>?)_hiddenPathsSnapshot ?? Array.Empty<string>();

    public static void Stop()
    {
        if (!_running && _thread == null) return;
        _running = false;
        _thread?.Join(500);
        _thread = null;
        _physicalXInputDevnodes = new();
        _physicalIsXInput = false;
        _autoSwitchedPadType = false;
        _tookPlayerOne = false;
        _slotParkBrokerUsed = false;
        SlotPriorityFailure = null;
        SlotFailureNote = null;
        PassthroughNote = null;
        _hiddenPathsSnapshot = null;
        PhysicalXInputSlot = -1;
        lock (_lock)
        {
            _offsetLX = _offsetLY = _offsetRX = _offsetRY = 0;
            _buttonOverride = 0; _dpadOverride = 0; _ltOverride = _rtOverride = 0;
            _vButtons = 0; _vLt = _vRt = 0; _vLx = _vLy = _vRx = _vRy = 0;
        }
        
        
        try { _backend?.SetState(0, default); } catch { }
        
        
        try { (_backend as ViGEmVirtualBackend)?.Shutdown(); } catch { }
        
        try { (_backend as VJoyVirtualControllerBackend)?.SetEnabled(0, false); } catch { }
        _backend = null;
        
        
        
        
        if (_usedHelper) HidHideElevation.AskHelperToDisarm();
        _usedHelper = false;
        HidHideControl.Release();
        HidHidden = false;
    }

    public static void SetStickOffset(int controllerId, string stick, double x, double y)
    {
        lock (_lock)
        {
            if (stick == "left_stick") { _offsetLX = Math.Clamp(x, -1, 1); _offsetLY = Math.Clamp(y, -1, 1); }
            else { _offsetRX = Math.Clamp(x, -1, 1); _offsetRY = Math.Clamp(y, -1, 1); }
        }
    }

    public static void PressButton(string button)
    {
        ushort f = WindowsControllerBackend.ButtonToFlag(button);
        if (f != 0) lock (_lock) _buttonOverride |= f;
    }

    public static void ReleaseButton(string button)
    {
        ushort f = WindowsControllerBackend.ButtonToFlag(button);
        if (f != 0) lock (_lock) _buttonOverride &= (ushort)~f;
    }

    public static void SetTrigger(string trigger, double value)
    {
        lock (_lock)
        {
            if (trigger == "left_trigger") _ltOverride = Math.Clamp(value, 0, 1);
            else _rtOverride = Math.Clamp(value, 0, 1);
        }
    }

    public static void SetDPad(string direction)
    {
        ushort f = direction switch
        {
            "up" => 0x0001, "down" => 0x0002, "left" => 0x0004, "right" => 0x0008,
            "up_left" => 0x0005, "up_right" => 0x0009, "down_left" => 0x0006, "down_right" => 0x000A,
            _ => 0
        };
        lock (_lock) _dpadOverride = f;
    }

    
    
    
    
    

    
    public static bool VirtualButtonDown(string button)
    {
        ushort f = WindowsControllerBackend.ButtonToFlag(button);
        if (f == 0) return false;
        lock (_lock) return (_vButtons & f) != 0;
    }

    
    public static double VirtualTriggerValue(string trigger)
    {
        lock (_lock) return (trigger == "left_trigger" ? _vLt : _vRt) / 255.0;
    }

    
    public static double VirtualStickValue(string stick, bool y)
    {
        lock (_lock)
        {
            short v = stick == "left_stick" ? (y ? _vLy : _vLx) : (y ? _vRy : _vRx);
            return v / 32767.0;
        }
    }

    private static void Loop()
    {
        var backend = _backend ?? CreateVirtualBackend();
        int rehideCountdown = 0;
        while (_running)
        {
            
            
            
            
            
            if (_backend != null && !ReferenceEquals(_backend, backend)) backend = _backend;
            
            
            try
            {
                Tick(backend);
            }
            catch
            {
                
            }
            if (--rehideCountdown <= 0)
            {
                RehideCheck();
                rehideCountdown = 250; 
            }
            Thread.Sleep(8); 
        }
    }

    private static IVirtualControllerBackend CreateVirtualBackend()
    {
        
        
        
        
        
        if (ViGEmVirtualBackend.StaticIsAvailable())
        {
            for (int attempt = 0; attempt < 5 && (_running || attempt == 0); attempt++)
            {
                var v = new ViGEmVirtualBackend();
                if (v.IsAvailable) return v;
                Thread.Sleep(600);
            }
        }
        return new VJoyVirtualControllerBackend();
    }

    private static void Tick(IVirtualControllerBackend backend)
    {
        ushort buttons = 0; byte lt = 0, rt = 0;
        short lx = 0, ly = 0, rx = 0, ry = 0;

        
        
        
        
        
        
        
        
        
        
        int physSlot = ResolvePhysicalSlot(backend);
        PhysicalXInputSlot = physSlot;
        bool selfSlot = physSlot < 0;
        bool got = false;

        if (!selfSlot && WindowsControllerBackend.XInputGetState(physSlot, out var st) == 0)
        {
            buttons = st.Gamepad.wButtons;
            lt = st.Gamepad.bLeftTrigger; rt = st.Gamepad.bRightTrigger;
            lx = st.Gamepad.sThumbLX; ly = st.Gamepad.sThumbLY;
            rx = st.Gamepad.sThumbRX; ry = st.Gamepad.sThumbRY;
            got = true;
        }

        
        
        if (!got && DualSenseHid.TryRead(0, out var ds))
        {
            buttons = ds.Buttons;
            lt = ds.Lt; rt = ds.Rt;
            lx = ds.Lx; ly = ds.Ly;
            rx = ds.Rx; ry = ds.Ry;
            got = true;
        }

        
        
        if (!got && selfSlot && backend is ViGEmVirtualBackend v2)
        {
            for (int i = 0; i < 4 && !got; i++)
            {
                if (i == v2.XInputSlot) continue;
                if (WindowsControllerBackend.XInputGetState(i, out var st2) == 0)
                {
                    buttons = st2.Gamepad.wButtons;
                    lt = st2.Gamepad.bLeftTrigger; rt = st2.Gamepad.bRightTrigger;
                    lx = st2.Gamepad.sThumbLX; ly = st2.Gamepad.sThumbLY;
                    rx = st2.Gamepad.sThumbRX; ry = st2.Gamepad.sThumbRY;
                    got = true;
                }
            }
        }
        lock (_lock)
        {
            buttons |= (ushort)(_buttonOverride | _dpadOverride);
            lt = Math.Max(lt, (byte)Math.Round(_ltOverride * 255));
            rt = Math.Max(rt, (byte)Math.Round(_rtOverride * 255));
            lx = ClampAxis(lx + (int)(_offsetLX * 32767));
            ly = ClampAxis(ly + (int)(_offsetLY * 32767));
            rx = ClampAxis(rx + (int)(_offsetRX * 32767));
            ry = ClampAxis(ry + (int)(_offsetRY * 32767));
            _vButtons = buttons; _vLt = lt; _vRt = rt;
            _vLx = lx; _vLy = ly; _vRx = rx; _vRy = ry;
        }
        backend.SetState(_physicalId, VJoyState.FromGamepad(buttons, lt, rt, lx, ly, rx, ry));
    }

    private static short ClampAxis(int v) => (short)Math.Clamp(v, short.MinValue, short.MaxValue);
}
