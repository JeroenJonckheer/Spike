namespace Spike;

/// <summary>
/// Decides what happens when remote-access programs come and go and the user makes a choice.
/// Pure logic: it returns what to do and the <see cref="Guard"/> does it.
///
/// Rules:
/// - A program that starts gives an alarm and is frozen until the user chooses.
/// - "I don't trust this" shuts everything down, also later, after "my helper".
/// - "My helper is on the phone" lets that program continue with a banner. Trust is per
///   connection: a new incoming connection of the same program gives a new alarm, and so
///   does someone moving the mouse again after <see cref="TrustIdle"/> of quiet.
/// - "My helper is done" closes the program.
/// - Someone else moving the mouse without a known program gives an alarm for an unknown
///   program. After "my helper" it stays quiet for <see cref="QuietAfterTrustedUnknown"/>.
/// </summary>
internal sealed class AlarmState
{
    internal const string Unknown = "";
    internal static readonly TimeSpan QuietAfterTrustedUnknown = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan TrustIdle = TimeSpan.FromMinutes(3);

    internal abstract record Action;
    internal sealed record RaiseAlarm(int Id, string Tool, bool Again = false) : Action;
    internal sealed record EndAlarm(int Id) : Action;
    internal sealed record Freeze(IReadOnlyList<int> Pids) : Action;
    internal sealed record Unfreeze(IReadOnlyList<int> Pids) : Action;
    internal sealed record ShutDown(int Id) : Action;
    internal sealed record CloseTool(int Id, IReadOnlyList<int> Pids) : Action;
    internal sealed record ShowBanner(int Id, string Tool) : Action;
    internal sealed record HideBanner(int Id) : Action;

    internal sealed class Incident(int id, string tool, DateTime started)
    {
        public int Id { get; } = id;
        public string Tool { get; } = tool;
        public DateTime Started { get; } = started;
        public HashSet<int> Pids { get; } = [];
        public string? Choice { get; set; }
        public DateTime LastRemote { get; set; } = started;
        public bool Trusted => Choice == PipeMessage.Trusted;
    }

    private readonly Dictionary<string, Incident> _open = new(StringComparer.OrdinalIgnoreCase);
    private int _nextId = 1;
    private DateTime _quietUntil = DateTime.MinValue;

    internal IEnumerable<Incident> Open => _open.Values;

    internal Incident? Find(int id) => _open.Values.FirstOrDefault(i => i.Id == id);

    private List<Action> Raise(string tool, IEnumerable<int> pids, DateTime now, bool again)
    {
        var incident = new Incident(_nextId++, tool, now);
        _open[tool] = incident;
        incident.Pids.UnionWith(pids);
        var actions = new List<Action> { new RaiseAlarm(incident.Id, tool, again) };
        if (incident.Pids.Count > 0) actions.Add(new Freeze(incident.Pids.ToList()));
        return actions;
    }

    /// <summary>The programs running right now, by name, with their process ids.</summary>
    internal List<Action> Seen(IReadOnlyDictionary<string, List<int>> running, DateTime now)
    {
        var actions = new List<Action>();
        foreach (var (tool, pids) in running)
        {
            if (!_open.TryGetValue(tool, out var incident))
            {
                actions.AddRange(Raise(tool, pids, now, again: false));
                continue;
            }
            var fresh = pids.Where(p => incident.Pids.Add(p)).ToList();
            if (fresh.Count > 0 && incident.Choice is null) actions.Add(new Freeze(fresh));
        }

        foreach (var incident in _open.Values.Where(i => i.Tool != Unknown && !running.ContainsKey(i.Tool)).ToList())
        {
            _open.Remove(incident.Tool);
            actions.Add(incident.Trusted ? new HideBanner(incident.Id) : new EndAlarm(incident.Id));
        }
        return actions;
    }

    /// <summary>A program that the user trusted got a new incoming connection: ask again.</summary>
    internal List<Action> NewConnection(string tool, DateTime now)
    {
        if (!_open.TryGetValue(tool, out var old) || !old.Trusted) return [];
        _open.Remove(tool);
        var actions = new List<Action> { new HideBanner(old.Id) };
        actions.AddRange(Raise(tool, old.Pids, now, again: true));
        return actions;
    }

    /// <summary>Someone else is moving the mouse or typing.</summary>
    internal List<Action> RemoteInput(DateTime now)
    {
        if (now < _quietUntil) return [];
        var trusted = _open.Values.Where(i => i.Trusted).ToList();
        if (trusted.Count > 0)
        {
            var actions = new List<Action>();
            foreach (var t in trusted)
            {
                if (now - t.LastRemote > TrustIdle) actions.AddRange(NewConnection(t.Tool, now));
                else t.LastRemote = now;
            }
            return actions;
        }
        if (_open.Count > 0) return [];
        var incident = new Incident(_nextId++, Unknown, now);
        _open[Unknown] = incident;
        return [new RaiseAlarm(incident.Id, Unknown)];
    }

    internal List<Action> Choose(int id, string choice, DateTime now)
    {
        var incident = Find(id);
        if (incident is null) return [];

        if (choice == PipeMessage.Distrust && incident.Choice is null or PipeMessage.Trusted)
        {
            // Everything goes, also after "my helper": other open alarms get the same answer.
            incident.Choice = choice;
            foreach (var other in _open.Values) other.Choice = PipeMessage.Distrust;
            _open.Clear();
            return [new ShutDown(id)];
        }

        if (choice == PipeMessage.Done && incident.Trusted)
        {
            _open.Remove(incident.Tool);
            return [new HideBanner(id), new CloseTool(id, incident.Pids.ToList())];
        }

        if (choice != PipeMessage.Trusted || incident.Choice is not null) return [];
        incident.Choice = choice;
        incident.LastRemote = now;

        if (incident.Tool == Unknown)
        {
            _open.Remove(Unknown);
            _quietUntil = now + QuietAfterTrustedUnknown;
            return [];
        }
        return [new Unfreeze(incident.Pids.ToList()), new ShowBanner(id, incident.Tool)];
    }
}
