using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace Spike;

/// <summary>The Windows functions Spike calls directly, with their real names and values.</summary>
internal static class Native
{
    // ---- Processes ----

    internal const uint PROCESS_TERMINATE = 0x0001;
    internal const uint PROCESS_SUSPEND_RESUME = 0x0800;
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref uint size);

    [DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(IntPtr process);

    [DllImport("ntdll.dll")]
    private static extern int NtResumeProcess(IntPtr process);

    /// <summary>Full path of a running program, or null when Windows does not tell.</summary>
    internal static string? ProcessPath(int pid)
    {
        IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            uint size = (uint)sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString(0, (int)size) : null;
        }
        finally { CloseHandle(h); }
    }

    /// <summary>Freezes (true) or unfreezes (false) every thread of a process.</summary>
    internal static bool Suspend(int pid, bool suspend)
    {
        IntPtr h = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
        if (h == IntPtr.Zero) return false;
        try { return (suspend ? NtSuspendProcess(h) : NtResumeProcess(h)) == 0; }
        finally { CloseHandle(h); }
    }

    // ---- Connections ----

    private const int AF_INET = 2, AF_INET6 = 23;
    private const int TCP_TABLE_OWNER_PID_ALL = 5;

    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool sort, int family, int tableClass, uint reserved);

    internal sealed record Connection(int Pid, IPEndPoint Local, IPEndPoint Remote, int State);

    /// <summary>All TCP connections (IPv4 and IPv6) with the process that owns them.</summary>
    internal static List<Connection> TcpConnections()
    {
        var list = new List<Connection>();
        Read(AF_INET, 24, row =>
        {
            int state = Marshal.ReadInt32(row, 0);
            var local = new IPEndPoint(new IPAddress((uint)Marshal.ReadInt32(row, 4)), Port(row, 8));
            var remote = new IPEndPoint(new IPAddress((uint)Marshal.ReadInt32(row, 12)), Port(row, 16));
            list.Add(new Connection(Marshal.ReadInt32(row, 20), local, remote, state));
        });
        Read(AF_INET6, 56, row =>
        {
            var localAddr = new byte[16];
            var remoteAddr = new byte[16];
            Marshal.Copy(row, localAddr, 0, 16);
            Marshal.Copy(row + 24, remoteAddr, 0, 16);
            var local = new IPEndPoint(new IPAddress(localAddr), Port(row, 20));
            var remote = new IPEndPoint(new IPAddress(remoteAddr), Port(row, 44));
            list.Add(new Connection(Marshal.ReadInt32(row, 52), local, remote, Marshal.ReadInt32(row, 48)));
        });
        return list;

        static int Port(IntPtr row, int offset) =>
            (Marshal.ReadByte(row, offset) << 8) | Marshal.ReadByte(row, offset + 1);

        static void Read(int family, int rowSize, Action<IntPtr> each)
        {
            int size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, TCP_TABLE_OWNER_PID_ALL, 0);
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (GetExtendedTcpTable(buffer, ref size, false, family, TCP_TABLE_OWNER_PID_ALL, 0) != 0) return;
                int count = Marshal.ReadInt32(buffer);
                for (int i = 0; i < count; i++) each(buffer + 4 + i * rowSize);
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
    }

    // ---- Sessions: starting the Dog for each signed-in user ----

    private const int WTSActive = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WTS_SESSION_INFO
    {
        public int SessionId;
        public string pWinStationName;
        public int State;
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSEnumerateSessions(IntPtr server, int reserved, int version, out IntPtr info, out int count);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(int sessionId, out IntPtr token);

    /// <summary>Sessions where someone is signed in and active.</summary>
    internal static List<int> ActiveSessions()
    {
        var ids = new List<int>();
        if (!WTSEnumerateSessions(IntPtr.Zero, 0, 1, out IntPtr info, out int count)) return ids;
        try
        {
            int size = Marshal.SizeOf<WTS_SESSION_INFO>();
            for (int i = 0; i < count; i++)
            {
                var s = Marshal.PtrToStructure<WTS_SESSION_INFO>(info + i * size);
                if (s.State == WTSActive && s.SessionId != 0) ids.Add(s.SessionId);
            }
        }
        finally { WTSFreeMemory(info); }
        return ids;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(IntPtr token, string? app, StringBuilder cmd, IntPtr pa, IntPtr ta,
        bool inherit, uint flags, IntPtr env, string? dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr env, IntPtr token, bool inherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr env);

    /// <summary>Starts a program on the desktop of a signed-in user. Only works from a LocalSystem service.</summary>
    internal static int StartInSession(int sessionId, string exe, string arguments)
    {
        if (!WTSQueryUserToken(sessionId, out IntPtr token)) return 0;
        IntPtr env = IntPtr.Zero;
        try
        {
            CreateEnvironmentBlock(out env, token, false);
            var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>(), lpDesktop = @"winsta0\default" };
            var cmd = new StringBuilder($"\"{exe}\" {arguments}");
            if (!CreateProcessAsUser(token, exe, cmd, IntPtr.Zero, IntPtr.Zero, false, CREATE_UNICODE_ENVIRONMENT, env,
                    Path.GetDirectoryName(exe), ref si, out var pi)) return 0;
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            return pi.dwProcessId;
        }
        finally
        {
            if (env != IntPtr.Zero) DestroyEnvironmentBlock(env);
            CloseHandle(token);
        }
    }

    [DllImport("kernel32.dll")]
    internal static extern bool ProcessIdToSessionId(int pid, out int sessionId);

    // ---- Misc ----

    internal const int SM_REMOTESESSION = 0x1000;

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int index);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetNamedPipeClientProcessId(IntPtr pipe, out int pid);
}
