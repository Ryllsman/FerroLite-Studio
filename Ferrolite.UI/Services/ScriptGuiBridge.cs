using System.Collections.Concurrent;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Ferrolite.Core.Lang;
using Ferrolite.UI.Controls;

namespace Ferrolite.UI.Services;








public sealed class ScriptGuiBridge : IScriptGuiBridge
{
    private static ScriptGuiBridge? _current;
    public static ScriptGuiBridge Current
    {
        get => _current ?? throw new InvalidOperationException(
            "No ScriptGuiBridge registered. The Ferrolite UI must start before scripts that call gui_*() run.");
        internal set => _current = value;
    }

    private readonly ConcurrentDictionary<int, OverlayWindow> _overlays = new();
    private int _nextId = 1;
    private readonly ConcurrentDictionary<int, SessionBag> _sessionOverlays = new();
    private int _currentSessionId;

    
    
    
    
    public void AttachTo(Interpreter interp)
    {
        if (interp.ScriptGuiBridge is not null)
            throw new InvalidOperationException("Interpreter already has a ScriptGuiBridge attached.");
        interp.ScriptGuiBridge = this;
    }

    
    
    
    public void BeginSession(int sessionId)
    {
        _currentSessionId = sessionId;
        _sessionOverlays.GetOrAdd(sessionId, _ => new SessionBag());
    }

    
    
    
    
    public void CloseSession(int sessionId)
    {
        if (!_sessionOverlays.TryRemove(sessionId, out var _)) return;
        foreach (var bag in _sessionOverlays.Values)
            bag.Remove(sessionId);
        if (_currentSessionId == sessionId)
            _currentSessionId = 0;
    }

    

    public int CreateOverlay(string title, int width, int height)
    {
        return Dispatch(() =>
        {
            var id = _nextId++;
            var win = new OverlayWindow(title, width, height);
            _overlays[id] = win;
            if (_currentSessionId != 0 && _sessionOverlays.TryGetValue(_currentSessionId, out var bag))
                bag.Add(id);
            win.Show();
            win.Focusable = false;
            return id;
        });
    }

    public void CloseOverlay(int overlayId) => CloseOverlayImpl(overlayId);

    

    public int AddLabel(int overlayId, string text, int x, int y) =>
        WithWindow(overlayId, w => w.AddLabel(text, x, y));

    public int AddButton(int overlayId, string text, int x, int y) =>
        WithWindow(overlayId, w => w.AddButton(text, x, y));

    public int AddToggle(int overlayId, string text, int x, int y, bool @default = false) =>
        WithWindow(overlayId, w => w.AddToggle(text, x, y, @default));

    public void SetText(int overlayId, int elementId, string text)
    {
        if (!TryGet(overlayId, out var w) || w is null) return;
        Dispatch(() => { try { w.SetText(elementId, text); } catch { } });
    }

    public void SetElementPosition(int overlayId, int elementId, int x, int y)
    {
        if (!TryGet(overlayId, out var w) || w is null) return;
        Dispatch(() => { try { w.SetElementPosition(elementId, x, y); } catch { } });
    }

    public bool ButtonClicked(int overlayId, int elementId)
    {
        if (!TryGet(overlayId, out var w) || w is null) return false;
        return Dispatch(() => w.ButtonClicked(elementId));
    }

    public bool GetToggle(int overlayId, int elementId)
    {
        if (!TryGet(overlayId, out var w) || w is null) return false;
        return Dispatch(() => w.GetToggle(elementId));
    }

    public void RemoveElement(int overlayId, int elementId)
    {
        if (!TryGet(overlayId, out var w) || w is null) return;
        Dispatch(() => { try { w.RemoveElement(elementId); } catch { } });
    }

    public void SetAlwaysOnTop(int overlayId, bool enabled)
    {
        if (!TryGet(overlayId, out var w) || w is null) return;
        Dispatch(() => { try { w.SetAlwaysOnTop(enabled); } catch { } });
    }

    public void SetPosition(int overlayId, int x, int y)
    {
        if (!TryGet(overlayId, out var w) || w is null) return;
        Dispatch(() => { try { w.Move(x, y); } catch { } });
    }

    public void SetVisible(int overlayId, bool visible)
    {
        if (!TryGet(overlayId, out var w) || w is null) return;
        Dispatch(() => { try { w.SetVisible(visible); } catch { } });
    }

    

    private void CloseOverlayImpl(int overlayId)
    {
        if (!_overlays.TryRemove(overlayId, out var win) || win is null) return;
        Dispatch(() =>
        {
            try { win.CloseWindow(); } catch { }
            try { win.Close(); } catch { }
        });
        foreach (var bag in _sessionOverlays.Values)
            bag.Remove(overlayId);
    }

    private bool TryGet(int overlayId, out OverlayWindow? w)
    {
        w = _overlays.TryGetValue(overlayId, out var win) ? win : null;
        return w != null;
    }

    private T WithWindow<T>(int overlayId, Func<OverlayWindow, T> f)
    {
        if (!TryGet(overlayId, out var w) || w is null)
            throw new VSRuntimeError($"gui: unknown overlay id {overlayId}");
        return Dispatch(() => f(w));
    }

    private static T Dispatch<T>(Func<T> f)
    {
        var app = Application.Current;
        if (app?.Dispatcher.CheckAccess() == true)
            return f();
        var dispatcher = app?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
        return (T)dispatcher.Invoke(f);
    }

    private void Dispatch(Action f)
    {
        var app = Application.Current;
        if (app?.Dispatcher.CheckAccess() == true)
        {
            f();
            return;
        }
        var dispatcher = app?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
        dispatcher.Invoke(f);
    }

    private sealed class SessionBag
    {
        private readonly object _lock = new();
        private readonly HashSet<int> _ids = new();

        public void Add(int id) { lock (_lock) _ids.Add(id); }
        public bool Remove(int id) { lock (_lock) return _ids.Remove(id); }
    }
}
