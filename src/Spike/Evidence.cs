using System.Globalization;
using System.Net;
using System.Text;

namespace Spike;

/// <summary>
/// The evidence folder of one incident, under %ProgramData%\Spike\Bewijs. Written by the Guard
/// (SYSTEM); users may read it but not delete it. Everything stays on this computer.
/// </summary>
internal sealed class Evidence : IDisposable
{
    internal static string Root => Path.Combine(Settings.Folder, "Bewijs");

    internal string Folder { get; }

    private readonly string _tool;
    private readonly DateTime _started;
    private readonly HashSet<string> _connections = [];
    private readonly HashSet<string> _addresses = [];
    private readonly List<FileSystemWatcher> _fileWatchers = [];
    private readonly object _lock = new();
    private int _shots;
    private string? _choice;

    internal Evidence(string tool, DateTime started, string? root = null)
    {
        _tool = tool;
        _started = started;
        var name = $"{started:yyyy-MM-dd HH.mm.ss} {Safe(tool)}";
        Folder = Path.Combine(root ?? Root, name);
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory(Path.Combine(Folder, "scherm"));
        Write("verbindingen.csv", "tijd;proces-id;programma;lokaal;extern;status\r\n");
        Write("processen.csv", "tijd;proces-id;ouder-id;naam;pad\r\n");
        Write("bestanden.csv", "tijd;gebeurtenis;pad\r\n");
        Log($"{Text.Current.SummaryProgram}: {tool}");
    }

    private static string Safe(string s)
    {
        var name = string.IsNullOrWhiteSpace(s) ? "onbekend" : s;
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    private static string Now => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private void Write(string file, string text)
    {
        lock (_lock)
        {
            try { File.AppendAllText(Path.Combine(Folder, file), text, Encoding.UTF8); }
            catch (IOException) { }
        }
    }

    internal void Log(string line) => Write("logboek.txt", $"{Now}  {line}\r\n");

    internal void Choice(string choice, string helperName)
    {
        _choice = choice == PipeMessage.Distrust ? Text.Current.ChoiceDistrust : string.Format(Text.Current.ChoiceTrusted, helperName);
        Log($"{Text.Current.SummaryChoice}: {_choice}");
    }

    /// <summary>Records the connections of the given processes; each connection once.</summary>
    internal void Connections(IEnumerable<Native.Connection> all, IReadOnlySet<int> pids, Func<int, string> name)
    {
        var sb = new StringBuilder();
        foreach (var c in all)
        {
            if (!pids.Contains(c.Pid) || IPAddress.IsLoopback(c.Remote.Address) || c.Remote.Address.Equals(IPAddress.Any) || c.Remote.Address.Equals(IPAddress.IPv6Any)) continue;
            var key = $"{c.Pid};{c.Local};{c.Remote}";
            if (!_connections.Add(key)) continue;
            _addresses.Add(c.Remote.Address.ToString());
            sb.Append($"{Now};{c.Pid};{name(c.Pid)};{c.Local};{c.Remote};{c.State}\r\n");
        }
        if (sb.Length > 0) Write("verbindingen.csv", sb.ToString());
    }

    internal void Started(Watcher.ProcInfo p) =>
        Write("processen.csv", $"{Now};{p.Pid};{p.ParentPid};{p.Name};{p.Path}\r\n");

    /// <summary>Watches the users' personal folders while the incident lasts.</summary>
    internal void WatchFiles(IEnumerable<string> folders)
    {
        foreach (var f in folders.Where(Directory.Exists))
        {
            try
            {
                var w = new FileSystemWatcher(f) { IncludeSubdirectories = true, EnableRaisingEvents = true };
                w.Created += (_, e) => Write("bestanden.csv", $"{Now};aangemaakt;{e.FullPath}\r\n");
                w.Changed += (_, e) => Write("bestanden.csv", $"{Now};gewijzigd;{e.FullPath}\r\n");
                w.Deleted += (_, e) => Write("bestanden.csv", $"{Now};verwijderd;{e.FullPath}\r\n");
                w.Renamed += (_, e) => Write("bestanden.csv", $"{Now};hernoemd;{e.OldFullPath} -> {e.FullPath}\r\n");
                _fileWatchers.Add(w);
            }
            catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { }
        }
    }

    internal void Screenshot(byte[] jpeg)
    {
        int n = Interlocked.Increment(ref _shots);
        try { File.WriteAllBytes(Path.Combine(Folder, "scherm", $"{DateTime.Now:HH.mm.ss}-{n:D4}.jpg"), jpeg); }
        catch (IOException) { }
    }

    /// <summary>Copies the program's own logs, which hold the other side's ID and IP address.</summary>
    internal void CopyToolLogs(Tool? tool, IEnumerable<string> profiles)
    {
        if (tool is null) return;
        var target = Path.Combine(Folder, "programma-logs");
        foreach (var pattern in tool.Logs)
        {
            foreach (var file in Expand(pattern, profiles))
            {
                try
                {
                    Directory.CreateDirectory(target);
                    var dest = Path.Combine(target, file.Replace(':', '_').Replace('\\', '_'));
                    using var src = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var dst = File.Create(dest);
                    src.CopyTo(dst);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>A log path from the catalog for every user profile; * is allowed in the file name.</summary>
    internal static IEnumerable<string> Expand(string pattern, IEnumerable<string> profiles)
    {
        var p = pattern.Replace(@"\\", @"\");
        var candidates = new List<string>();
        if (p.Contains("%APPDATA%", StringComparison.OrdinalIgnoreCase) || p.Contains("%LOCALAPPDATA%", StringComparison.OrdinalIgnoreCase) || p.Contains("%USERPROFILE%", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var profile in profiles)
                candidates.Add(p.Replace("%APPDATA%", Path.Combine(profile, @"AppData\Roaming"), StringComparison.OrdinalIgnoreCase)
                                .Replace("%LOCALAPPDATA%", Path.Combine(profile, @"AppData\Local"), StringComparison.OrdinalIgnoreCase)
                                .Replace("%USERPROFILE%", profile, StringComparison.OrdinalIgnoreCase));
        }
        else if (p.StartsWith(@"*\", StringComparison.Ordinal))
        {
            foreach (var profile in profiles) candidates.Add(Path.Combine(profile, p[2..]));
        }
        else candidates.Add(Environment.ExpandEnvironmentVariables(p));

        foreach (var c in candidates)
        {
            if (c.Contains('*', StringComparison.Ordinal))
            {
                var dir = Path.GetDirectoryName(c);
                if (dir is null || dir.Contains('*', StringComparison.Ordinal) || !Directory.Exists(dir)) continue;
                foreach (var f in Directory.EnumerateFiles(dir, Path.GetFileName(c))) yield return f;
            }
            else if (File.Exists(c)) yield return c;
        }
    }

    /// <summary>Writes the one-page summary for the police or the bank.</summary>
    internal void Finish(DateTime ended)
    {
        var t = Text.Current;
        var sb = new StringBuilder();
        sb.AppendLine(t.SummaryTitle).AppendLine();
        sb.AppendLine($"{t.SummaryStart}: {_started:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"{t.SummaryEnd}: {ended:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"{t.SummaryProgram}: {(string.IsNullOrEmpty(_tool) ? t.UnknownProgram : _tool)}");
        sb.AppendLine($"{t.SummaryChoice}: {_choice ?? t.ChoiceNone}");
        sb.AppendLine($"Computer: {Environment.MachineName}").AppendLine();
        sb.AppendLine($"{t.SummaryAddresses}:");
        foreach (var a in _addresses.Order()) sb.AppendLine($"  {a}");
        sb.AppendLine().AppendLine(t.SummaryMore);
        Write("samenvatting.txt", sb.ToString());
        Log(t.SummaryEnd);
    }

    /// <summary>Removes the oldest incidents beyond the age or size limit. Summaries are kept.</summary>
    internal static void Prune(string root, TimeSpan maxAge, long maxBytes, DateTime now)
    {
        if (!Directory.Exists(root)) return;
        var folders = new DirectoryInfo(root).GetDirectories().OrderBy(d => d.CreationTimeUtc).ToList();
        long total = folders.Sum(Size);
        foreach (var d in folders)
        {
            bool old = now.ToUniversalTime() - d.CreationTimeUtc > maxAge;
            if (!old && total <= maxBytes) break;
            long before = Size(d);
            foreach (var f in d.EnumerateFiles("*", SearchOption.AllDirectories))
                if (!f.Name.Equals("samenvatting.txt", StringComparison.OrdinalIgnoreCase))
                    try { f.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            total -= before - Size(d);
        }

        static long Size(DirectoryInfo d) =>
            d.Exists ? d.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Exists ? f.Length : 0) : 0;
    }

    public void Dispose()
    {
        foreach (var w in _fileWatchers) w.Dispose();
        _fileWatchers.Clear();
    }
}
