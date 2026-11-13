using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;
using Microsoft.Win32;

namespace Spike;

/// <summary>
/// The red button: "I don't trust this". Closes every remote-access program, switches its
/// services off so they don't come back by themselves, and takes the computer off the
/// internet until the next start of Windows. Disabling the network adapters is the only
/// way that surely cuts a connection that is already open.
/// </summary>
internal sealed class Panic(Catalog catalog, Func<string, string, int>? run = null, string? stateFile = null)
{
    private readonly Func<string, string, int> _run = run ?? Run;
    private readonly string _stateFile = stateFile ?? Path.Combine(Settings.Folder, "offline.json");

    internal List<string> Log { get; } = [];

    internal void Execute(IEnumerable<int> pids, Func<string, ToolMatch?> recognise)
    {
        foreach (var pid in pids) Kill(pid);
        StopServices(recognise);
        GoOffline(AdaptersThatAreUp());
    }

    private void Kill(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill();
            Log.Add($"stopped process {pid} {p.ProcessName}");
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Log.Add($"could not stop process {pid}: {e.Message}");
        }
    }

    /// <summary>Every installed service that belongs to a remote-access program: stop it and disable it.</summary>
    private void StopServices(Func<string, ToolMatch?> recognise)
    {
        using var services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
        if (services is null) return;
        foreach (var name in services.GetSubKeyNames())
        {
            using var key = services.OpenSubKey(name);
            var image = key?.GetValue("ImagePath") as string;
            var exe = ExePath(image);
            bool remote = catalog.FindService(name) is not null || exe is not null && recognise(exe) is not null;
            if (!remote) continue;
            _run("sc.exe", $"stop \"{name}\"");
            int rc = _run("sc.exe", $"config \"{name}\" start= disabled");
            Log.Add($"service {name} stopped and disabled ({rc})");
        }
    }

    /// <summary>The program file in a service's ImagePath ("C:\x\y.exe" --service → C:\x\y.exe).</summary>
    internal static string? ExePath(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return null;
        var s = Environment.ExpandEnvironmentVariables(imagePath.Trim());
        if (s.StartsWith('"'))
        {
            int end = s.IndexOf('"', 1);
            return end > 1 ? s[1..end] : null;
        }
        int exe = s.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0 ? s[..(exe + 4)] : null;
    }

    internal static List<string> AdaptersThatAreUp() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(n => n.Name)
            .ToList();

    internal void GoOffline(IReadOnlyList<string> adapters)
    {
        var done = new List<string>();
        foreach (var a in adapters)
        {
            int rc = _run("netsh.exe", $"interface set interface name=\"{a}\" admin=disabled");
            Log.Add($"adapter {a} disabled ({rc})");
            if (rc == 0) done.Add(a);
        }
        // Remember which adapters Spike switched off, also those already off in an earlier panic.
        var all = Remembered().Union(done, StringComparer.OrdinalIgnoreCase).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(_stateFile)!);
        File.WriteAllText(_stateFile, JsonSerializer.Serialize(all));
    }

    /// <summary>At the start of Windows: switch on again what Spike switched off. Other adapters stay as they are.</summary>
    internal void RestoreAfterRestart()
    {
        var adapters = Remembered();
        foreach (var a in adapters)
        {
            int rc = _run("netsh.exe", $"interface set interface name=\"{a}\" admin=enabled");
            Log.Add($"adapter {a} enabled again ({rc})");
        }
        try { File.Delete(_stateFile); } catch (IOException) { }
    }

    private List<string> Remembered()
    {
        try { return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_stateFile)) ?? []; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return []; }
    }

    private static int Run(string exe, string arguments)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, arguments) { CreateNoWindow = true, UseShellExecute = false });
            if (p is null) return -1;
            p.WaitForExit(15000);
            return p.HasExited ? p.ExitCode : -1;
        }
        catch (System.ComponentModel.Win32Exception) { return -1; }
    }
}
