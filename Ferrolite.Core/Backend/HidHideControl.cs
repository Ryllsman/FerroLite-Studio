using System.Runtime.InteropServices;

namespace Ferrolite.Core.Lang;






















public static class HidHideControl
{
    
    public enum HideMode { None, Session, Persistent }

    
    public static bool IsAvailable { get; private set; }

    
    public static string? LastHideFailure { get; private set; }

    
    public static bool Active { get; private set; }

    
    public static HideMode Mode { get; private set; } = HideMode.None;

    private static readonly object _lock = new();
    private static string[] _hiddenPaths = Array.Empty<string>();
    private static bool _activeWasOn = true;   

    
    
    
    
    
    
    private static string BrokerRequestDir
    {
        get
        {
            string dir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "Ferrolite", "hidhide-broker");
            return dir;
        }
    }

    private static string StateFile
    {
        get
        {
            string dir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "Ferrolite");
            return Path.Combine(dir, "hidhide_blacklist_state.txt");
        }
    }

    
    
    
    private static uint Ctl(uint fn) => (32769u << 16) | (1u << 14) | (fn << 2);

    private const uint IOCTL_GET_WHITELIST = 2048, IOCTL_SET_WHITELIST = 2049;
    private const uint IOCTL_GET_BLACKLIST = 2050, IOCTL_SET_BLACKLIST = 2051;
    private const uint IOCTL_GET_ACTIVE = 2052, IOCTL_SET_ACTIVE = 2053;
    private const uint IOCTL_ADD_SESSION_BLACKLIST = 2056;   
    private const uint IOCTL_CLR_SESSION_BLACKLIST = 2057;   

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2;
    private const uint OPEN_EXISTING = 3;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr h, uint ioctl, byte[]? inBuf, uint inSize, byte[]? outBuf, uint outSize, out uint returned, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint QueryDosDeviceW(string deviceName, System.Text.StringBuilder target, uint max);

    private static readonly IntPtr InvalidHandle = new(-1);

    
    public static string LastOpenFailure { get; private set; } = "";

    
    
    
    
    
    
    private static IntPtr OpenForWrite()
    {
        IntPtr h = CreateFileW(@"\\.\HidHide", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h == InvalidHandle)
        {
            int err = Marshal.GetLastWin32Error();
            LastOpenFailure = err == 5
                ? "access denied (HidHide requires an elevated process — the helper broker should handle this)"
                : "cannot open \\\\.\\HidHide (error " + err + ")";
        }
        return h;
    }

    static HidHideControl()
    {
        
        
        
        try { RecoverFromPreviousCrash(); } catch { }
    }

    
    
    
    
    
    
    
    public static bool EnsureWhitelisted()
    {
        string? exePath = System.Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return false;
        return EnsureWhitelistedFor(exePath);
    }

    
    
    
    
    public static bool EnsureWhitelistedFor(string exePath)
    {
        string? ntPath = NtDevicePath(exePath);
        if (ntPath == null) return false;

        lock (_lock)
        {
            IntPtr h = OpenForWrite();
            if (h == InvalidHandle) { IsAvailable = false; LastHideFailure = LastOpenFailure; return false; }
            try
            {
                IsAvailable = true;
                var current = ReadMultiSzList(h, Ctl(IOCTL_GET_WHITELIST));
                if (current.Any(p => string.Equals(p, ntPath, StringComparison.OrdinalIgnoreCase)))
                    return true;
                var updated = current.Append(ntPath).ToArray();
                byte[] buf = BuildMultiSz(updated);
                bool ok = DeviceIoControl(h, Ctl(IOCTL_SET_WHITELIST), buf, (uint)buf.Length, null, 0, out _, IntPtr.Zero);
                if (!ok) LastHideFailure = "whitelist IOCTL failed (error " + Marshal.GetLastWin32Error() + ")";
                return ok;
            }
            finally { CloseHandle(h); }
        }
    }

    
    private static List<string> ReadMultiSzList(IntPtr h, uint ioctl)
    {
        foreach (int size in new[] { 4096, 16384, 65536 })
        {
            var buf = new byte[size];
            if (DeviceIoControl(h, ioctl, null, 0, buf, (uint)size, out uint returned, IntPtr.Zero))
                return DecodeMultiSz(buf, returned);
        }
        return new List<string>();
    }

    private static List<string> DecodeMultiSz(byte[] buf, uint length)
    {
        var result = new List<string>();
        if (length < 2) return result;
        string joined = System.Text.Encoding.Unicode.GetString(buf, 0, (int)Math.Min(length, buf.Length));
        foreach (var part in joined.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            result.Add(part);
        return result;
    }

    
    
    
    
    private static string? NtDevicePath(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            if (full.Length < 2 || full[1] != ':') return null;
            string drive = full.Substring(0, 2);          
            var sb = new System.Text.StringBuilder(1024);
            uint len = QueryDosDeviceW(drive, sb, (uint)sb.Capacity);
            if (len == 0) return null;
            string ntRoot = sb.ToString().Split('\0', StringSplitOptions.RemoveEmptyEntries)[0];
            return ntRoot + full.Substring(2);            
        }
        catch { return null; }
    }

    
    
    
    
    
    
    
    public static bool TryHide(string[] deviceInstancePaths)
    {
        lock (_lock)
        {
            if (deviceInstancePaths.Length == 0) { LastHideFailure = "no device paths to hide"; return false; }
            IntPtr h = OpenForWrite();
            if (h == InvalidHandle)
            {
                IsAvailable = false;
                LastHideFailure = LastOpenFailure;
                return false;
            }
            try
            {
                IsAvailable = true;

                
                
                
                _activeWasOn = ReadActive(h);
                if (!_activeWasOn) SetActive(h, true);

                
                
                
                
                deviceInstancePaths = deviceInstancePaths.ToArray();

                
                ClearSession(h);
                bool sessionOk = true;
                foreach (var path in deviceInstancePaths)
                {
                    byte[] buf = BuildMultiSz(new[] { path });
                    if (!DeviceIoControl(h, Ctl(IOCTL_ADD_SESSION_BLACKLIST), buf, (uint)buf.Length, null, 0, out _, IntPtr.Zero))
                    {
                        sessionOk = false;
                        LastHideFailure = "session blacklist IOCTL failed (error " + Marshal.GetLastWin32Error() + ")";
                        break;
                    }
                }
                if (sessionOk)
                {
                    
                    
                    
                    
                    
                    _hiddenPaths = deviceInstancePaths;
                    Mode = HideMode.Session;
                    Active = true;
                    return true;
                }

                
                
                
                
                
                PurgeStalePersistentEntries(h);
                var currentList = ReadMultiSzList(h, Ctl(IOCTL_GET_BLACKLIST));
                var existing = currentList.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var ours = deviceInstancePaths.Where(p => !existing.Contains(p)).ToArray();

                
                
                
                WriteStateFile(ours);

                var combined = currentList.Concat(ours).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                byte[] set = BuildMultiSz(combined);
                if (!DeviceIoControl(h, Ctl(IOCTL_SET_BLACKLIST), set, (uint)set.Length, null, 0, out _, IntPtr.Zero))
                {
                    LastHideFailure = "persistent blacklist IOCTL failed (error " + Marshal.GetLastWin32Error() + ")";
                    try { File.Delete(StateFile); } catch { }
                    return false;
                }

                _hiddenPaths = deviceInstancePaths;
                Mode = HideMode.Persistent;
                Active = true;

                
                
                
                
                
                
                try
                {
                    if (EnforcementProbe(deviceInstancePaths))
                    {
                        return true;
                    }
                    LastHideFailure = "blacklist accepted but device still openable — driver not enforcing";
                    Active = false;
                    _hiddenPaths = Array.Empty<string>();
                    Mode = HideMode.None;
                    return false;
                }
                catch { return true; } 
            }
            finally { CloseHandle(h); }
        }
    }

    
    
    
    
    
    
    private static bool EnforcementProbe(string[] hiddenPaths)
    {
        foreach (var instance in hiddenPaths)
        {
            string? iface = InstancePathToInterfacePath(instance);
            if (iface == null) continue;
            IntPtr h = CreateFileW(iface, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == InvalidHandle)
            {
                int err = Marshal.GetLastWin32Error();
                if (err == 5) return true; 
                continue; 
            }
            CloseHandle(h);
            
            
            
            
            
            
            return true;
        }
        return true; 
    }

    
    
    
    private static string? InstancePathToInterfacePath(string instancePath)
    {
        try
        {
            HidD_GetHidGuid(out Guid hidGuid);
            IntPtr set = SetupDiGetClassDevsW(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
                DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == InvalidHandle) return null;
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
                        
                        string body = path.StartsWith("\\\\?\\") ? path.Substring(4) : path;
                        int hash = body.IndexOf("#{", StringComparison.Ordinal);
                        if (hash >= 0) body = body.Substring(0, hash);
                        body = body.Replace('#', '\\');
                        int last = body.LastIndexOf('\\');
                        string inst = last < 0 ? body
                            : body.Substring(0, last).ToUpperInvariant() + body.Substring(last);
                        if (string.Equals(inst, instancePath, StringComparison.OrdinalIgnoreCase))
                            return path;
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }
        catch { }
        return null;
    }

    
    
    
    

    
    public static string? LastReleaseFailure { get; private set; }

    
    
    
    
    
    
    
    
    
    
    
    public static void Release()
    {
        lock (_lock)
        {
            if (!Active && Mode == HideMode.None) return;
            LastReleaseFailure = null;
            
            
            
            
            var ours = (Mode == HideMode.Persistent ? ReadStateFile() : _hiddenPaths.ToList())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            
            string? unHideFailure = null;
            try
            {
                IntPtr h = CreateFileW(@"\\.\HidHide", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (h != InvalidHandle)
                {
                    try
                    {
                        if (Mode == HideMode.Session)
                        {
                            
                            
                            ClearSession(h);
                            
                            
                            
                            
                            
                            
                            if (ours.Count > 0 && PersistentStillHolds(h, ours))
                            {
                                if (PersistentUnHide(h, ours) && PersistentStillHolds(h, ours))
                                    unHideFailure = "persistent entries could not be removed (driver refused the write)";
                            }
                        }
                        else if (Mode == HideMode.Persistent && ours.Count > 0)
                        {
                            
                            if (PersistentUnHide(h, ours) && PersistentStillHolds(h, ours))
                                unHideFailure = "persistent entries could not be removed (driver refused the write)";
                        }
                    }
                    finally { CloseHandle(h); }
                }
                else
                {
                    
                    
                    unHideFailure = Mode == HideMode.Persistent
                        ? "cannot reach the HidHide driver to remove " + ours.Count + " hidden entr" + (ours.Count == 1 ? "y" : "ies")
                        : null; 
                }
            }
            catch (Exception ex)
            {
                unHideFailure = Mode == HideMode.Persistent
                    ? "un-hide failed: " + ex.Message
                    : null;
            }

            
            
            
            bool keepRecord = unHideFailure != null;
            if (!keepRecord) { try { File.Delete(StateFile); } catch { } }
            LastReleaseFailure = unHideFailure;

            _hiddenPaths = Array.Empty<string>();
            Mode = HideMode.None;
            Active = false;

            
            
            
            
            
            if (!_activeWasOn && !keepRecord)
            {
                try
                {
                    IntPtr h = CreateFileW(@"\\.\HidHide", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                    if (h != InvalidHandle)
                    {
                        try { SetActive(h, false); } finally { CloseHandle(h); }
                    }
                }
                catch { }
                _activeWasOn = true;
            }
        }
    }

    
    public static IReadOnlyList<string> HiddenPaths { get { lock (_lock) return _hiddenPaths; } }

    
    
    
    
    
    
    
    
    
    private static void PurgeStalePersistentEntries(IntPtr h)
    {
        try
        {
            var current = ReadMultiSzList(h, Ctl(IOCTL_GET_BLACKLIST));
            if (current.Count == 0) return;
            var stale = current.Where(p => !InstanceExists(p)).ToList();
            if (stale.Count == 0) return;
            var remaining = current.Where(p => InstanceExists(p)).ToArray();
            byte[] set = BuildMultiSz(remaining);
            DeviceIoControl(h, Ctl(IOCTL_SET_BLACKLIST), set, (uint)set.Length, null, 0, out _, IntPtr.Zero);
        }
        catch {  }
    }

    
    private static bool InstanceExists(string instanceId)
    {
        try
        {
            int res = CM_Locate_DevNodeW(out uint devInst, instanceId, 0);
            return res == 0; 
        }
        catch { return true; } 
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

    
    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid guid, IntPtr enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid guid, int index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int size, ref int needed, IntPtr dev);
    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    private const uint DIGCF_PRESENT = 2, DIGCF_DEVICEINTERFACE = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid ifaceGuid; public uint flags; public IntPtr extra; }

    
    private static bool PersistentUnHide(IntPtr h, HashSet<string> ours)
    {
        var remaining = ReadMultiSzList(h, Ctl(IOCTL_GET_BLACKLIST))
            .Where(p => !ours.Contains(p))
            .ToArray();
        byte[] set = BuildMultiSz(remaining);
        return DeviceIoControl(h, Ctl(IOCTL_SET_BLACKLIST), set, (uint)set.Length, null, 0, out _, IntPtr.Zero);
    }

    
    private static bool PersistentStillHolds(IntPtr h, HashSet<string> ours)
    {
        var current = ReadMultiSzList(h, Ctl(IOCTL_GET_BLACKLIST));
        return current.Any(ours.Contains);
    }

    private static void ClearSession(IntPtr h)
    {
        byte[] empty = BuildMultiSz(Array.Empty<string>());
        DeviceIoControl(h, Ctl(IOCTL_CLR_SESSION_BLACKLIST), empty, (uint)empty.Length, null, 0, out _, IntPtr.Zero);
    }

    private static bool ReadActive(IntPtr h)
    {
        var buf = new byte[1];
        return DeviceIoControl(h, Ctl(IOCTL_GET_ACTIVE), null, 0, buf, 1, out uint ret, IntPtr.Zero) && ret == 1 && buf[0] != 0;
    }

    private static void SetActive(IntPtr h, bool on)
    {
        byte[] buf = { on ? (byte)1 : (byte)0 };
        DeviceIoControl(h, Ctl(IOCTL_SET_ACTIVE), buf, (uint)buf.Length, null, 0, out _, IntPtr.Zero);
    }

    
    
    
    
    
    
    
    public static void RecordOwnership(IEnumerable<string> deviceInstancePaths)
    {
        var paths = deviceInstancePaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        if (paths.Length > 0) WriteStateFile(paths);
    }

    
    
    
    
    
    
    
    
    
    
    
    public static void RecoverFromPreviousRun()
    {
        RecoverFromPreviousCrash();
        RecoverOrphanedBrokerArms();
    }

    
    
    
    
    
    
    private static void RecoverOrphanedBrokerArms()
    {
        List<string> records;
        try { records = Directory.Exists(BrokerRequestDir) ? Directory.GetFiles(BrokerRequestDir, "armed-*.txt").ToList() : new List<string>(); }
        catch { return; }
        if (records.Count == 0) return;

        var recorded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in records)
            foreach (var line in SafeReadLines(file))
                recorded.Add(line);
        if (recorded.Count == 0) { ForeachDelete(records); return; }

        try
        {
            lock (_lock)
            {
                IntPtr h = CreateFileW(@"\\.\HidHide", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (h == InvalidHandle) return; 
                bool hideActiveBefore = ReadActive(h);
                try
                {
                    var current = ReadMultiSzList(h, Ctl(IOCTL_GET_BLACKLIST));
                    var remaining = current.Where(p => !recorded.Contains(p)).ToArray();
                    if (remaining.Length != current.Count)
                    {
                        byte[] set = BuildMultiSz(remaining);
                        DeviceIoControl(h, Ctl(IOCTL_SET_BLACKLIST), set, (uint)set.Length, null, 0, out _, IntPtr.Zero);
                    }
                    
                    
                    
                    if (remaining.Length == 0 && hideActiveBefore) SetActive(h, false);
                }
                finally { CloseHandle(h); }
            }
        }
        catch { return; }

        ForeachDelete(records);
    }

    private static List<string> SafeReadLines(string path)
    {
        try { return File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l)).ToList(); }
        catch { return new List<string>(); }
    }

    private static void ForeachDelete(IEnumerable<string> files)
    {
        foreach (var f in files) { try { File.Delete(f); } catch { } }
    }

    private static void WriteStateFile(string[] ours)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
            File.WriteAllLines(StateFile, ours);
        }
        catch {  }
    }

    private static List<string> ReadStateFile()
    {
        try
        {
            if (!File.Exists(StateFile)) return new List<string>();
            return File.ReadAllLines(StateFile)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
        }
        catch { return new List<string>(); }
    }

    private static void RecoverFromPreviousCrash()
    {
        var recorded = ReadStateFile();
        if (recorded.Count == 0) return;
        try
        {
            IntPtr h = CreateFileW(@"\\.\HidHide", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == InvalidHandle) return;
            try
            {
                var current = ReadMultiSzList(h, Ctl(IOCTL_GET_BLACKLIST));
                var ours = recorded.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var remaining = current.Where(p => !ours.Contains(p)).ToArray();
                byte[] set = BuildMultiSz(remaining);
                DeviceIoControl(h, Ctl(IOCTL_SET_BLACKLIST), set, (uint)set.Length, null, 0, out _, IntPtr.Zero);
            }
            finally { CloseHandle(h); }
        }
        finally
        {
            try { File.Delete(StateFile); } catch { }
        }
    }

    
    private static byte[] BuildMultiSz(string[] strings)
    {
        var chars = new List<char>();
        foreach (var s in strings) { chars.AddRange(s); chars.Add('\0'); }
        chars.Add('\0');
        if (chars.Count % 2 == 1) chars.Add('\0'); 
        var bytes = new byte[chars.Count * 2];
        Buffer.BlockCopy(chars.ToArray(), 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
