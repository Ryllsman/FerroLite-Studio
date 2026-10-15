using System.Runtime.InteropServices;

namespace Ferrolite.Core.Lang;




















public static class VigemBus
{
    
    private static readonly Guid BusGuid = new(0x96E42B22, 0xF5E9, 0x42F8, 0xB0, 0x43, 0xED, 0x0F, 0x93, 0x2F, 0x01, 0x4F);

    private const uint VigemCommonVersion = 0x0001;
    private const uint FileDeviceBusExtender = 0x0000002A;
    private const uint MethodBuffered = 0;
    private const uint FileReadData = 1, FileWriteData = 2;
    private const uint VigemBase = 0x801;

    private static uint Ctl(uint func, uint access) =>
        (FileDeviceBusExtender << 16) | (access << 14) | (func << 2) | MethodBuffered;

    private static readonly uint IOCTL_VIGEM_PLUGIN_TARGET = Ctl(VigemBase + 0x000, FileWriteData);
    private static readonly uint IOCTL_VIGEM_UNPLUG_TARGET = Ctl(VigemBase + 0x001, FileWriteData);
    private static readonly uint IOCTL_VIGEM_CHECK_VERSION = Ctl(VigemBase + 0x002, FileWriteData);
    private static readonly uint IOCTL_VIGEM_WAIT_DEVICE_READY = Ctl(VigemBase + 0x003, FileWriteData);
    private static readonly uint IOCTL_XUSB_SUBMIT_REPORT = Ctl(VigemBase + 0x201, FileWriteData);
    private static readonly uint IOCTL_DS4_SUBMIT_REPORT = Ctl(VigemBase + 0x202, FileWriteData);
    private static readonly uint IOCTL_XUSB_GET_USER_INDEX = Ctl(VigemBase + 0x206, FileReadData | FileWriteData);

    private const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    private const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;
    private const uint DIGCF_PRESENT = 0x2, DIGCF_DEVICEINTERFACE = 0x10;

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid guid, IntPtr enumerator, IntPtr hwndParent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid guid, int index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detailBuffer, int detailSize, ref int neededSize, IntPtr devInfoData);
    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid guid; public uint flags; public IntPtr reserved; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr h, uint ioctl, ref IN_STRUCT input, uint inSize, IntPtr outBuf, uint outSize, out uint returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr h, uint ioctl, ref VERSION_STRUCT input, uint inSize, IntPtr outBuf, uint outSize, out uint returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr h, uint ioctl, ref SUBMIT_STRUCT input, uint inSize, IntPtr outBuf, uint outSize, out uint returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr h, uint ioctl, DS4_SUBMIT_STRUCT input, uint inSize, IntPtr outBuf, uint outSize, out uint returned, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr h, uint ioctl, ref GET_USER_INDEX_STRUCT input, uint inSize, ref GET_USER_INDEX_STRUCT output, uint outSize, out uint returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);

    
    
    
    [StructLayout(LayoutKind.Sequential)]
    private struct IN_STRUCT
    {
        public uint Size, SerialNo, Payload0, Payload1;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct VERSION_STRUCT
    {
        public uint Size, Version;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct XUSB_REPORT
    {
        public ushort wButtons;
        public byte bLeftTrigger, bRightTrigger;
        public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct SUBMIT_STRUCT 
    {
        public uint Size;
        public uint SerialNo;
        public XUSB_REPORT Report;
    }

    
    
    
    
    
    [StructLayout(LayoutKind.Sequential)]
    private struct DS4_SUBMIT_STRUCT
    {
        public uint Size;
        public uint SerialNo;
        public byte Lx, Ly, Rx, Ry;
        public ushort wButtons;
        public byte Special, Lt, Rt;
    }

    
    [StructLayout(LayoutKind.Sequential)]
    private struct GET_USER_INDEX_STRUCT
    {
        public uint Size;
        public uint SerialNo;
        public uint UserIndex;
    }

    private static IntPtr _bus = IntPtr.Zero;
    private static bool _probed;              
    private static bool _unavailable;         
    private static int _lastFailTick;         
    private static readonly object _busLock = new();

    
    
    
    
    
    
    public static IntPtr GetBus()
    {
        lock (_busLock)
        {
            if (_bus != IntPtr.Zero) return _bus;
            if (_unavailable && System.Environment.TickCount - _lastFailTick < 5000)
                return IntPtr.Zero;
            _bus = TryConnect();
            _probed = true;
            if (_bus == IntPtr.Zero)
            {
                _unavailable = true;
                _lastFailTick = System.Environment.TickCount;
            }
            else _unavailable = false;
            return _bus;
        }
    }

    
    public static bool IsDriverAvailable() => GetBus() != IntPtr.Zero;

    
    public static bool Probed => _probed;

    
    
    
    
    private static IntPtr TryConnect()
    {
        Guid guid = BusGuid;
        IntPtr set = SetupDiGetClassDevsW(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set.ToInt64() == -1 || set == IntPtr.Zero) return IntPtr.Zero;
        try
        {
            int i = 0;
            while (true)
            {
                var iface = new SP_DEVICE_INTERFACE_DATA();
                iface.cbSize = Marshal.SizeOf(iface);
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i++, ref iface)) break;

                int needed = 0;
                SetupDiGetDeviceInterfaceDetailW(set, ref iface, IntPtr.Zero, 0, ref needed, IntPtr.Zero);
                if (needed == 0) continue;

                IntPtr buf = Marshal.AllocHGlobal(needed);
                try
                {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6); 
                    if (!SetupDiGetDeviceInterfaceDetailW(set, ref iface, buf, needed, ref needed, IntPtr.Zero)) continue;
                    string path = Marshal.PtrToStringUni(buf + 4) ?? "";
                    if (path.Length == 0) continue;

                    IntPtr bus = CreateFileW(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                        IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_NO_BUFFERING | FILE_FLAG_WRITE_THROUGH, IntPtr.Zero);
                    if (bus.ToInt64() == -1 || bus == IntPtr.Zero) continue;

                    
                    var ver = new VERSION_STRUCT { Size = 8, Version = VigemCommonVersion };
                    if (DeviceIoControl(bus, IOCTL_VIGEM_CHECK_VERSION, ref ver, 8, IntPtr.Zero, 0, out _, IntPtr.Zero))
                        return bus;

                    CloseHandle(bus);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        catch
        {
            
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return IntPtr.Zero;
    }

    
    
    
    
    public static int PluginX360(IntPtr bus, ushort vendorId = 0x045E, ushort productId = 0x028E)
    {
        lock (_busLock)
        {
            int s = PluginScan(bus, vendorId, productId);
            if (s != 0) return s;

            
            
            
            try { if (_bus != IntPtr.Zero) CloseHandle(_bus); } catch { }
            _bus = IntPtr.Zero;
            _bus = TryConnect();
            if (_bus == IntPtr.Zero) return 0;
            return PluginScan(_bus, vendorId, productId);
        }
    }

    private static int PluginScan(IntPtr bus, ushort vendorId, ushort productId)
    {
        
        for (uint serial = 1; serial <= 16; serial++)
        {
            
            
            var plug = new IN_STRUCT
            {
                Size = 16,
                SerialNo = serial,
                Payload0 = 0,                                   
                Payload1 = ((uint)productId << 16) | vendorId,  
            };
            if (!DeviceIoControl(bus, IOCTL_VIGEM_PLUGIN_TARGET, ref plug, 16, IntPtr.Zero, 0, out _, IntPtr.Zero))
                continue;

            
            
            
            var ready = new IN_STRUCT { Size = 8, SerialNo = serial };
            DeviceIoControl(bus, IOCTL_VIGEM_WAIT_DEVICE_READY, ref ready, 8, IntPtr.Zero, 0, out _, IntPtr.Zero);
            return (int)serial;
        }
        return 0;
    }

    
    public static bool Submit(IntPtr bus, int serial, ushort buttons, byte lt, byte rt, short lx, short ly, short rx, short ry)
    {
        var rep = new SUBMIT_STRUCT
        {
            Size = 20, 
            SerialNo = (uint)serial,
            Report = new XUSB_REPORT { wButtons = buttons, bLeftTrigger = lt, bRightTrigger = rt, sThumbLX = lx, sThumbLY = ly, sThumbRX = rx, sThumbRY = ry },
        };
        lock (_busLock)
            return DeviceIoControl(bus, IOCTL_XUSB_SUBMIT_REPORT, ref rep, 20, IntPtr.Zero, 0, out _, IntPtr.Zero);
    }

    
    public static void Unplug(IntPtr bus, int serial)
    {
        var un = new IN_STRUCT { Size = 8, SerialNo = (uint)serial };
        lock (_busLock)
            DeviceIoControl(bus, IOCTL_VIGEM_UNPLUG_TARGET, ref un, 8, IntPtr.Zero, 0, out _, IntPtr.Zero);
    }

    
    
    
    
    public static int PluginDS4(IntPtr bus)
    {
        lock (_busLock)
        {
            for (uint serial = 1; serial <= 16; serial++)
            {
                var plug = new IN_STRUCT
                {
                    Size = 16,
                    SerialNo = serial,
                    Payload0 = 2, 
                    Payload1 = 0, 
                };
                if (!DeviceIoControl(bus, IOCTL_VIGEM_PLUGIN_TARGET, ref plug, 16, IntPtr.Zero, 0, out _, IntPtr.Zero))
                    continue;
                var ready = new IN_STRUCT { Size = 8, SerialNo = serial };
                DeviceIoControl(bus, IOCTL_VIGEM_WAIT_DEVICE_READY, ref ready, 8, IntPtr.Zero, 0, out _, IntPtr.Zero);
                return (int)serial;
            }
            return 0;
        }
    }

    
    public static bool SubmitDS4(IntPtr bus, int serial, ushort buttons, byte special, byte lt, byte rt, byte lx, byte ly, byte rx, byte ry)
    {
        var rep = new DS4_SUBMIT_STRUCT
        {
            Size = 20, 
            SerialNo = (uint)serial,
            Lx = lx, Ly = ly, Rx = rx, Ry = ry,
            wButtons = buttons,
            Special = special,
            Lt = lt, Rt = rt,
        };
        lock (_busLock)
            return DeviceIoControl(bus, IOCTL_DS4_SUBMIT_REPORT, rep, 20, IntPtr.Zero, 0, out _, IntPtr.Zero);
    }

    
    
    
    
    
    
    public static void XusbToDs4(ushort xb, byte xlt, byte xrt, short xl, short xly, short rx, short ry,
        out ushort db, out byte sp, out byte dlt, out byte drt, out byte dlx, out byte dly, out byte drx, out byte dry)
    {
        db = 0; sp = 0;
        if ((xb & 0x1000) != 0) db |= 0x0020; 
        if ((xb & 0x2000) != 0) db |= 0x0040; 
        if ((xb & 0x4000) != 0) db |= 0x0010; 
        if ((xb & 0x8000) != 0) db |= 0x0080; 
        if ((xb & 0x0100) != 0) db |= 0x0100; 
        if ((xb & 0x0200) != 0) db |= 0x0200; 
        if ((xb & 0x0020) != 0) db |= 0x1000; 
        if ((xb & 0x0010) != 0) db |= 0x2000; 
        if ((xb & 0x0040) != 0) db |= 0x4000; 
        if ((xb & 0x0080) != 0) db |= 0x8000; 
        if (xlt > 0) db |= 0x0400;            
        if (xrt > 0) db |= 0x0800;            
        if ((xb & 0x0400) != 0) sp |= 0x01;   
        dlt = xlt; drt = xrt;
        dlx = (byte)((xl + 32768) >> 8);      
        dly = (byte)Math.Clamp(128 - ((xly + 64) >> 7), 0, 255); 
        drx = (byte)((rx + 32768) >> 8);
        dry = (byte)Math.Clamp(128 - ((ry + 64) >> 7), 0, 255);
        
        
        
        
        bool u = (xb & 0x0001) != 0, d = (xb & 0x0002) != 0;
        bool l = (xb & 0x0004) != 0, r = (xb & 0x0008) != 0;
        byte hat;
        if (u && r) hat = 1;
        else if (d && r) hat = 3;
        else if (d && l) hat = 5;
        else if (u && l) hat = 7;
        else if (u) hat = 0;
        else if (r) hat = 2;
        else if (d) hat = 4;
        else if (l) hat = 6;
        else hat = 8;
        db |= hat;
    }

    
    
    
    
    public static int GetUserIndex(IntPtr bus, int serial)
    {
        var req = new GET_USER_INDEX_STRUCT { Size = 12, SerialNo = (uint)serial, UserIndex = 0 };
        lock (_busLock)
        {
            if (!DeviceIoControl(bus, IOCTL_XUSB_GET_USER_INDEX, ref req, 12, ref req, 12, out _, IntPtr.Zero))
                return -1;
        }
        return (int)req.UserIndex;
    }
}









public class ViGEmVirtualBackend : IVirtualControllerBackend
{
    private int _serial;      
    private bool _failed;
    private int _xinputSlot = -2; 
    private readonly bool _ds4;

    
    
    
    
    
    public static bool UseDualShock4 { get; set; }

    public ViGEmVirtualBackend() { _ds4 = UseDualShock4; }

    
    
    
    public int XInputSlot
    {
        get
        {
            if (_xinputSlot == -2)
            {
                int s = _serial;
                _xinputSlot = (s != 0) ? VigemBus.GetUserIndex(VigemBus.GetBus(), s) : -1;
            }
            return _xinputSlot;
        }
    }

    public bool IsAvailable
    {
        get
        {
            if (_failed) return false;
            if (_serial != 0) return true;
            try
            {
                IntPtr bus = VigemBus.GetBus();
                if (bus == IntPtr.Zero) { _failed = true; return false; }
                _serial = _ds4 ? VigemBus.PluginDS4(bus) : VigemBus.PluginX360(bus);
                if (_serial == 0) { _failed = true; return false; }
                return true;
            }
            catch { _failed = true; return false; }
        }
    }

    public int VirtualDeviceCount => IsAvailable ? 1 : 0;

    public void SetState(int deviceIndex, VJoyState state)
    {
        int serial = _serial;
        if (serial == 0) return;
        try
        {
            IntPtr bus = VigemBus.GetBus();
            if (_ds4)
            {
                VigemBus.XusbToDs4(state.wButtons, state.bLeftTrigger, state.bRightTrigger,
                    state.sThumbLX, state.sThumbLY, state.sThumbRX, state.sThumbRY,
                    out ushort db, out byte sp, out byte dlt, out byte drt, out byte dlx, out byte dly, out byte drx, out byte dry);
                VigemBus.SubmitDS4(bus, serial, db, sp, dlt, drt, dlx, dly, drx, dry);
            }
            else
            {
                VigemBus.Submit(bus, serial, state.wButtons, state.bLeftTrigger, state.bRightTrigger,
                    state.sThumbLX, state.sThumbLY, state.sThumbRX, state.sThumbRY);
            }
        }
        catch
        {
            
        }
    }

    public bool SetEnabled(int deviceIndex, bool enabled)
    {
        
        return IsAvailable;
    }

    
    public void Shutdown()
    {
        int serial = _serial;
        if (serial == 0) return;
        try { VigemBus.Unplug(VigemBus.GetBus(), serial); } catch { }
        _serial = 0;
    }

    
    public static bool StaticIsAvailable() => VigemBus.IsDriverAvailable();
}
