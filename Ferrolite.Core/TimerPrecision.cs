using System.Runtime.InteropServices;

namespace Ferrolite.Core;







public static class TimerPrecision
{
    [DllImport("winmm.dll")]
    private static extern int timeBeginPeriod(int ms);

    [DllImport("winmm.dll")]
    private static extern int timeEndPeriod(int ms);

    private static int? _activePeriodMs;
    private static int _lockCount;
    private static readonly object Lock = new();

    public static bool BeginHighResTimer(int periodMs = 1)
    {
        lock (Lock)
        {
            if (_activePeriodMs != null) { _lockCount++; return true; }
            if (timeBeginPeriod(periodMs) == 0)
            {
                _activePeriodMs = periodMs;
                _lockCount = 1;
                return true;
            }
            return false;
        }
    }

    public static void EndHighResTimer()
    {
        lock (Lock)
        {
            if (_activePeriodMs == null) return;
            _lockCount = Math.Max(0, _lockCount - 1);
            if (_lockCount == 0)
            {
                timeEndPeriod(_activePeriodMs.Value);
                _activePeriodMs = null;
            }
        }
    }

    private static readonly double SpinWindowSec = 0.002;

    
    public static void PreciseSleep(double seconds)
    {
        if (seconds <= 0) return;
        var target = System.Diagnostics.Stopwatch.GetTimestamp() + (long)(seconds * System.Diagnostics.Stopwatch.Frequency);
        while (true)
        {
            double remaining = (target - System.Diagnostics.Stopwatch.GetTimestamp()) / (double)System.Diagnostics.Stopwatch.Frequency;
            if (remaining <= 0) return;
            if (remaining > SpinWindowSec)
                Thread.Sleep(TimeSpan.FromSeconds(Math.Min(remaining - SpinWindowSec, 0.010)));
        }
    }
}
