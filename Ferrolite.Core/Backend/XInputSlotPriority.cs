using System.Runtime.InteropServices;

namespace Ferrolite.Core.Lang;


















internal static class XInputSlotPriority
{
    private const int CR_SUCCESS = 0;
    private const int CR_INVALID_DEVNODE = 5;    
    private const int CR_NO_SUCH_DEVNODE = 13;   
    private const int CR_REMOVE_VETOED = 23;     
    private const int CR_ACCESS_DENIED = 51;     
    private const uint CM_DISABLE_UI_NOT_OK = 0x00000001;
    private const uint CM_DISABLE_WILL_FORCE_INSTALL = 0x00000002;
    private const uint CM_PROB_DISABLED = 22;

    
    
    
    
    internal static string? LastDisableFailure { get; private set; }

    
    
    
    
    
    
    internal static bool DisableRunsInBroker { get; set; }

    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    internal static bool Disable(IReadOnlyList<string> instanceIds, out string reason)
    {
        if (instanceIds.Count == 0) { LastDisableFailure = reason = "no device instance paths to park"; return false; }

        
        
        
        
        var deviceNodes = instanceIds.Where(id => !id.Contains("&IG_")).ToList();
        if (deviceNodes.Count == 0) deviceNodes = instanceIds.ToList();
        var interfaces = instanceIds.Where(id => id.Contains("&IG_")).ToList();

        if (!DisableRunsInBroker)
            WriteRecord(instanceIds);
        int located = 0, lastCfgError = 0;
        bool deviceLetGo = false;
        foreach (var id in deviceNodes)
        {
            if (!Locate(id, out uint node)) continue;
            located++;
            int rc = CM_Disable_DevNode(node, CM_DISABLE_UI_NOT_OK);
            if (rc == CR_SUCCESS) deviceLetGo = true;
            else if (rc == CR_ACCESS_DENIED && !DisableRunsInBroker)
            {
                
                
                
                rc = CM_Disable_DevNode(node, CM_DISABLE_WILL_FORCE_INSTALL);
                if (rc == CR_SUCCESS) deviceLetGo = true;
            }
            if (rc != CR_SUCCESS) lastCfgError = rc;
        }

        if (!deviceLetGo)
        {
            if (!DisableRunsInBroker) TryDeleteRecord();
            LastDisableFailure = reason = located == 0
                ? "none of the " + deviceNodes.Count + " device paths resolved to a devnode"
                : Explain(lastCfgError);
            return false;
        }

        
        foreach (var id in interfaces)
        {
            if (!Locate(id, out uint node)) continue;
            CM_Disable_DevNode(node, CM_DISABLE_UI_NOT_OK);
        }
        LastDisableFailure = null;
        reason = "";
        return true;
    }

    
    
    
    
    
    private static string Explain(int cfgError) => cfgError switch
    {
        CR_REMOVE_VETOED =>
            "Windows refused to detach the pad from XInput (cfgmgr32 23, CR_REMOVE_VETOED): the XUSB driver " +
            "behind an Xbox-mode pad vetoes its own removal, so player 1 cannot be taken from it",
        CR_ACCESS_DENIED =>
            "Windows refused to detach the pad (cfgmgr32 51, access denied) — run FerroLite as administrator",
        CR_NO_SUCH_DEVNODE or CR_INVALID_DEVNODE =>
            "the pad re-enumerated while it was being parked (cfgmgr32 " + cfgError + "); re-arm",
        _ =>
            "Windows refused to detach the pad from XInput (cfgmgr32 " + cfgError + ")"
    };

    
    internal static void Enable(IReadOnlyList<string> instanceIds)
    {
        foreach (var id in instanceIds)
        {
            if (!Locate(id, out uint node)) continue;
            
            
            
            
            if (CM_Get_DevNode_Status(out _, out uint problem, node, 0) == CR_SUCCESS &&
                problem != CM_PROB_DISABLED)
                continue;
            CM_Enable_DevNode(node, 0);
        }
        if (!DisableRunsInBroker)
            TryDeleteRecord();
    }

    
    
    
    
    
    internal static void RecoverOnStartup()
    {
        var ids = ReadRecord();
        if (ids.Count == 0) return;
        foreach (var id in ids)
        {
            try
            {
                if (!Locate(id, out uint node)) continue;
                if (CM_Get_DevNode_Status(out _, out uint problem, node, 0) != CR_SUCCESS) continue;
                if (problem != CM_PROB_DISABLED) continue;
                CM_Enable_DevNode(node, 0);
            }
            catch { }
        }
        TryDeleteRecord();
    }

    private static bool Locate(string deviceId, out uint node) =>
        CM_Locate_DevNodeW(out node, deviceId, 0) == CR_SUCCESS;

    private static string RecordFile
    {
        get
        {
            string dir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "Ferrolite");
            return Path.Combine(dir, "xinput_slot_priority.txt");
        }
    }

    private static void WriteRecord(IReadOnlyList<string> ids)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RecordFile)!);
            File.WriteAllLines(RecordFile, ids);
        }
        catch { }
    }

    private static List<string> ReadRecord()
    {
        try
        {
            if (!File.Exists(RecordFile)) return new List<string>();
            return File.ReadAllLines(RecordFile).Where(l => l.Trim().Length > 0).ToList();
        }
        catch { return new List<string>(); }
    }

    private static void TryDeleteRecord()
    {
        try { File.Delete(RecordFile); } catch { }
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);
    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Disable_DevNode(uint devInst, uint flags);
    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Enable_DevNode(uint devInst, uint flags);
    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Get_DevNode_Status(out uint status, out uint problemNumber, uint devInst, uint flags);
}
