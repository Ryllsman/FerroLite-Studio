using System.Runtime.InteropServices;

namespace Ferrolite.Core.Lang;









public static class DualSenseHid
{
    
    public const ushort F_UP = 0x0001, F_DOWN = 0x0002, F_LEFT = 0x0004, F_RIGHT = 0x0008,
        F_START = 0x0010, F_BACK = 0x0020, F_L3 = 0x0040, F_R3 = 0x0080,
        F_LB = 0x0100, F_RB = 0x0200, F_A = 0x1000, F_B = 0x2000, F_X = 0x4000, F_Y = 0x8000;

    public struct PadState
    {
        public bool Connected;
        public ushort Buttons;
        public byte Lt, Rt;
        public short Lx, Ly, Rx, Ry;
    }

    private static readonly object _lock = new();
    private static readonly List<Reader> _readers = new();
    private static Thread? _watchdog;
    private static volatile bool _started;

    
    public static int PadCount
    {
        get { EnsureStarted(); lock (_lock) return _readers.Count(r => r.IsConnected); }
    }

    
    public static bool TryRead(int index, out PadState state)
    {
        EnsureStarted();
        state = default;
        Reader[] snapshot;
        lock (_lock) snapshot = _readers.ToArray();
        var ordered = snapshot.OrderByDescending(r => r.IsConnected).ToArray();
        var connected = snapshot.Where(r => r.IsConnected).ToList();
        if (index >= connected.Count) return false;
        state = connected[index].Latest;
        return true;
    }

    private static void EnsureStarted()
    {
        if (_started) return;
        lock (_lock)
        {
            if (_started) return;
            _started = true;
            _watchdog = new Thread(WatchdogLoop) { IsBackground = true, Name = "DualSenseHidWatchdog", Priority = ThreadPriority.AboveNormal };
            _watchdog.Start();
        }
    }

    
    
    
    
    private static void WatchdogLoop()
    {
        while (true)
        {
            try
            {
                lock (_lock)
                {
                    _readers.RemoveAll(r => !r.IsAlive);
                    var paths = EnumerateSonyGamepads();
                    foreach (var path in paths)
                    {
                        if (_readers.Any(r => r.DevicePath == path)) continue;
                        var reader = new Reader(path);
                        _readers.Add(reader);
                        reader.Start();
                    }
                }
            }
            catch {  }
            Thread.Sleep(2000);
        }
    }

    

    
    
    
    
    
    
    private sealed class HidDevice
    {
        public string InterfacePath = "";
        public string InstanceId = "";
        public string[] Ids = Array.Empty<string>();
    }

    
    private static List<HidDevice> EnumerateHidDevices()
    {
        var list = new List<HidDevice>();
        HidD_GetHidGuid(out Guid hidGuid);
        IntPtr set = SetupDiGetClassDevsW(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
            DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set == InvalidHandle) return list;

        try
        {
            int i = 0;
            while (true)
            {
                var iface = new SP_DEVICE_INTERFACE_DATA();
                iface.cbSize = Marshal.SizeOf(iface);
                var devInfo = new SP_DEVINFO_DATA();
                devInfo.cbSize = Marshal.SizeOf(devInfo);
                if (!SetupDiEnumDeviceInterfaces(set, ref devInfo, ref hidGuid, i++, ref iface)) break;

                int needed = 0;
                SetupDiGetDeviceInterfaceDetailW(set, ref iface, IntPtr.Zero, 0, ref needed, IntPtr.Zero);
                if (needed == 0) continue;

                IntPtr buf = Marshal.AllocHGlobal(needed);
                try
                {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetailW(set, ref iface, buf, needed, ref needed, ref devInfo)) continue;

                    string path = Marshal.PtrToStringUni(buf + 4) ?? "";
                    string? instance = GetDeviceInstanceId(set, ref devInfo) ?? InterfacePathToInstancePath(path);
                    if (path.Length == 0 || string.IsNullOrEmpty(instance)) continue;

                    list.Add(new HidDevice
                    {
                        InterfacePath = path,
                        InstanceId = instance,
                        Ids = GetDeviceIds(set, ref devInfo),
                    });
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return list;
    }

    
    private static string? GetDeviceInstanceId(IntPtr set, ref SP_DEVINFO_DATA devInfo)
    {
        try
        {
            SetupDiGetDeviceInstanceIdW(set, ref devInfo, IntPtr.Zero, 0, out int needed);
            if (needed <= 1 || needed > 4096) return null;
            IntPtr buf = Marshal.AllocHGlobal(needed * 2);
            try
            {
                if (!SetupDiGetDeviceInstanceIdW(set, ref devInfo, buf, needed, out _)) return null;
                string id = Marshal.PtrToStringUni(buf) ?? "";
                return id.Length == 0 ? null : NormalizeInstanceId(id);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        catch { return null; }
    }

    
    
    
    
    
    
    
    
    private static string[] GetDeviceIds(IntPtr set, ref SP_DEVINFO_DATA devInfo)
    {
        try
        {
            return RegistryStrings(set, ref devInfo, SPDRP_HARDWAREID)
                .Concat(RegistryStrings(set, ref devInfo, SPDRP_COMPATIBLEIDS))
                .ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    
    private static string[] RegistryStrings(IntPtr set, ref SP_DEVINFO_DATA devInfo, uint property)
    {
        try
        {
            SetupDiGetDeviceRegistryPropertyW(set, ref devInfo, property, out _, null, 0, out int needed);
            if (needed <= 0 || needed > 8192) return Array.Empty<string>();
            var buf = new byte[needed];
            if (!SetupDiGetDeviceRegistryPropertyW(set, ref devInfo, property, out uint type, buf, buf.Length, out _))
                return Array.Empty<string>();
            const uint REG_MULTI_SZ = 7;
            if (type != REG_MULTI_SZ) return Array.Empty<string>();
            return System.Text.Encoding.Unicode.GetString(buf, 0, needed).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
        catch { return Array.Empty<string>(); }
    }

    
    
    
    
    
    
    
    
    
    public static List<string> EnumeratePhysicalControllerInstancePaths()
    {
        var result = new List<string>();
        foreach (var dev in EnumerateHidDevices())
        {
            
            
            
            
            
            
            if (IsVirtualPadInstance(dev.InstanceId)) continue;
            
            
            if (IsVJoyDevice(dev)) continue;
            if (!LooksLikePhysicalPad(dev)) continue;
            if (!result.Contains(dev.InstanceId)) result.Add(dev.InstanceId);
        }
        return result;
    }

    
    
    
    
    
    
    
    
    
    
    
    
    
    private static bool LooksLikePhysicalPad(HidDevice dev)
    {
        foreach (var id in dev.Ids)
        {
            if (id.Contains("HID_DEVICE_SYSTEM_GAME", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("HID_DEVICE_SYSTEM_JOYSTICK", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("HID_DEVICE_SYSTEM_MULTIAXIS", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("HID_DEVICE_UP:0001_U:0004", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("HID_DEVICE_UP:0001_U:0005", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("HID_DEVICE_UP:0001_U:0008", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        if (dev.InstanceId.Contains("vid_054c", StringComparison.OrdinalIgnoreCase)) return true;
        if (dev.InstanceId.Contains("&ig_", StringComparison.OrdinalIgnoreCase)) return true;

        
        
        
        
        
        
        foreach (var id in dev.Ids)
        {
            if (id.Contains("HID_DEVICE_SYSTEM_MOUSE", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("HID_DEVICE_SYSTEM_KEYBOARD", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("HID_DEVICE_SYSTEM_CONSUMER", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("HID_DEVICE_SYSTEM_CONTROL", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("HID_DEVICE_SYSTEM_MULTIMEDIA", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("HID_DEVICE_SYSTEM_PEN", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("HID_DEVICE_SYSTEM_TOUCHPAD", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return IsGamepadCaps(dev.InterfacePath);
    }

    
    
    
    
    
    
    private static bool IsVJoyDevice(HidDevice dev) => IdsSayVJoy(dev.InstanceId, dev.Ids);

    private static bool IdsSayVJoy(string instanceId, string[] ids)
    {
        if (instanceId.Contains("VJOY", StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var id in ids)
        {
            if (id.Contains("VID_1234&PID_BEAD", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("VJOYRAWPDO", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        
        
        
        return instanceId.StartsWith(@"HID\HIDCLASS\", StringComparison.OrdinalIgnoreCase)
               && VJoyVirtualControllerBackend.StaticIsAvailable();
    }

    
    
    
    
    
    
    
    public static List<string> EnumerateXInputDevnodes() => EnumerateXInputDevnodes(false);

    
    
    
    
    
    
    
    
    public static List<string> EnumeratePhysicalXInputInstancePaths() => EnumerateXInputDevnodes(true);

    private static List<string> EnumerateXInputDevnodes(bool physicalOnly)
    {
        var result = new List<string>();
        IntPtr set = SetupDiGetClassDevsAllClasses(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);
        if (set == InvalidHandle) return result;

        try
        {
            int i = 0;
            while (true)
            {
                var devInfo = new SP_DEVINFO_DATA();
                devInfo.cbSize = Marshal.SizeOf(devInfo);
                if (!SetupDiEnumDeviceInfo(set, i++, ref devInfo)) break;

                string? instance = GetDeviceInstanceId(set, ref devInfo);
                if (string.IsNullOrEmpty(instance)) continue;
                bool xinput = GetDeviceIds(set, ref devInfo).Any(id =>
                    id.Contains("XUSB", StringComparison.OrdinalIgnoreCase) ||
                    id.Contains("XINPUT", StringComparison.OrdinalIgnoreCase) ||
                    id.Contains("VID_045E&PID_028E", StringComparison.OrdinalIgnoreCase) ||
                    id.Contains("VID_045E&PID_02D1", StringComparison.OrdinalIgnoreCase) ||
                    id.Contains("VID_045E&PID_02EA", StringComparison.OrdinalIgnoreCase) ||
                    id.Contains("VID_045E&PID_0B00", StringComparison.OrdinalIgnoreCase));
                if (!xinput) continue;
                
                
                if (physicalOnly && IsVirtualPadInstance(instance)) continue;
                if (!result.Contains(instance)) result.Add(instance);
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return result;
    }

    
    private static bool IsGamepadCaps(string path)
    {
        IntPtr h = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h == InvalidHandle) return false;
        try
        {
            if (!HidD_GetPreparsedData(h, out IntPtr preparsed) || preparsed == IntPtr.Zero) return false;
            try
            {
                var caps = new HIDP_CAPS();
                if (HidP_GetCaps(preparsed, ref caps) != HIDP_STATUS_SUCCESS) return false;
                if (caps.UsagePage != UsagePageGenericDesktop) return false;
                
                return caps.Usage is 0x04 or 0x05 or 0x08;
            }
            finally { HidD_FreePreparsedData(preparsed); }
        }
        finally { CloseHandle(h); }
    }

    
    
    
    
    
    
    public static List<string> EnumerateVJoyInstancePaths()
    {
        var result = new List<string>();
        foreach (var dev in EnumerateHidDevices())
            if (IsVJoyDevice(dev) && !result.Contains(dev.InstanceId)) result.Add(dev.InstanceId);
        
        
        
        
        foreach (var instance in EnumerateVJoyDevnodes())
            if (!result.Contains(instance)) result.Add(instance);
        return result;
    }

    
    
    
    
    
    private static List<string> EnumerateVJoyDevnodes()
    {
        var result = new List<string>();
        
        
        
        Guid hidClass = new("745A17A0-74D3-11D0-B6FE-00A0C90F57DA");
        IntPtr set = SetupDiGetClassDevsW(ref hidClass, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
        if (set == InvalidHandle) return result;

        try
        {
            int i = 0;
            while (true)
            {
                var devInfo = new SP_DEVINFO_DATA();
                devInfo.cbSize = Marshal.SizeOf(devInfo);
                if (!SetupDiEnumDeviceInfo(set, i++, ref devInfo)) break;

                string? instance = GetDeviceInstanceId(set, ref devInfo);
                if (string.IsNullOrEmpty(instance)) continue;
                if (!IdsSayVJoy(instance, GetDeviceIds(set, ref devInfo))) continue;
                if (!result.Contains(instance)) result.Add(instance);
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return result;
    }

    
    
    
    
    
    
    
    
    public static List<string> EnumerateVirtualPadInstancePaths()
    {
        var result = new List<string>();
        HidD_GetHidGuid(out Guid hidGuid);
        IntPtr set = SetupDiGetClassDevsW(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
            DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set == InvalidHandle) return result;

        try
        {
            int i = 0;
            while (true)
            {
                var iface = new SP_DEVICE_INTERFACE_DATA();
                iface.cbSize = Marshal.SizeOf(iface);
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i++, ref iface)) break;

                int needed = 0;
                SetupDiGetDeviceInterfaceDetailW(set, ref iface, IntPtr.Zero, 0, ref needed, IntPtr.Zero);
                if (needed == 0) continue;

                IntPtr buf = Marshal.AllocHGlobal(needed);
                try
                {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetailW(set, ref iface, buf, needed, ref needed, IntPtr.Zero)) continue;
                    string path = Marshal.PtrToStringUni(buf + 4) ?? "";
                    string? instance = InterfacePathToInstancePath(path);
                    if (instance == null || result.Contains(instance)) continue;
                    if (!IsVirtualPadInstance(instance)) continue;

                    result.Add(instance);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return result;
    }

    
    
    
    
    
    
    
    
    
    
    
    private static bool IsVirtualPadInstance(string? instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return false;

        
        if (instanceId.StartsWith(@"HID\VIRTUALDEVICE", StringComparison.OrdinalIgnoreCase)) return true;

        
        
        
        if (!CfgMgrHelper.TryGetParent(instanceId, out string parentId)) return false;
        if (parentId.StartsWith(@"ROOT\", StringComparison.OrdinalIgnoreCase)) return true;
        if (!CfgMgrHelper.TryGetParent(parentId, out string grandParent)) return false;
        return grandParent.StartsWith(@"ROOT\", StringComparison.OrdinalIgnoreCase);
    }

    
    private static class CfgMgrHelper
    {
        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Get_Parent(out uint parentDevInst, uint devInst, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_Device_IDW(uint devInst, IntPtr buffer, uint bufferLen, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

        internal static bool TryGetParent(string deviceId, out string parentId)
        {
            parentId = "";
            try
            {
                if (CM_Locate_DevNodeW(out uint node, deviceId, 0) != 0) return false;
                if (CM_Get_Parent(out uint parent, node, 0) != 0) return false;
                IntPtr buf = Marshal.AllocHGlobal(512);
                try
                {
                    if (CM_Get_Device_IDW(parent, buf, 512, 0) != 0) return false;
                    parentId = Marshal.PtrToStringUni(buf) ?? "";
                    return parentId.Length > 0;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch { return false; }
        }

    }
    private static string? InterfacePathToInstancePath(string interfacePath)
    {
        if (!interfacePath.StartsWith("\\\\?\\", StringComparison.OrdinalIgnoreCase)) return null;
        string body = interfacePath.Substring(4);
        int hash = body.IndexOf("#{", StringComparison.Ordinal);
        if (hash >= 0) body = body.Substring(0, hash);
        return NormalizeInstanceId(body.Replace('#', '\\'));
    }

    
    
    
    
    
    
    
    private static string NormalizeInstanceId(string instanceId)
    {
        int last = instanceId.LastIndexOf('\\');
        if (last < 0) return instanceId;
        return instanceId.Substring(0, last).ToUpperInvariant() + instanceId.Substring(last);
    }

    private static List<string> EnumerateSonyGamepads()
    {
        var result = new List<string>();
        HidD_GetHidGuid(out Guid hidGuid);
        IntPtr set = SetupDiGetClassDevsW(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
            DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set == InvalidHandle) return result;

        try
        {
            int i = 0;
            while (true)
            {
                var iface = new SP_DEVICE_INTERFACE_DATA();
                iface.cbSize = Marshal.SizeOf(iface);
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i++, ref iface)) break;

                int needed = 0;
                SetupDiGetDeviceInterfaceDetailW(set, ref iface, IntPtr.Zero, 0, ref needed, IntPtr.Zero);
                if (needed == 0) continue;

                IntPtr buf = Marshal.AllocHGlobal(needed);
                try
                {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6); 
                    if (!SetupDiGetDeviceInterfaceDetailW(set, ref iface, buf, needed, ref needed, IntPtr.Zero)) continue;

                    
                    
                    string path = Marshal.PtrToStringUni(buf + 4) ?? "";
                    if (path.IndexOf("vid_054c", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    
                    if (!IsGamepadCollection(path)) continue;

                    
                    
                    
                    if (IsVirtualPadInstance(InterfacePathToInstancePath(path))) continue;
                    result.Add(path);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return result;
    }

    private static bool IsGamepadCollection(string path)
    {
        IntPtr h = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h == InvalidHandle) return false;
        try
        {
            if (!HidD_GetPreparsedData(h, out IntPtr preparsed) || preparsed == IntPtr.Zero) return false;
            try
            {
                var caps = new HIDP_CAPS();
                if (HidP_GetCaps(preparsed, ref caps) != HIDP_STATUS_SUCCESS) return false;
                
                
                
                
                return caps.InputReportByteLength >= 48;
            }
            finally { HidD_FreePreparsedData(preparsed); }
        }
        finally { CloseHandle(h); }
    }

    

    private sealed class Reader
    {
        public string DevicePath;
        private Thread? _thread;
        private PadState _latest;
        private readonly object _stateLock = new();

        
        
        
        
        
        private int _style = -1; 

        public Reader(string path) { DevicePath = path; }
        public bool IsConnected => _thread is { IsAlive: true };
        public bool IsAlive => IsConnected;
        public PadState Latest { get { lock (_stateLock) return _latest; } }

        public void Start()
        {
            _thread = new Thread(Loop) { IsBackground = true, Name = "DualSenseHidReader", Priority = ThreadPriority.Highest };
            _thread.Start();
        }

        private void Loop()
        {
            while (true)
            {
                IntPtr h = CreateFileW(DevicePath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (h == InvalidHandle) { Thread.Sleep(1500); continue; }

                IntPtr preparsed = IntPtr.Zero;
                HidD_GetPreparsedData(h, out preparsed);
                var buf = new byte[64];
                try
                {
                    while (true)
                    {
                        if (!ReadFile(h, buf, buf.Length, out int read, IntPtr.Zero) || read < 16)
                        {
                            break;
                        }
                        if (buf[0] != 0x01 && buf[0] != 0x31) continue; 
                        Parse(buf, preparsed);
                    }
                }
                catch {  }
                finally { CloseHandle(h); }
                if (preparsed != IntPtr.Zero) HidD_FreePreparsedData(preparsed);
                Thread.Sleep(1500);
            }
        }

        
        
        
        
        
        
        private void Parse(byte[] b, IntPtr preparsed)
        {
            if (preparsed != IntPtr.Zero && TryParseDescriptor(b, preparsed)) return;
            ParseRaw(b);
        }

        private bool TryParseDescriptor(byte[] b, IntPtr preparsed)
        {
            try
            {
                var usages = new ushort[32];
                uint len = (uint)usages.Length;
                int st = HidP_GetUsages(HidP_Input, UsagePageButton, 0, usages, ref len, preparsed, b, (uint)b.Length);
                if (st != HIDP_STATUS_SUCCESS) return false;

                
                
                if (_style == -1)
                {
                    int a32probe = AxisRaw(b, preparsed, 0x32);
                    int a33probe = AxisRaw(b, preparsed, 0x33);
                    
                    
                    
                    if (a32probe >= 100) _style = 1;
                    else if (a32probe <= 60) _style = 0;
                    else if (a33probe >= 100) _style = 0;
                    else if (a33probe <= 60) _style = 1;
                    else return false; 
                }
                bool ds4Style = _style == 1;

                var s = new PadState { Connected = true };
                for (int i = 0; i < len; i++) s.Buttons |= MapButtonUsage(usages[i], ds4Style);

                
                if (HidP_GetUsageValue(HidP_Input, UsagePageGenericDesktop, 0, 0x39, out uint hat, preparsed, b, (uint)b.Length) == HIDP_STATUS_SUCCESS
                    && hat <= 7)
                {
                    if (hat == 0 || hat == 7 || hat == 1) s.Buttons |= F_UP;
                    if (hat == 1 || hat == 2 || hat == 3) s.Buttons |= F_RIGHT;
                    if (hat == 3 || hat == 4 || hat == 5) s.Buttons |= F_DOWN;
                    if (hat == 5 || hat == 6 || hat == 7) s.Buttons |= F_LEFT;
                }

                
                
                
                
                
                int lxV = AxisRaw(b, preparsed, 0x30);
                int lyV = AxisRaw(b, preparsed, 0x31);
                int a32 = AxisRaw(b, preparsed, 0x32);
                int a33 = AxisRaw(b, preparsed, 0x33);
                int a34 = AxisRaw(b, preparsed, 0x34);
                int a35 = AxisRaw(b, preparsed, 0x35);

                s.Lx = ScaleAxis(lxV);
                s.Ly = ScaleAxis(256 - lyV);          
                if (ds4Style)
                {
                    s.Rx = ScaleAxis(a32);
                    s.Ry = ScaleAxis(256 - a35);
                    s.Lt = TriggerByte(a33);
                    s.Rt = TriggerByte(a34);
                }
                else
                {
                    s.Rx = ScaleAxis(a33);
                    s.Ry = ScaleAxis(256 - a34);
                    s.Lt = TriggerByte(a32);
                    s.Rt = TriggerByte(a35);
                }

                lock (_stateLock) _latest = s;
                return true;
            }
            catch { return false; }
        }

        private static int AxisRaw(byte[] b, IntPtr preparsed, ushort usage)
        {
            return HidP_GetUsageValue(HidP_Input, UsagePageGenericDesktop, 0, usage, out uint v, preparsed, b, (uint)b.Length) == HIDP_STATUS_SUCCESS
                ? (int)v : 0;
        }

        private static short ScaleAxis(int raw) => ClampAxis((raw - 128) * 257);

        private static byte TriggerByte(int raw) => (byte)Math.Clamp(raw, 0, 255);

        
        
        
        
        
        private static ushort MapButtonUsage(ushort usage, bool ds4Style)
        {
            if (ds4Style)
            {
                return usage switch
                {
                    1 => F_X,      
                    2 => F_A,      
                    3 => F_B,      
                    4 => F_Y,      
                    5 => F_LB,     
                    6 => F_RB,     
                    
                    9 => F_BACK,   
                    10 => F_START, 
                    11 => F_L3,    
                    12 => F_R3,    
                    _ => (ushort)0, 
                };
            }
            return usage switch
            {
                1 => F_X,      
                2 => F_A,      
                3 => F_B,      
                4 => F_Y,      
                5 => F_BACK,   
                6 => F_START,  
                7 => F_L3,     
                8 => F_R3,     
                9 => F_LB,     
                10 => F_RB,    
                _ => (ushort)0,
            };
        }

        
        private static short Axis(byte[] b, IntPtr preparsed, ushort usage, bool invert)
        {
            if (HidP_GetUsageValue(HidP_Input, UsagePageGenericDesktop, 0, usage, out uint v, preparsed, b, (uint)b.Length) != HIDP_STATUS_SUCCESS) return 0;
            int centered = (int)v - 128;
            if (invert) centered = -centered;
            return ClampAxis(centered * 257);
        }

        private static byte Trigger(byte[] b, IntPtr preparsed, ushort usage)
        {
            if (HidP_GetUsageValue(HidP_Input, UsagePageGenericDesktop, 0, usage, out uint v, preparsed, b, (uint)b.Length) != HIDP_STATUS_SUCCESS) return 0;
            return (byte)Math.Clamp(v, 0, 255);
        }

        
        private void ParseRaw(byte[] b)
        {
            var s = new PadState { Connected = true };

            s.Lx = ClampAxis((b[1] - 128) * 257);
            s.Ly = ClampAxis((128 - b[2]) * 257);   
            s.Rx = ClampAxis((b[3] - 128) * 257);
            s.Ry = ClampAxis((128 - b[4]) * 257);
            s.Lt = b[5];
            s.Rt = b[6];

            ushort btn = 0;
            byte b1 = b[8];
            if ((b1 & 0x01) != 0) btn |= F_X;          
            if ((b1 & 0x02) != 0) btn |= F_A;          
            if ((b1 & 0x04) != 0) btn |= F_B;          
            if ((b1 & 0x08) != 0) btn |= F_Y;          
            if ((b1 & 0x10) != 0) btn |= F_BACK;       
            if ((b1 & 0x20) != 0) btn |= F_START;      
            if ((b1 & 0x40) != 0) btn |= F_L3;
            if ((b1 & 0x80) != 0) btn |= F_R3;

            
            byte dpad = (byte)((b[9] >> 4) & 0x0F);
            if (dpad == 0) btn |= F_UP;
            else if (dpad == 1) btn |= F_UP | F_RIGHT;
            else if (dpad == 2) btn |= F_RIGHT;
            else if (dpad == 3) btn |= F_DOWN | F_RIGHT;
            else if (dpad == 4) btn |= F_DOWN;
            else if (dpad == 5) btn |= F_DOWN | F_LEFT;
            else if (dpad == 6) btn |= F_LEFT;
            else if (dpad == 7) btn |= F_UP | F_LEFT;

            s.Buttons = btn;
            lock (_stateLock) _latest = s;
        }

        private static short ClampAxis(int v) => (short)Math.Clamp(v, short.MinValue, short.MaxValue);
    }

    

    private const int GENERIC_READ = unchecked((int)0x80000000);
    private const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2;
    private const uint OPEN_EXISTING = 3;
    private const uint DIGCF_PRESENT = 0x2, DIGCF_DEVICEINTERFACE = 0x10, DIGCF_ALLCLASSES = 0x4;
    private const uint SPDRP_HARDWAREID = 0x00000001, SPDRP_COMPATIBLEIDS = 0x00000003;
    private const int HIDP_STATUS_SUCCESS = unchecked((int)0x00110000);
    private const int HidP_Input = 0;               
    private const ushort UsagePageButton = 0x09;    
    private const ushort UsagePageGenericDesktop = 0x01;
    private static readonly IntPtr InvalidHandle = new(-1);

    [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll")] private static extern bool HidD_GetPreparsedData(IntPtr h, out IntPtr preparsed);
    [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr preparsed);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr preparsed, ref HIDP_CAPS caps);

    [DllImport("hid.dll")]
    private static extern int HidP_GetUsages(int reportType, ushort usagePage, ushort linkCollection,
        [Out] ushort[] usageList, ref uint usageLength, IntPtr preparsed, byte[] report, uint reportLength);

    [DllImport("hid.dll")]
    private static extern int HidP_GetUsageValue(int reportType, ushort usagePage, ushort linkCollection,
        ushort usage, out uint usageValue, IntPtr preparsed, byte[] report, uint reportLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDP_CAPS
    {
        public ushort UsagePage, Usage;
        public ushort InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        public ushort R0, R1, R2, R3, R4, R5, R6, R7, R8, R9, R10, R11, R12, R13, R14, R15, R16; 
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
        public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid guid, IntPtr enumerator, IntPtr hwndParent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid guid, int index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detailBuffer, int detailSize, ref int neededSize, IntPtr devInfoData);
    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid interfaceClassGuid;
        public uint flags;
        public IntPtr reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public int cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, ref SP_DEVINFO_DATA devInfo, ref Guid guid, int index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr set, int index, ref SP_DEVINFO_DATA devInfo);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr set, ref SP_DEVINFO_DATA devInfo, uint property, out uint propertyRegDataType, byte[]? buffer, int bufferSize, out int requiredSize);
    
    
    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevsW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsAllClasses(IntPtr guid, IntPtr enumerator, IntPtr hwndParent, uint flags);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detailBuffer, int detailSize, ref int neededSize, ref SP_DEVINFO_DATA devInfoData);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref SP_DEVINFO_DATA devInfo, IntPtr buffer, int bufferSize, out int requiredSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileW(string name, int access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(IntPtr h, byte[] buffer, int toRead, out int read, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);
}
