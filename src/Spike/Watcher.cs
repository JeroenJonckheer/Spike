using System.Runtime.InteropServices;

namespace Spike;

/// <summary>
/// Looks at every running program about once a second and tells which remote-access
/// programs are running. A program is recognised by what it is (signature, version
/// information, folder), not by its name. Results are cached per file, so the second look
/// at a running program costs almost nothing.
/// Also watches every user's Downloads folder for installers of remote-access programs.
/// </summary>
internal sealed class Watcher : IDisposable
{
    internal sealed record ProcInfo(int Pid, int ParentPid, string Name, string? Path, string? Tool);

    private readonly Catalog _catalog;
    private readonly Dictionary<int, ProcInfo> _known = [];
    private readonly Dictionary<string, (long Size, long Written, ToolMatch? Match)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> _downloads = [];
    private readonly int _ownPid = Environment.ProcessId;

    /// <summary>An installer of a remote-access program appeared in a Downloads folder.</summary>
    internal event Action<string, string>? Downloaded;

    internal Watcher(Catalog catalog) => _catalog = catalog;

    /// <summary>Running remote-access programs (name → process ids) and the programs started since the last look.</summary>
    internal (Dictionary<string, List<int>> Running, List<ProcInfo> Started) Poll()
    {
        var running = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var started = new List<ProcInfo>();
        var alive = new HashSet<int>();

        foreach (var (pid, parent, name) in Snapshot())
        {
            alive.Add(pid);
            if (!_known.TryGetValue(pid, out var info))
            {
                var path = Native.ProcessPath(pid);
                var tool = pid == _ownPid || path is null ? null : Recognise(path)?.Tool.Name;
                info = new ProcInfo(pid, parent, name, path, tool);
                _known[pid] = info;
                started.Add(info);
            }
            if (info.Tool is { } t)
            {
                if (!running.TryGetValue(t, out var list)) running[t] = list = [];
                list.Add(pid);
            }
        }
        foreach (var gone in _known.Keys.Where(p => !alive.Contains(p)).ToList()) _known.Remove(gone);
        return (running, started);
    }

    /// <summary>Process ids of every running remote-access program, for the red button.</summary>
    internal List<int> ToolPids() => _known.Values.Where(p => p.Tool is not null).Select(p => p.Pid).ToList();

    internal ProcInfo? Process(int pid) => _known.GetValueOrDefault(pid);

    internal ToolMatch? Recognise(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) return null;
            long size = fi.Length, written = fi.LastWriteTimeUtc.Ticks;
            if (_cache.TryGetValue(path, out var hit) && hit.Size == size && hit.Written == written) return hit.Match;
            var match = _catalog.Find(Identify.Read(path));
            _cache[path] = (size, written, match);
            return match;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Starts watching C:\Users\*\Downloads.</summary>
    internal void WatchDownloads()
    {
        var users = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "Users");
        if (!Directory.Exists(users)) return;
        foreach (var profile in Directory.EnumerateDirectories(users))
        {
            var dl = Path.Combine(profile, "Downloads");
            if (!Directory.Exists(dl)) continue;
            try
            {
                var w = new FileSystemWatcher(dl) { IncludeSubdirectories = false, EnableRaisingEvents = true };
                w.Created += (_, e) => Check(e.FullPath);
                w.Renamed += (_, e) => Check(e.FullPath);
                _downloads.Add(w);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
    }

    private void Check(string path)
    {
        var ext = Path.GetExtension(path);
        if (!ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".msi", StringComparison.OrdinalIgnoreCase)) return;
        _ = Task.Run(async () =>
        {
            // The browser is still writing the file: wait until it can be read.
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(2000);
                try { using var _ = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read); break; }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { return; }
            }
            if (Recognise(path) is { } m) Downloaded?.Invoke(m.Tool.Name, path);
        });
    }

    // ---- Toolhelp: every process with its parent ----

    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize, cntUsage, th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID, cntThreads, th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32FirstW(IntPtr snap, ref PROCESSENTRY32W entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32NextW(IntPtr snap, ref PROCESSENTRY32W entry);

    private static List<(int Pid, int Parent, string Name)> Snapshot()
    {
        var list = new List<(int, int, string)>();
        IntPtr snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snap == new IntPtr(-1)) return list;
        try
        {
            var e = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            for (bool ok = Process32FirstW(snap, ref e); ok; ok = Process32NextW(snap, ref e))
                if (e.th32ProcessID > 4) list.Add(((int)e.th32ProcessID, (int)e.th32ParentProcessID, e.szExeFile));
        }
        finally { Native.CloseHandle(snap); }
        return list;
    }

    public void Dispose()
    {
        foreach (var w in _downloads) w.Dispose();
        _downloads.Clear();
    }
}
