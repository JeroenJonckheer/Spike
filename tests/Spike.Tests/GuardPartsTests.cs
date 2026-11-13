using System.Net;
using Xunit;

namespace Spike.Tests;

public class RemoteInputDetectorTests
{
    [Fact]
    public void Steady_injected_input_while_the_real_mouse_is_still_is_reported()
    {
        var d = new RemoteInputDetector();
        bool fired = false;
        for (int ms = 10_000; ms < 12_000 && !fired; ms += 50) fired = d.Feed(injected: true, ms);

        Assert.True(fired);
    }

    [Fact]
    public void Injected_input_right_after_a_real_hand_is_not_reported()
    {
        // The on-screen keyboard injects keys, but only while a real hand clicks it.
        var d = new RemoteInputDetector();
        bool fired = false;
        for (int ms = 10_000; ms < 14_000; ms += 50)
        {
            if (ms % 1000 == 0) d.Feed(injected: false, ms);
            fired |= d.Feed(injected: true, ms);
        }

        Assert.False(fired);
    }

    [Fact]
    public void A_few_injected_events_are_not_enough()
    {
        var d = new RemoteInputDetector();
        bool fired = false;
        for (int i = 0; i < RemoteInputDetector.MinEvents - 1; i++) fired |= d.Feed(true, 10_000 + i * 500);

        Assert.False(fired);
    }

    [Fact]
    public void It_is_reported_at_most_every_ten_seconds()
    {
        var d = new RemoteInputDetector();
        int count = 0;
        for (int ms = 10_000; ms < 25_000; ms += 50) if (d.Feed(true, ms)) count++;

        Assert.Equal(2, count);
    }
}

public class PanicTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("spike-panic").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    [Theory]
    [InlineData("\"C:\\Program Files (x86)\\AnyDesk\\AnyDesk.exe\" --service", @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe")]
    [InlineData(@"C:\Program Files\TeamViewer\TeamViewer_Service.exe", @"C:\Program Files\TeamViewer\TeamViewer_Service.exe")]
    [InlineData(@"C:\Windows\system32\svchost.exe -k netsvcs -p", @"C:\Windows\system32\svchost.exe")]
    [InlineData(@"System32\drivers\acpi.sys", null)]
    [InlineData(null, null)]
    public void The_program_of_a_service_is_found_in_its_ImagePath(string? image, string? expected)
    {
        Assert.Equal(expected, Panic.ExePath(image));
    }

    [Fact]
    public void After_a_restart_only_the_adapters_Spike_switched_off_come_back()
    {
        var calls = new List<string>();
        var state = Path.Combine(_dir, "offline.json");
        var panic = new Panic(Catalog.Load(), (exe, args) => { calls.Add(args); return 0; }, state);

        panic.GoOffline(["Wi-Fi", "Ethernet"]);
        calls.Clear();
        new Panic(Catalog.Load(), (exe, args) => { calls.Add(args); return 0; }, state).RestoreAfterRestart();

        Assert.Equal(["interface set interface name=\"Wi-Fi\" admin=enabled", "interface set interface name=\"Ethernet\" admin=enabled"], calls);
        Assert.False(File.Exists(state));
    }

    [Fact]
    public void An_adapter_that_could_not_be_switched_off_is_not_switched_on_later()
    {
        var state = Path.Combine(_dir, "offline.json");
        var panic = new Panic(Catalog.Load(), (exe, args) => args.Contains("Bluetooth") ? 1 : 0, state);
        panic.GoOffline(["Wi-Fi", "Bluetooth"]);

        var calls = new List<string>();
        new Panic(Catalog.Load(), (exe, args) => { calls.Add(args); return 0; }, state).RestoreAfterRestart();

        Assert.Single(calls);
        Assert.Contains("Wi-Fi", calls[0]);
    }

    [Fact]
    public void Without_an_earlier_panic_nothing_is_switched_on()
    {
        var calls = new List<string>();
        new Panic(Catalog.Load(), (exe, args) => { calls.Add(args); return 0; }, Path.Combine(_dir, "none.json")).RestoreAfterRestart();

        Assert.Empty(calls);
    }
}

public class EvidenceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("spike-evidence").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void An_incident_gets_its_own_folder_with_a_summary()
    {
        var start = new DateTime(2026, 11, 13, 14, 5, 0);
        using var e = new Evidence("AnyDesk", start, _dir);
        e.Connections([new Native.Connection(42, new IPEndPoint(IPAddress.Parse("192.168.1.5"), 50000), new IPEndPoint(IPAddress.Parse("34.1.2.3"), 443), 5)],
            new HashSet<int> { 42 }, _ => "AnyDesk.exe");
        e.Choice(PipeMessage.Distrust, "Sanne");
        e.Finish(start.AddMinutes(3));

        Assert.EndsWith("2026-11-13 14.05.00 AnyDesk", e.Folder);
        var summary = File.ReadAllText(Path.Combine(e.Folder, "samenvatting.txt"));
        Assert.Contains("34.1.2.3", summary);
        Assert.Contains("AnyDesk", summary);
        Assert.Contains("34.1.2.3:443", File.ReadAllText(Path.Combine(e.Folder, "verbindingen.csv")));
    }

    [Fact]
    public void Each_connection_is_written_once_and_local_ones_are_skipped()
    {
        using var e = new Evidence("TeamViewer", DateTime.Now, _dir);
        var c = new Native.Connection(7, new IPEndPoint(IPAddress.Loopback, 1), new IPEndPoint(IPAddress.Parse("5.6.7.8"), 5938), 5);
        var local = new Native.Connection(7, new IPEndPoint(IPAddress.Loopback, 1), new IPEndPoint(IPAddress.Loopback, 2), 5);
        e.Connections([c, local], new HashSet<int> { 7 }, _ => "tv");
        e.Connections([c], new HashSet<int> { 7 }, _ => "tv");

        var lines = File.ReadAllLines(Path.Combine(e.Folder, "verbindingen.csv"));
        Assert.Equal(2, lines.Length); // header + one connection
    }

    [Fact]
    public void Log_paths_are_found_in_every_user_profile()
    {
        var profile = Path.Combine(_dir, "Ria");
        Directory.CreateDirectory(Path.Combine(profile, @"AppData\Roaming\AnyDesk"));
        File.WriteAllText(Path.Combine(profile, @"AppData\Roaming\AnyDesk\connection_trace.txt"), "Incoming 2026-11-13, 14:05 User 123 123");

        var found = Evidence.Expand(@"%APPDATA%\AnyDesk\connection_trace.txt", [profile]).ToList();

        Assert.Single(found);
    }

    [Fact]
    public void Pruning_removes_old_details_but_keeps_the_summary()
    {
        var old = Directory.CreateDirectory(Path.Combine(_dir, "old"));
        File.WriteAllText(Path.Combine(old.FullName, "samenvatting.txt"), "x");
        File.WriteAllText(Path.Combine(old.FullName, "logboek.txt"), "x");
        old.CreationTimeUtc = DateTime.UtcNow.AddDays(-100);

        Evidence.Prune(_dir, TimeSpan.FromDays(90), long.MaxValue, DateTime.Now);

        Assert.True(File.Exists(Path.Combine(old.FullName, "samenvatting.txt")));
        Assert.False(File.Exists(Path.Combine(old.FullName, "logboek.txt")));
    }
}

public class MessageAndSettingsTests
{
    [Fact]
    public void A_pipe_message_survives_the_trip()
    {
        var m = new PipeMessage { Type = PipeMessage.Choice, Id = 3, Pick = PipeMessage.Trusted };

        var back = PipeMessage.FromLine(m.ToLine());

        Assert.Equal(m, back);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData(null)]
    public void Garbage_on_the_pipe_is_ignored(string? line)
    {
        Assert.Null(PipeMessage.FromLine(line));
    }

    [Fact]
    public void Settings_keep_at_most_three_complete_helpers()
    {
        var s = new Settings { Helpers = [new(" Sanne ", "06 1"), new("", "06 2"), new("Mark", " "), new("A", "1"), new("B", "2"), new("C", "3")] };

        s.Normalize();

        Assert.Equal([new Helper("Sanne", "06 1"), new Helper("A", "1"), new Helper("B", "2")], s.Helpers);
    }

    [Fact]
    public void Settings_are_saved_and_read_back()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spike-{Guid.NewGuid():N}.json");
        try
        {
            new Settings { Helpers = [new("Sanne", "06-12 34 56 78")], Language = "nl" }.Save(path);

            var s = Settings.Load(path);

            Assert.Equal("Sanne", s.Helpers.Single().Name);
            Assert.Equal("nl", s.Language);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Without_helpers_the_alarm_shows_the_fraud_helpdesk()
    {
        Assert.Equal("088 786 73 72", new Settings().HelpersOrDefault[0].Phone);
    }

    [Fact]
    public void The_bark_is_a_valid_wave_file()
    {
        var wav = Bark.Make();

        Assert.Equal("RIFF"u8.ToArray(), wav[..4]);
        Assert.True(wav.Length > 10_000);
    }
}
