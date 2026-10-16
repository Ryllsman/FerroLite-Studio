using System.Runtime.InteropServices;

namespace Ferrolite.Core.Lang;







public sealed class VJoyVirtualControllerBackend : IVirtualControllerBackend
{
    
    
    private static bool _present;
    private static readonly HashSet<uint> _acquired = new();
    private static readonly object _lock = new();

    static VJoyVirtualControllerBackend()
    {
        try
        {
            
            
            
            if (LoadLibraryW("vJoyInterface.dll") != IntPtr.Zero) { _present = true; return; }
            string arch = System.Environment.Is64BitProcess ? "x64" : "x86";
            string pf = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
            string candidate = Path.Combine(pf, "vJoy", arch, "vJoyInterface.dll");
            _present = File.Exists(candidate) && LoadLibraryW(candidate) != IntPtr.Zero;
        }
        catch { _present = false; }
    }

    public bool IsAvailable => _present;
    public static bool StaticIsAvailable() => _present;

    public int VirtualDeviceCount
    {
        get
        {
            if (!_present) return 0;
            int n = 0;
            for (uint dev = 1; dev <= 16; dev++)
                if (GetVJDAxisExist(dev, HID_AXIS_X) != 0) n++;
            return n;
        }
    }

    public bool SetEnabled(int deviceIndex, bool enabled)
    {
        if (!_present) return false;
        lock (_lock)
        {
            if (enabled) return AcquireVJD((uint)deviceIndex) || _acquired.Contains((uint)deviceIndex);
            RelinquishVJD((uint)deviceIndex);
            _acquired.Remove((uint)deviceIndex);
            return true;
        }
    }

    public void SetState(int deviceIndex, VJoyState state)
    {
        if (!_present) return;
        try
        {
            SetStateCore(deviceIndex, state);
        }
        catch (EntryPointNotFoundException) { _present = false; } 
        catch (DllNotFoundException) { _present = false; }
        catch (Exception) {  }
    }

    private static void SetStateCore(int deviceIndex, VJoyState state)
    {
        uint dev = (uint)deviceIndex;
        lock (_lock)
        {
            if (!_acquired.Contains(dev))
            {
                if (!AcquireVJD(dev)) return; 
                _acquired.Add(dev);
            }
        }

        
        SetBtn((state.wButtons & 0x1000) != 0, dev, 1); 
        SetBtn((state.wButtons & 0x2000) != 0, dev, 2); 
        SetBtn((state.wButtons & 0x4000) != 0, dev, 3); 
        SetBtn((state.wButtons & 0x8000) != 0, dev, 4); 
        SetBtn((state.wButtons & 0x0100) != 0, dev, 5); 
        SetBtn((state.wButtons & 0x0200) != 0, dev, 6); 
        SetBtn((state.wButtons & 0x0020) != 0, dev, 7); 
        SetBtn((state.wButtons & 0x0010) != 0, dev, 8); 
        SetBtn((state.wButtons & 0x0040) != 0, dev, 9); 
        SetBtn((state.wButtons & 0x0080) != 0, dev, 10); 

        
        
        int pov = -1;
        bool up = (state.wButtons & 0x0001) != 0;
        bool down = (state.wButtons & 0x0002) != 0;
        bool left = (state.wButtons & 0x0004) != 0;
        bool right = (state.wButtons & 0x0008) != 0;
        if (up && right) pov = 4500;
        else if (up && left) pov = 31500;
        else if (down && right) pov = 13500;
        else if (down && left) pov = 22500;
        else if (up) pov = 0;
        else if (down) pov = 18000;
        else if (left) pov = 27000;
        else if (right) pov = 9000;
        SetContPov(pov, dev, 0);

        
        SetAxis(ToAxis(state.sThumbLX), dev, HID_AXIS_X);
        SetAxis(ToAxis(-state.sThumbLY), dev, HID_AXIS_Y);   
        SetAxis(ToAxis(state.sThumbRX), dev, HID_AXIS_RX);
        SetAxis(ToAxis(-state.sThumbRY), dev, HID_AXIS_RY);

        
        SetAxis((int)(state.bLeftTrigger == 0 ? 0x1 : 0x1 + state.bLeftTrigger / 255.0 * 0x7FFE), dev, HID_AXIS_Z);
        SetAxis((int)(state.bRightTrigger == 0 ? 0x1 : 0x1 + state.bRightTrigger / 255.0 * 0x7FFE), dev, HID_AXIS_RZ);
    }

    private static int ToAxis(double v)
    {
        double d = Math.Clamp(v / 32767.0, -1.0, 1.0);
        return Math.Clamp(0x4000 + (int)(d * 0x3FFF), 0x1, 0x7FFF);
    }

    private const uint HID_AXIS_X = 0x30;
    private const uint HID_AXIS_Y = 0x31;
    private const uint HID_AXIS_Z = 0x32;
    private const uint HID_AXIS_RX = 0x33;
    private const uint HID_AXIS_RY = 0x34;
    private const uint HID_AXIS_RZ = 0x35;

    public static VJoyState ParseVJoyState(object?[] args)
    {
        ushort buttons = 0;
        byte lt = 0, rt = 0;
        short ltx = 0, lty = 0, rtx = 0, rty = 0;

        if (args.Length > 0 && args[0] != null) buttons = (ushort)Convert.ToInt32(args[0]);
        if (args.Length > 1 && args[1] != null) lt = (byte)Convert.ToInt32(args[1]);
        if (args.Length > 2 && args[2] != null) rt = (byte)Convert.ToInt32(args[2]);
        if (args.Length > 3 && args[3] != null) ltx = (short)Convert.ToInt32(args[3]);
        if (args.Length > 4 && args[4] != null) lty = (short)Convert.ToInt32(args[4]);
        if (args.Length > 5 && args[5] != null) rtx = (short)Convert.ToInt32(args[5]);
        if (args.Length > 6 && args[6] != null) rty = (short)Convert.ToInt32(args[6]);

        return VJoyState.FromGamepad(buttons, lt, rt, ltx, lty, rtx, rty);
    }

    
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern bool AcquireVJD(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void RelinquishVJD(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern bool SetAxis(int value, uint rID, uint axis);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern bool SetBtn(bool value, uint rID, byte nBtn);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern bool SetContPov(int value, uint rID, byte nPov);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int GetVJDAxisExist(uint rID, uint axis);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string lpFileName);
}
