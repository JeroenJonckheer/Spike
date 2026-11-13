using System.Runtime.InteropServices;

namespace Spike;

/// <summary>
/// Decides from a stream of input events whether someone else is operating the computer:
/// many "injected" mouse or keyboard events while the real mouse and keyboard are still.
/// Pure logic, so it can be tested.
/// </summary>
internal sealed class RemoteInputDetector
{
    internal const int WindowMs = 2000;   // look at the last two seconds
    internal const int MinEvents = 12;    // that many injected events …
    internal const int QuietMs = 2000;    // … while no real hand touched mouse or keyboard
    internal const int RepeatMs = 10000;  // tell the Guard at most every ten seconds

    private readonly Queue<long> _injected = new();
    private long _lastPhysical = long.MinValue / 2;
    private long _lastFired = long.MinValue / 2;

    /// <summary>Feeds one event; returns true when the Guard should be told.</summary>
    internal bool Feed(bool injected, long nowMs)
    {
        if (!injected)
        {
            _lastPhysical = nowMs;
            _injected.Clear();
            return false;
        }
        _injected.Enqueue(nowMs);
        while (_injected.Count > 0 && nowMs - _injected.Peek() > WindowMs) _injected.Dequeue();

        if (_injected.Count < MinEvents || nowMs - _lastPhysical < QuietMs || nowMs - _lastFired < RepeatMs) return false;
        _lastFired = nowMs;
        return true;
    }
}

/// <summary>
/// Low-level mouse and keyboard hooks in the Dog. Windows marks input that a program sends
/// (as remote-access programs do) with LLMHF_INJECTED / LLKHF_INJECTED; a real hand never has
/// that mark on real hardware. In a remote session (Remote Desktop, a virtual machine viewer)
/// all input is marked, so detection is off there.
/// </summary>
internal sealed class InputWatch : IDisposable
{
    private const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204, WM_MBUTTONDOWN = 0x0207;
    private const uint LLMHF_INJECTED = 0x01, LLMHF_LOWER_IL_INJECTED = 0x02;
    private const uint LLKHF_INJECTED = 0x10, LLKHF_LOWER_IL_INJECTED = 0x02;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public int X, Y; public uint mouseData, flags, time; public IntPtr extra; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr extra; }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int id, HookProc fn, IntPtr module, uint thread);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? name);

    private readonly HookProc _mouseProc, _keyProc;   // kept alive: Windows calls them
    private readonly IntPtr _mouseHook, _keyHook;
    private readonly RemoteInputDetector _detector = new();
    private long _lastPhysicalClick = long.MinValue / 2;

    /// <summary>Someone else seems to be operating the computer.</summary>
    internal event Action? RemoteInput;

    /// <summary>True when detection runs; false inside a remote session.</summary>
    internal bool Detecting { get; } = Native.GetSystemMetrics(Native.SM_REMOTESESSION) == 0;

    /// <summary>Must be created on a thread with a message loop (the Dog's UI thread).</summary>
    internal InputWatch()
    {
        _mouseProc = OnMouse;
        _keyProc = OnKey;
        var module = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
        _keyHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyProc, module, 0);
    }

    /// <summary>The last mouse button press came from a real hand, at most a second ago.</summary>
    internal bool RecentClickWasPhysical => Environment.TickCount64 - _lastPhysicalClick < 1000;

    private IntPtr OnMouse(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var m = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            bool injected = (m.flags & (LLMHF_INJECTED | LLMHF_LOWER_IL_INJECTED)) != 0;
            int msg = (int)wParam;
            if (!injected && msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN)
                _lastPhysicalClick = Environment.TickCount64;
            Feed(injected);
        }
        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            Feed((k.flags & (LLKHF_INJECTED | LLKHF_LOWER_IL_INJECTED)) != 0);
        }
        return CallNextHookEx(_keyHook, code, wParam, lParam);
    }

    private void Feed(bool injected)
    {
        if (Detecting && _detector.Feed(injected, Environment.TickCount64)) RemoteInput?.Invoke();
    }

    public void Dispose()
    {
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        if (_keyHook != IntPtr.Zero) UnhookWindowsHookEx(_keyHook);
    }
}
