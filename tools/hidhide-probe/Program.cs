using System.Runtime.InteropServices;
using System.Text;

namespace HidHideProbeTool;









internal static class Program
{
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2;
    private const uint OPEN_EXISTING = 3;

    private const uint IOCTL_GET_WHITELIST = 2048, IOCTL_SET_WHITELIST = 2049;
    private const uint IOCTL_GET_BLACKLIST = 2050, IOCTL_SET_BLACKLIST = 2051;
    private const uint IOCTL_GET_ACTIVE = 2052, IOCTL_SET_ACTIVE = 2053;

    private static readonly IntPtr InvalidHandle = new(-1);

    
    private static uint Ctl(uint fn) => (32769u << 16) | (1u << 14) | (fn << 2);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sec, uint creation, uint flags, IntPtr tmpl);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr h, uint ioctl, byte[]? inBuf, uint inSize, byte[] outBuf, uint outSize, out uint returned, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);

    private static int Main(string[] args)
    {
        bool repair = args.Any(a => string.Equals(a, "--repair", StringComparison.OrdinalIgnoreCase));

        IntPtr h = CreateFileW(@"\\.\HidHide", GENERIC_READ | (repair ? GENERIC_WRITE : 0), FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h == InvalidHandle)
        {
            int err = Marshal.GetLastWin32Error();
            Console.WriteLine($"HidHide: NOT readable — Win32 error {err}");
            Console.WriteLine(err == 5
                ? "(access denied — --repair needs an elevated prompt)"
                : "(the read path needs no admin, so this normally means the driver is not installed)");
            return 1;
        }

        try
        {
            Console.WriteLine("=== HidHide driver state ===");
            bool? active = ReadActive(h);
            Console.WriteLine("ACTIVE (master switch) : " + (active is null ? "unknown" : active.Value.ToString()));

            Console.WriteLine();
            Console.WriteLine("--- persistent BLACKLIST (hidden from every app) ---");
            Print(ReadList(h, IOCTL_GET_BLACKLIST));

            Console.WriteLine();
            Console.WriteLine("--- persistent WHITELIST (apps allowed to see hidden devices) ---");
            Print(ReadList(h, IOCTL_GET_WHITELIST));

            if (repair)
            {
                Console.WriteLine();
                Console.WriteLine("--- repair ---");
                Repair(h);
            }
        }
        finally
        {
            CloseHandle(h);
        }
        return 0;
    }

    
    
    
    
    
    
    
    private static void Repair(IntPtr h)
    {
        var current = ReadList(h, IOCTL_GET_BLACKLIST);
        var ours = current.Where(IsFerroLiteControllerEntry).ToArray();
        if (ours.Length == 0)
        {
            Console.WriteLine("  nothing to repair — no leftover controller entries found");
            return;
        }

        var remaining = current.Where(p => !ours.Contains(p, StringComparer.OrdinalIgnoreCase)).ToArray();
        byte[] set = BuildMultiSz(remaining);
        bool ok = DeviceIoControl(h, Ctl(IOCTL_SET_BLACKLIST), set, (uint)set.Length, Array.Empty<byte>(), 0, out _, IntPtr.Zero);
        if (!ok)
        {
            Console.WriteLine($"  FAILED to write the blacklist (Win32 error {Marshal.GetLastWin32Error()}) — run this elevated");
            return;
        }
        foreach (var p in ours) Console.WriteLine("  removed: " + p);

        if (remaining.Length == 0)
        {
            bool? wasActive = ReadActive(h);
            if (wasActive == true)
            {
                byte[] on = { 0 };
                DeviceIoControl(h, Ctl(IOCTL_SET_ACTIVE), on, 1, Array.Empty<byte>(), 0, out _, IntPtr.Zero);
                Console.WriteLine("  master switch: switched OFF (the blacklist is empty)");
            }
        }
        Console.WriteLine("  done — your controller is visible to every application again");
    }

    private static bool IsFerroLiteControllerEntry(string path)
    {
        string p = path.ToUpperInvariant();
        
        
        return p.Contains("VID_054C") || p.Contains("VJOY") || p.Contains("HIDCLASS");
    }

    private static byte[] BuildMultiSz(string[] strings)
    {
        var chars = new List<char>();
        foreach (string s in strings) { chars.AddRange(s); chars.Add('\0'); }
        chars.Add('\0');
        if (chars.Count % 2 == 1) chars.Add('\0');
        var bytes = new byte[chars.Count * 2];
        Buffer.BlockCopy(chars.ToArray(), 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static void Print(List<string> items)
    {
        if (items.Count == 0) { Console.WriteLine("  (empty)"); return; }
        foreach (string s in items) Console.WriteLine("  " + s);
    }

    private static bool? ReadActive(IntPtr h)
    {
        var buf = new byte[1];
        if (!DeviceIoControl(h, Ctl(IOCTL_GET_ACTIVE), null, 0, buf, 1, out uint ret, IntPtr.Zero)) return null;
        return ret == 1 && buf[0] != 0;
    }

    private static List<string> ReadList(IntPtr h, uint ioctl)
    {
        var result = new List<string>();
        foreach (int size in new[] { 4096, 16384, 65536 })
        {
            var buf = new byte[size];
            if (!DeviceIoControl(h, Ctl(ioctl), null, 0, buf, (uint)size, out uint ret, IntPtr.Zero)) continue;
            if (ret < 2) return result;
            string joined = Encoding.Unicode.GetString(buf, 0, (int)Math.Min(ret, (uint)buf.Length));
            foreach (string part in joined.Split('\0'))
                if (part.Length > 0) result.Add(part);
            return result;
        }
        return result;
    }
}
