using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace BlazeImGui;

/// <summary>
/// A window the overlay should cover (usually a game). Set <see cref="OverlayOptions.Follow"/> to one of these
/// and the overlay keeps itself over the target's client area in physical screen pixels.
/// </summary>
public sealed class WindowTarget
{
    private readonly Func<IntPtr> _find;
    private IntPtr _window;
    private long _nextSearch;

    private WindowTarget(Func<IntPtr> find) => _find = find;

    /// <summary>The main window of a process, by id.</summary>
    public static WindowTarget FromProcessId(int processId) => new(() =>
    {
        try { using var process = Process.GetProcessById(processId); return process.MainWindowHandle; }
        catch (ArgumentException) { return IntPtr.Zero; } // The process exited.
    });

    /// <summary>The main window of the first running process with this name (without ".exe").</summary>
    public static WindowTarget FromProcessName(string processName) => new(() =>
    {
        foreach (var process in Process.GetProcessesByName(processName))
            using (process)
                if (process.MainWindowHandle != IntPtr.Zero) return process.MainWindowHandle;
        return IntPtr.Zero;
    });

    /// <summary>A top-level window by exact title.</summary>
    public static WindowTarget FromTitle(string title) => new(() => FindWindow(null, title));

    /// <summary>A window handle you already have.</summary>
    public static WindowTarget FromHandle(IntPtr handle) => new(() => handle);

    /// <summary>The target's handle, or zero while it is missing. Searches at most once a second.</summary>
    public IntPtr Handle
    {
        get
        {
            if (_window != IntPtr.Zero && IsWindow(_window)) return _window;
            _window = IntPtr.Zero;
            var now = Environment.TickCount64;
            if (now < _nextSearch) return IntPtr.Zero;
            _nextSearch = now + 1000;
            _window = _find();
            return _window;
        }
    }

    /// <summary>The target's client area in screen pixels; false while it is missing or minimized.</summary>
    public bool TryGetClientArea(out Rectangle area)
    {
        area = default;
        var window = Handle;
        if (window == IntPtr.Zero || IsIconic(window) || !GetClientRect(window, out var client)) return false;
        var origin = new POINT();
        if (!ClientToScreen(window, ref origin) || client.Right <= 0 || client.Bottom <= 0) return false;
        area = new(origin.X, origin.Y, client.Right, client.Bottom);
        return true;
    }

    /// <summary>True when the target or the overlay itself is the foreground window.</summary>
    public bool IsForeground(IntPtr overlay)
    {
        var foreground = GetForegroundWindow();
        return foreground != IntPtr.Zero && (foreground == _window || foreground == overlay);
    }

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out RECT rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref POINT point);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string title);
}
