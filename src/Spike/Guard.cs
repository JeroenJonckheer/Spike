using System.Diagnostics;
using System.Globalization;

namespace Spike;

/// <summary>
/// The Guard: runs in the Windows service as SYSTEM. Looks at the running programs every
/// second, raises the alarm through the Dogs, freezes and unfreezes programs, presses the red
/// button and keeps the evidence. It also makes sure a Dog runs for every signed-in user.
/// </summary>
internal sealed class Guard : IDisposable
{
    private readonly object _sync = new();
    private readonly Catalog _catalog = Catalog.Load();
    private readonly AlarmState _state = new();
    private readonly Dictionary<int, Evidence> _evidence = [];
    private readonly Watcher _watcher;
    private readonly Panic _panic;
    private readonly bool _startDogs;
    private readonly string _exe = Environment.ProcessPath ?? "Spike.exe";
    private PipeServer? _pipe;
    private System.Threading.Timer? _timer;
    private Settings _settings = Settings.Load();
    private long _ticks;

    internal Guard(bool startDogs)
    {
        _startDogs = startDogs;
        _watcher = new Watcher(_catalog);
        _panic = new Panic(_catalog);
        Text.Current = Text.FromCode(_settings.Language);
    }

    internal static string LogFile => Path.Combine(Settings.Folder, "spike.log");

    internal static void Log(string line)
    {
        try
        {
            Directory.CreateDirectory(Settings.Folder);
            var fi = new FileInfo(LogFile);
            if (fi.Exists && fi.Length > 1_000_000) File.Move(LogFile, LogFile + ".1", true);
            File.AppendAllText(LogFile, $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}  {line}\r\n");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    internal void Start()
    {
        Log("Spike started");
        _panic.RestoreAfterRestart();
        foreach (var l in _panic.Log) Log(l);
        _panic.Log.Clear();
        Evidence.Prune(Evidence.Root, TimeSpan.FromDays(90), 2L * 1024 * 1024 * 1024, DateTime.Now);

        _pipe = new PipeServer();
        _pipe.Received += OnMessage;
        _watcher.Downloaded += (tool, path) =>
        {
            Log($"download of {tool}: {path}");
            _pipe?.Broadcast(new PipeMessage { Type = PipeMessage.Download, Tool = tool });
        };
        _watcher.WatchDownloads();
        _timer = new System.Threading.Timer(_ => Tick(), null, 0, 1000);
    }

    /// <summary>The service is being stopped: warn the user, it may be a scammer doing it.</summary>
    internal void Stopping()
    {
        Log("Spike is being stopped");
        _pipe?.Broadcast(new PipeMessage { Type = PipeMessage.StopAttempt });
        Thread.Sleep(1500);
    }

    private void Tick()
    {
        lock (_sync)
        {
            try
            {
                var now = DateTime.Now;
                var (running, started) = _watcher.Poll();
                if (_ticks > 0)
                    foreach (var e in _evidence.Values)
                        foreach (var p in started) e.Started(p);
                Apply(_state.Seen(running, now), now);

                if (_evidence.Count > 0 && _ticks % 2 == 0) RecordConnections();
                if (_startDogs && _ticks % 5 == 0) EnsureDogs();
                if (_ticks % 30 == 0) _settings = Settings.Load();
            }
            catch (Exception e) { Log($"error: {e}"); }
            _ticks++;
        }
    }

    private void RecordConnections()
    {
        var pids = _state.Open.SelectMany(i => i.Pids).Concat(_watcher.ToolPids()).ToHashSet();
        if (pids.Count == 0) return;
        var all = Native.TcpConnections();
        foreach (var e in _evidence.Values)
            e.Connections(all, pids, pid => _watcher.Process(pid)?.Name ?? "?");
    }

    private void OnMessage(PipeMessage m)
    {
        lock (_sync)
        {
            var now = DateTime.Now;
            switch (m.Type)
            {
                case PipeMessage.Choice when m.Id > 0 && m.Pick is PipeMessage.Distrust or PipeMessage.Trusted:
                    if (_evidence.TryGetValue(m.Id, out var ev)) ev.Choice(m.Pick!, _settings.HelpersOrDefault[0].Name);
                    Log($"choice for incident {m.Id}: {m.Pick}");
                    Apply(_state.Choose(m.Id, m.Pick!, now), now);
                    break;
                case PipeMessage.RemoteInput:
                    Apply(_state.RemoteInput(now), now);
                    break;
                case PipeMessage.Screenshot when m.Data is not null:
                    var bytes = Convert.FromBase64String(m.Data);
                    foreach (var e in _evidence.Values) e.Screenshot(bytes);
                    break;
                case PipeMessage.TestAlarm:
                    _pipe?.Broadcast(new PipeMessage { Type = PipeMessage.Alarm, Id = 0, Tool = "AnyDesk", Test = true });
                    break;
            }
        }
    }

    private void Apply(List<AlarmState.Action> actions, DateTime now)
    {
        foreach (var action in actions)
        {
            switch (action)
            {
                case AlarmState.RaiseAlarm a:
                    var ev = new Evidence(a.Tool, now);
                    ev.WatchFiles(UserFolders());
                    _evidence[a.Id] = ev;
                    _toolNames[a.Id] = a.Tool;
                    Log($"ALARM {a.Id}: {(a.Tool.Length == 0 ? "remote input" : a.Tool)}");
                    _pipe?.Broadcast(new PipeMessage { Type = PipeMessage.Alarm, Id = a.Id, Tool = a.Tool });
                    _pipe?.Broadcast(new PipeMessage { Type = PipeMessage.Record, On = true });
                    break;
                case AlarmState.Freeze f:
                    foreach (var pid in f.Pids) Log($"freeze {pid}: {Native.Suspend(pid, true)}");
                    break;
                case AlarmState.Unfreeze u:
                    foreach (var pid in u.Pids) Log($"unfreeze {pid}: {Native.Suspend(pid, false)}");
                    break;
                case AlarmState.ShutDown s:
                    _panic.Log.Clear();
                    _panic.Execute(_watcher.ToolPids(), path => _watcher.Recognise(path));
                    foreach (var (id, e) in _evidence)
                    {
                        foreach (var l in _panic.Log) e.Log(l);
                        e.CopyToolLogs(ToolOf(id), Profiles());
                        e.Finish(now);
                        e.Dispose();
                    }
                    _evidence.Clear();
                    foreach (var l in _panic.Log) Log(l);
                    _pipe?.Broadcast(new PipeMessage { Type = PipeMessage.Offline, Id = s.Id });
                    _pipe?.Broadcast(new PipeMessage { Type = PipeMessage.Record, On = false });
                    break;
                case AlarmState.ShowBanner b:
                    _pipe?.Broadcast(new PipeMessage { Type = PipeMessage.Banner, Id = b.Id, Tool = b.Tool });
                    break;
                case AlarmState.HideBanner h:
                    Close(h.Id, now);
                    _pipe?.Broadcast(new PipeMessage { Type = PipeMessage.BannerEnd, Id = h.Id });
                    break;
                case AlarmState.EndAlarm e:
                    Close(e.Id, now);
                    _pipe?.Broadcast(new PipeMessage { Type = PipeMessage.AlarmEnd, Id = e.Id });
                    break;
            }
        }
    }

    private readonly Dictionary<int, string> _toolNames = [];

    private Tool? ToolOf(int id)
    {
        var name = _state.Find(id)?.Tool ?? _toolNames.GetValueOrDefault(id);
        return _catalog.Tools.FirstOrDefault(t => t.Name == name);
    }

    private void Close(int id, DateTime now)
    {
        if (!_evidence.Remove(id, out var e)) return;
        e.CopyToolLogs(ToolOf(id), Profiles());
        e.Finish(now);
        e.Dispose();
        Log($"incident {id} ended");
        if (_evidence.Count == 0) _pipe?.Broadcast(new PipeMessage { Type = PipeMessage.Record, On = false });
    }

    private static IEnumerable<string> Profiles()
    {
        var users = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "Users");
        if (!Directory.Exists(users)) return [];
        return Directory.EnumerateDirectories(users)
            .Where(d => !Path.GetFileName(d).Equals("Public", StringComparison.OrdinalIgnoreCase)
                        && !Path.GetFileName(d).StartsWith("Default", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> UserFolders() =>
        Profiles().SelectMany(p => new[] { "Desktop", "Documents", "Downloads", "Pictures" }.Select(f => Path.Combine(p, f)));

    /// <summary>Starts a Dog in every active session that has none (also after it was closed).</summary>
    private void EnsureDogs()
    {
        var name = Path.GetFileNameWithoutExtension(_exe);
        var withDog = Process.GetProcessesByName(name)
            .Where(p => p.Id != Environment.ProcessId)
            .Select(p => { try { return p.SessionId; } catch (InvalidOperationException) { return -1; } })
            .ToHashSet();
        foreach (var session in Native.ActiveSessions())
        {
            if (withDog.Contains(session)) continue;
            int pid = Native.StartInSession(session, _exe, "--dog");
            Log($"started the Dog in session {session}: {pid}");
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        lock (_sync)
        {
            foreach (var e in _evidence.Values) { e.Finish(DateTime.Now); e.Dispose(); }
            _evidence.Clear();
        }
        _watcher.Dispose();
        _pipe?.Dispose();
        Log("Spike stopped");
    }
}
