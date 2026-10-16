using System.IO;
using System.Windows;
using System.Windows.Threading;
using Ferrolite.Core.Lang;
using Ferrolite.UI.Services;

namespace Ferrolite.UI;

public partial class App : Application
{
    private static void LogCrash(string stage, Exception ex)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "ferrolite_crash.log"),
                $"[{stage}] {DateTime.Now:O}\n{ex}\n");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        
        
        
        
        string[] args = e.Args;
        if (args.Length >= 3 && args[0] == "--hidhide-broker")
        {
            int pid = args.Length >= 4 && int.TryParse(args[3], out int p) ? p : -1;
            HidHideElevation.RunBroker(args[1], args[2], pid);
            System.Environment.Exit(0);
            return;
        }

        
        
        
        
        
        if (args.Length >= 3 && args[0] == "--slot-park-broker")
        {
            int pid = args.Length >= 4 && int.TryParse(args[3], out int p) ? p : -1;
            HidHideElevation.RunSlotParkBroker(args[1], args[2], pid);
            System.Environment.Exit(0);
            return;
        }

        
        
        
        
        
        
        if (args.Length >= 1 && args[0] == "--recover-hidhide")
        {
            int code = 1;
            try
            {
                HidHideControl.RecoverFromPreviousRun();
                ControllerPassthrough.RecoverParkedPads();
                code = 0;
            }
            catch { code = 1; }
            System.Environment.Exit(code);
            return;
        }

        
        
        
        
        
        
        try { HidHideControl.RecoverFromPreviousRun(); } catch { }

        
        
        
        try { ControllerPassthrough.RecoverParkedPads(); } catch { }

        ScriptGuiBridge.Current = new ScriptGuiBridge();
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("dispatcher", args.Exception);
            args.Handled = true;
        };

        try
        {
            var splash = new Views.SplashWindow();
            splash.Show();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
            timer.Tick += (_, _) =>
            {
                try
                {
                    timer.Stop();
                    var main = new Views.MainWindow();
                    main.Show();
                    splash.Close();
                }
                catch (Exception ex)
                {
                    LogCrash("main", ex);
                    throw;
                }
            };
            timer.Start();
        }            catch (Exception ex)
        {
            LogCrash("startup", ex);
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { ScriptGuiBridge.Current?.CloseSession(_currentSessionId); } catch { }
        
        
        
        try { if (ControllerPassthrough.Active) ControllerPassthrough.Stop(); } catch { }
        base.OnExit(e);
    }

    private int _currentSessionId;
    internal void SetScriptSession(int sessionId)
    {
        _currentSessionId = sessionId;
        try { ScriptGuiBridge.Current?.BeginSession(sessionId); } catch { }
    }
    internal void ClearScriptSession()
    {
        try { ScriptGuiBridge.Current?.CloseSession(_currentSessionId); } catch { }
        _currentSessionId = 0;
    }
}
