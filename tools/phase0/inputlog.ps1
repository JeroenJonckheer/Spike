# Phase 0: logs every mouse and keyboard event with Windows' "injected" flags, to see
# whether a remote-access program's input can be told apart from a real hand.
#   mouse:    LLMHF_INJECTED 0x01, LLMHF_LOWER_IL_INJECTED 0x02
#   keyboard: LLKHF_INJECTED 0x10, LLKHF_LOWER_IL_INJECTED 0x02
# Writes C:\phase0\out\input.csv; one line per event, mouse moves at most every 50 ms.

Add-Type -ReferencedAssemblies System.Windows.Forms -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class InputLog
{
    const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
    const int WM_MOUSEMOVE = 0x0200;

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr extra; }
    delegate IntPtr Proc(int code, IntPtr w, IntPtr l);

    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, Proc fn, IntPtr mod, uint thread);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string name);

    static StreamWriter _w;
    static Proc _mouse = Mouse, _keys = Keys;
    static uint _lastMove;

    public static void Run(string path)
    {
        _w = new StreamWriter(path, true) { AutoFlush = true };
        _w.WriteLine("time,device,message,flags,injected,x_or_vk");
        IntPtr mod = GetModuleHandle(null);
        SetWindowsHookEx(WH_MOUSE_LL, _mouse, mod, 0);
        SetWindowsHookEx(WH_KEYBOARD_LL, _keys, mod, 0);
        Application.Run();
    }

    static IntPtr Mouse(int code, IntPtr w, IntPtr l)
    {
        var m = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(l, typeof(MSLLHOOKSTRUCT));
        int msg = (int)w;
        if (msg != WM_MOUSEMOVE || m.time - _lastMove >= 50)
        {
            if (msg == WM_MOUSEMOVE) _lastMove = m.time;
            _w.WriteLine(string.Format("{0:HH:mm:ss.fff},mouse,0x{1:X4},0x{2:X2},{3},{4};{5}",
                DateTime.Now, msg, m.flags, (m.flags & 0x03) != 0, m.pt.X, m.pt.Y));
        }
        return CallNextHookEx(IntPtr.Zero, code, w, l);
    }

    static IntPtr Keys(int code, IntPtr w, IntPtr l)
    {
        var k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(l, typeof(KBDLLHOOKSTRUCT));
        _w.WriteLine(string.Format("{0:HH:mm:ss.fff},key,0x{1:X4},0x{2:X2},{3},{4}",
            DateTime.Now, (int)w, k.flags, (k.flags & 0x12) != 0, k.vkCode));
        return CallNextHookEx(IntPtr.Zero, code, w, l);
    }
}
'@

[InputLog]::Run('C:\phase0\out\input.csv')
