using System.Diagnostics;
using System.IO;

namespace Ferrolite.Core.Lang;

























public static class HidHideElevation
{
    private static readonly object _lock = new();
    private static Process? _broker;
    private static string? _lastError;

    
    public static string? LastError { get { lock (_lock) return _lastError; } }

    private static string WorkDir
    {
        get
        {
            string dir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "Ferrolite", "hidhide-broker");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    
    
    
    
    
    
    public static bool TryArmViaHelper(string[] deviceInstancePaths)
    {
        lock (_lock)
        {
            _lastError = null;
            try
            {
                if (deviceInstancePaths.Length == 0)
                {
                    _lastError = "no device paths to hide";
                    return false;
                }

                string exe = System.Environment.ProcessPath ?? "";
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                {
                    _lastError = "cannot locate own executable for helper launch";
                    return false;
                }

                string dir = WorkDir;
                string id = Guid.NewGuid().ToString("N");
                string requestFile = Path.Combine(dir, $"req-{id}.txt");
                string resultFile = Path.Combine(dir, $"res-{id}.txt");

                
                
                File.WriteAllLines(requestFile, deviceInstancePaths);
                try { File.Delete(resultFile); } catch { }

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "--hidhide-broker \"" + requestFile + "\" \"" + resultFile + "\" " + Process.GetCurrentProcess().Id,
                    UseShellExecute = true,
                    Verb = "runas",                    
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                _broker = Process.Start(psi);
                if (_broker == null)
                {
                    _lastError = "helper process failed to start";
                    return false;
                }

                
                
                
                bool ok = false;
                for (int waited = 0; waited < 30_000; waited += 250)
                {
                    if (File.Exists(resultFile))
                    {
                        
                        Thread.Sleep(150);
                        ok = File.ReadAllText(resultFile).Trim() == "OK";
                        break;
                    }
                    if (_broker.HasExited && !File.Exists(resultFile)) {  }
                    Thread.Sleep(250);
                }

                if (!ok && _lastError == null)
                    _lastError = "helper did not report success (UAC denied, timeout, or IOCTL failure)";
                return ok;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                _lastError = "UAC prompt declined";
                return false;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return false;
            }
        }
    }

    
    
    
    
    
    
    internal static bool IsElevated =>
        System.Environment.IsPrivilegedProcess;

    
    public static void AskHelperToDisarm()
    {
        lock (_lock)
        {
            try
            {
                
                
                
                string disarm = Path.Combine(WorkDir, "disarm.txt");
                File.WriteAllText(disarm, "disarm");
            }
            catch { }
        }
    }

    
    
    
    
    
    
    
    
    
    
    
    

    
    
    
    
    
    
    
    
    
    
    public static void RunSlotParkBroker(string requestFile, string resultFile, int parentPid)
    {
        var ids = new List<string>();
        try
        {
            ids = File.ReadAllLines(requestFile)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .ToList();

            
            
            
            if (!XInputSlotPriority.Disable(ids, out string why))
            {
                try { File.WriteAllText(resultFile, "FAIL " + why); } catch { }
                return;
            }
            try { File.WriteAllText(resultFile, "OK"); } catch { }

            
            
            FileStream? poll = null;
            try { poll = new FileStream(requestFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); } catch { }
            try
            {
                using var parent = parentPid > 0 ? SafeGetProcess(parentPid) : null;
                while (true)
                {
                    if (IsRequestFileGone(requestFile)) break;
                    if (parent != null && parent.HasExited) break;
                    Thread.Sleep(100);
                }
            }
            finally { poll?.Dispose(); }
        }
        catch
        {
            try { File.WriteAllText(resultFile, "FAIL " + (XInputSlotPriority.LastDisableFailure ?? "broker error")); } catch { }
        }
        finally
        {
            
            
            try { XInputSlotPriority.Enable(ids); } catch { }
        }
    }

    private static bool IsRequestFileGone(string requestFile)
    {
        try { return !File.Exists(requestFile); }
        catch { return false; } 
    }

    
    
    

    private static string? _lastSlotParkError;

    
    public static string? LastSlotParkError { get { lock (_lock) return _lastSlotParkError; } }

    private static string SlotParkWorkDir
    {
        get
        {
            string dir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "Ferrolite", "slot-park-broker");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    
    
    
    
    
    
    
    
    public static bool TryStartSlotParkBroker(IReadOnlyList<string> instanceIds, out string why, out string requestFile)
    {
        lock (_lock)
        {
            _lastSlotParkError = why = "";
            requestFile = "";
            try
            {
                string exe = System.Environment.ProcessPath ?? "";
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                {
                    why = "cannot locate own executable for helper launch";
                    _lastSlotParkError = why;
                    return false;
                }

                string dir = SlotParkWorkDir;
                string id = Guid.NewGuid().ToString("N");
                requestFile = Path.Combine(dir, $"park-{id}.txt");
                string resultFile = Path.Combine(dir, $"park-{id}.result.txt");

                File.WriteAllLines(requestFile, instanceIds);
                try { File.Delete(resultFile); } catch { }

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "--slot-park-broker \"" + requestFile + "\" \"" + resultFile + "\" " + Process.GetCurrentProcess().Id,
                    UseShellExecute = true,
                    Verb = "runas",                    
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    why = "elevated helper process failed to start";
                    _lastSlotParkError = why;
                    return false;
                }

                bool ok = false;
                for (int waited = 0; waited < 45_000; waited += 250)
                {
                    if (File.Exists(resultFile))
                    {
                        Thread.Sleep(150); 
                        ok = File.ReadAllText(resultFile).Trim() == "OK";
                        if (!ok) _lastSlotParkError = XInputSlotPriority.LastDisableFailure ?? "Windows refused to park the physical pad";
                        break;
                    }
                    if (proc.HasExited && !File.Exists(resultFile)) {  }
                    Thread.Sleep(250);
                }
                if (!ok && string.IsNullOrEmpty(_lastSlotParkError))
                    _lastSlotParkError = why = "helper did not report success (UAC denied, timeout, or device refusal)";
                else why = _lastSlotParkError ?? "";
                return ok;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                why = "UAC prompt declined";
                _lastSlotParkError = why;
                return false;
            }
            catch (Exception ex)
            {
                why = ex.Message;
                _lastSlotParkError = why;
                return false;
            }
        }
    }

    
    
    
    
    
    
    
    public static FileStream OpenParkSignal(string requestFile)
    {
        try
        {
            return new FileStream(requestFile, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch
        {
            
            
            throw new FileNotFoundException();
        }
    }

    
    public static void WaitSlotParkExit(int timeoutMs)
    {
        for (int waited = 0; waited < timeoutMs; waited += 100)
        {
            try
            {
                bool any = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(System.Environment.ProcessPath ?? ""))
                    .Any(p =>
                    {
                        try { return p.Id != Process.GetCurrentProcess().Id && p.SessionId == Process.GetCurrentProcess().SessionId; }
                        catch { return false; }
                    });
                foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(System.Environment.ProcessPath ?? "")))
                    try { p.Dispose(); } catch { }
                if (!any) return;
            }
            catch { return; }
            Thread.Sleep(100);
        }
    }

    
    
    
    

    
    
    
    
    
    
    
    public static void RunBroker(string requestFile, string resultFile, int parentPid)
    {
        try
        {
            var paths = File.ReadAllLines(requestFile)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .ToArray();

            
            
            string? parentExe = null;
            try
            {
                using var cur = Process.GetCurrentProcess();
                parentExe = cur.MainModule?.FileName;
            }
            catch { }

            if (!string.IsNullOrEmpty(parentExe))
                HidHideControl.EnsureWhitelistedFor(parentExe);

            
            
            
            
            
            
            
            HidHideControl.RecordOwnership(paths);
            
            
            
            try
            {
                string armed = Path.Combine(Path.GetDirectoryName(requestFile) ?? ".", "armed-" + Path.GetFileName(requestFile));
                File.Move(requestFile, armed, overwrite: true);
                requestFile = armed;
            }
            catch {  }

            bool applied = paths.Length == 0 || HidHideControl.TryHide(paths);
            if (applied)
            {
                
                
                
                
                
                try { File.WriteAllText(resultFile, "OK"); } catch { }
            }
            else
            {
                string why = HidHideControl.LastHideFailure ?? "unknown IOCTL failure";
                try { File.WriteAllText(resultFile, "FAIL " + why); } catch { }
                
                
                return;
            }

            WatchParentAndRestore(parentPid);
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(resultFile, "FAIL " + ex.Message); } catch { }
        }
        finally
        {
            
            
            
            try { File.Delete(requestFile); } catch { }
        }
    }

    
    
    
    
    
    
    private static void WatchParentAndRestore(int parentPid)
    {
        string disarmFile = Path.Combine(WorkDir, "disarm.txt");

        using var parent = parentPid > 0 ? SafeGetProcess(parentPid) : null;
        while (true)
        {
            Thread.Sleep(500);
            try { if (File.Exists(disarmFile)) { try { File.Delete(disarmFile); } catch { } break; } } catch { }
            if (parent != null && parent.HasExited) break;
        }

        try { HidHideControl.Release(); } catch { }
    }

    private static Process? SafeGetProcess(int pid)
    {
        try { return Process.GetProcessById(pid); } catch { return null; }
    }
}
