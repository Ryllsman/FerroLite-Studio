using System.Diagnostics;
using Ferrolite.Core.Lang;
using Ferrolite.Core.Recorder;

namespace Ferrolite.UI.Services;





public class RawPlaybackService
{
    public event Action<double, int>? FinishedOk;   
    public event Action<string>? FinishedError;

    private readonly object _lock = new();
    private bool _stopped;
    private bool _paused;
    private double _speed = 1.0;

    public bool IsPlaying { get; private set; }
    public Thread? Thread { get; private set; }

    public void Play(List<RecordedEvent> events)
    {
        Stop();
        var list = events.Where(e => e.Included).ToList();
        if (list.Count == 0) return;

        lock (_lock)
        {
            _stopped = false;
            _paused = false;
        }
        IsPlaying = true;
        var snapshot = list;

        Thread = new Thread(() =>
        {
            var backend = new WindowsBackend();
            var gamepad = new WindowsControllerBackend();
            var sw = Stopwatch.StartNew();
            double baseTs = snapshot[0].Timestamp;
            int idx = 0;
            try
            {
                foreach (var ev in snapshot)
                {
                    lock (_lock)
                    {
                        if (_stopped) break;
                        while (_paused && !_stopped)
                        {
                            Monitor.Wait(_lock, 80);
                            sw.Restart();
                            baseTs = ev.Timestamp;
                        }
                    }
                    double target = (ev.Timestamp - baseTs) / _speed;
                    double wait = target - sw.Elapsed.TotalSeconds;
                    SleepInterruptible(wait);

                    switch (ev.Type)
                    {
                        case EventType.MouseMove:
                            backend.MouseMove(ev.X ?? 0, ev.Y ?? 0, 0);
                            break;
                        case EventType.MouseClick:
                            backend.MouseClick(ev.X, ev.Y, ev.Button ?? "left");
                            break;
                        case EventType.MouseDown:
                            backend.MouseDown(ev.Button ?? "left");
                            break;
                        case EventType.MouseUp:
                            backend.MouseUp(ev.Button ?? "left");
                            break;
                        case EventType.MouseScroll:
                            backend.MouseScroll(ev.ScrollDy ?? 0, ev.X, ev.Y);
                            break;
                        case EventType.KeyDown:
                            backend.KeyDown(ev.Key ?? "a");
                            break;
                        case EventType.KeyUp:
                            backend.KeyUp(ev.Key ?? "a");
                            break;
                        case EventType.ControllerButtonPress:
                            gamepad.GamepadPress(ev.ControllerId ?? 0, ev.Button ?? "gamepad_a");
                            break;
                        case EventType.ControllerButtonRelease:
                            gamepad.GamepadRelease(ev.ControllerId ?? 0, ev.Button ?? "gamepad_a");
                            break;
                        case EventType.ControllerStickMove:
                            gamepad.GamepadStick(ev.ControllerId ?? 0, ev.Button ?? "left_stick", ev.StickX ?? 0, ev.StickY ?? 0);
                            break;
                        case EventType.ControllerTriggerMove:
                            gamepad.GamepadTrigger(ev.ControllerId ?? 0, ev.Button ?? "left_trigger", ev.TriggerValue ?? 0);
                            break;
                        case EventType.ControllerDPadMove:
                            gamepad.GamepadDPad(ev.ControllerId ?? 0, ev.DPadDirection ?? "up");
                            break;
                    }
                    idx++;
                }
                sw.Stop();
                if (StoppedRequested())
                    FinishedError?.Invoke("Playback stopped by user.");
                else
                    FinishedOk?.Invoke(sw.Elapsed.TotalSeconds, idx);
            }
            catch (Exception e)
            {
                FinishedError?.Invoke(e.Message);
            }
            finally
            {
                IsPlaying = false;
            }
        })
        { IsBackground = true, Name = "FerroliteRawPlayback" };
        Thread.Start();
    }

    private bool StoppedRequested()
    {
        lock (_lock) return _stopped;
    }

    private void SleepInterruptible(double seconds)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            lock (_lock)
            {
                if (_stopped) return;
                while (_paused && !_stopped)
                {
                    Monitor.Wait(_lock, 80);
                    sw.Restart();
                }
            }
            Thread.Sleep(20);
        }
    }

    public void Stop()
    {
        lock (_lock) _stopped = true;
        if (Thread != null && Thread.IsAlive)
        {
            Thread.Join(TimeSpan.FromMilliseconds(400));
        }
        IsPlaying = false;
    }

    public void Pause(bool paused)
    {
        lock (_lock)
        {
            _paused = paused;
            if (!paused) Monitor.PulseAll(_lock);
        }
    }
}
