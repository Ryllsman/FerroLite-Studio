using System.IO;
using System.Text.Json;
using Ferrolite.Core.Recorder;

namespace Ferrolite.UI.Services;

public static class AppStorage
{
    public static string AppDataDir
    {
        get
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ferrolite");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string RecordingsDir
    {
        get
        {
            string dir = Path.Combine(AppDataDir, "recordings");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string SettingsPath => Path.Combine(AppDataDir, "settings.json");
    public static string RecordingsFilter => "Ferrolite Recording (*.frr)|*.frr|All Files (*.*)|*.*";

    
    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    
    public static string? SettingsSaveError { get; private set; }

    
    
    
    
    
    
    public static bool SaveSettings(AppSettings s)
    {
        SettingsSaveError = null;
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch (Exception ex)
        {
            SettingsSaveError = ex.Message;
            return false;
        }
    }

    
    public static string SaveRecording(List<RecordedEvent> events)
    {
        string name = $"ferrolite_{DateTime.Now:yyyyMMdd_HHmmss}.frr";
        string path = Path.Combine(RecordingsDir, name);
        
        
        var doc = new Dictionary<string, object?>
        {
            ["recorded_at"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["events"] = events.Select(e => e.ToDict()).ToList(),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    public static List<RecordedEvent> LoadRecording(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement arr = doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("events", out var ev)
            ? ev
            : doc.RootElement; 
        var list = new List<RecordedEvent>();
        foreach (var el in arr.EnumerateArray())
        {
            var d = new Dictionary<string, object?>();
            foreach (var p in el.EnumerateObject())
                d[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.Number => p.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => p.Value.GetString(),
                    _ => null,
                };
            list.Add(RecordedEvent.FromDict(d));
        }
        return list;
    }

    public static IEnumerable<RecordingInfo> ListRecordings()
    {
        if (!Directory.Exists(RecordingsDir)) yield break;
        foreach (var f in Directory.GetFiles(RecordingsDir, "*.frr").OrderByDescending(f => f))
        {
            var info = new FileInfo(f);
            int count = 0;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(f));
                count = doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("events", out var ev)
                    ? ev.GetArrayLength()
                    : doc.RootElement.GetArrayLength();
            }
            catch { }
            yield return new RecordingInfo
            {
                Path = f,
                Name = Path.GetFileName(f),
                Count = count,
                Date = info.LastWriteTime,
                Size = info.Length,
            };
        }
    }

    
    private static string RecentsPath => Path.Combine(AppDataDir, "recent.json");

    public static void AddRecentFile(string path)
    {
        try
        {
            var list = Load();
            list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, path);
            if (list.Count > 10) list.RemoveAt(list.Count - 1);
            File.WriteAllText(RecentsPath, JsonSerializer.Serialize(list));
        }
        catch { }
    }

    public static List<string> Load()
    {
        try
        {
            if (File.Exists(RecentsPath))
                return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentsPath)) ?? new List<string>();
        }
        catch { }
        return new List<string>();
    }

    
    public static List<string> LoadRecentFiles() => Load();

    
    private static string SessionPath => Path.Combine(AppDataDir, "session.json");

    
    
    
    
    
    
    public static bool SaveSession(SessionState s)
    {
        try
        {
            File.WriteAllText(SessionPath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch { return false; }
    }

    public static SessionState LoadSession()
    {
        try
        {
            if (File.Exists(SessionPath))
                return JsonSerializer.Deserialize<SessionState>(File.ReadAllText(SessionPath)) ?? new SessionState();
        }
        catch { }
        return new SessionState();
    }
}

public class AppSettings
{
    public double FontSize { get; set; } = 14;
    public bool AutoIndent { get; set; } = true;
    public bool ConfirmOnRun { get; set; }
    public double MoveThrottle { get; set; } = 0.05;
    
    public string CaptureMode { get; set; } = "absolute";

    
    
    
    public string VirtualController { get; set; } = "x360";
}







public class SessionState
{
    
    public double WindowX { get; set; }
    public double WindowY { get; set; }
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    
    public bool TerminalVisible { get; set; } = true;

    public List<SessionTab> Tabs { get; set; } = new();
    
    public int ActiveTab { get; set; }
}




public class SessionTab
{
    public string? FilePath { get; set; }
    
    public string? UnsavedText { get; set; }
    public int CaretLine { get; set; } = 1;
    public int CaretColumn { get; set; } = 1;
}

public class RecordingInfo
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public DateTime Date { get; set; }
    public long Size { get; set; }
}
