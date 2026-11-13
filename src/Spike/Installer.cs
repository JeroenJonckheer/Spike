using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace Spike;

/// <summary>
/// Installing and removing Spike. Done once by the helper, as administrator.
/// </summary>
internal static class Installer
{
    internal static string InstallFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Spike");

    internal static string InstalledExe => Path.Combine(InstallFolder, "Spike.exe");

    /// <summary>
    /// Who may do what with the service. SYSTEM: everything. Administrators: read, start and
    /// change these rules (needed to uninstall), but not stop or delete it directly. Signed-in
    /// users: read only. Rights: CC query config, LC query status, SW dependents, RP start,
    /// WP stop, DT pause, LO interrogate, CR control, SD delete, RC read, WD write DAC, WO owner.
    /// </summary>
    internal const string LockedService = "D:(A;;CCLCSWRPWPDTLOCRSDRCWDWO;;;SY)(A;;CCLCSWRPLOCRRCWD;;;BA)(A;;CCLCSWLOCRRC;;;AU)";

    /// <summary>The usual rules, so an administrator can stop and delete the service again.</summary>
    internal const string OpenService = "D:(A;;CCLCSWRPWPDTLOCRSDRCWDWO;;;SY)(A;;CCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;AU)";

    internal static bool IsAdministrator()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal static bool IsInstalled() =>
        Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{SpikeService.Name}") is not null;

    internal static List<string> Install(Settings settings, bool removeQuickAssist, bool disableRemoteAssistance)
    {
        var log = new List<string>();
        if (IsInstalled())
        {
            log.Add(Sc($"sdset {SpikeService.Name} {OpenService}"));
            log.Add(Sc($"stop {SpikeService.Name}"));
            WaitForStop();
        }

        Directory.CreateDirectory(InstallFolder);
        var me = Environment.ProcessPath ?? throw new InvalidOperationException("Unknown program path.");
        if (!string.Equals(Path.GetFullPath(me), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase))
            File.Copy(me, InstalledExe, overwrite: true);

        ProtectDataFolder();
        settings.Save();

        string bin = $"\\\"{InstalledExe}\\\" --service";
        log.Add(IsInstalled()
            ? Sc($"config {SpikeService.Name} binPath= \"{bin}\" start= auto")
            : Sc($"create {SpikeService.Name} binPath= \"{bin}\" start= auto DisplayName= \"Spike\""));
        log.Add(Sc($"description {SpikeService.Name} \"Spike: a watchdog that barks when someone takes over this computer remotely.\""));
        log.Add(Sc($"failure {SpikeService.Name} reset= 86400 actions= restart/2000/restart/2000/restart/2000"));
        log.Add(Sc($"sdset {SpikeService.Name} {LockedService}"));

        if (removeQuickAssist)
            log.Add(Run("powershell.exe", "-NoProfile -Command \"Get-AppxPackage -AllUsers -Name MicrosoftCorporationII.QuickAssist | Remove-AppxPackage -AllUsers\""));
        if (disableRemoteAssistance)
        {
            using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Remote Assistance");
            key.SetValue("fAllowToGetHelp", 0, RegistryValueKind.DWord);
            log.Add("Remote Assistance switched off");
        }

        log.Add(Sc($"start {SpikeService.Name}"));
        Guard.Log("installed: " + string.Join(" | ", log));
        return log;
    }

    internal static List<string> Uninstall()
    {
        var log = new List<string>();
        Guard.Log("uninstall requested");
        log.Add(Sc($"sdset {SpikeService.Name} {OpenService}"));
        log.Add(Sc($"stop {SpikeService.Name}"));
        WaitForStop();
        log.Add(Sc($"delete {SpikeService.Name}"));
        foreach (var p in Process.GetProcessesByName("Spike").Where(p => p.Id != Environment.ProcessId))
            try { p.Kill(); } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        try { File.Delete(Settings.FilePath); } catch (IOException) { }
        // The program folder goes after this program has ended. The evidence stays.
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 3 /nobreak >nul & rmdir /s /q \"{InstallFolder}\"")
        { CreateNoWindow = true, UseShellExecute = false });
        return log;
    }

    /// <summary>%ProgramData%\Spike: SYSTEM and administrators may write, users may only read.</summary>
    private static void ProtectDataFolder()
    {
        var dir = new DirectoryInfo(Settings.Folder);
        if (!dir.Exists) dir.Create();
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
        dir.SetAccessControl(acl);
    }

    private static void WaitForStop()
    {
        for (int i = 0; i < 20; i++)
        {
            var spikes = Process.GetProcessesByName("Spike").Where(p => p.Id != Environment.ProcessId && SafeSession(p) == 0);
            if (!spikes.Any()) return;
            Thread.Sleep(500);
        }
    }

    private static int SafeSession(Process p)
    {
        try { return p.SessionId; } catch (InvalidOperationException) { return -1; }
    }

    private static string Sc(string arguments) => Run("sc.exe", arguments);

    private static string Run(string exe, string arguments)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p is null) return $"{exe} {arguments}: not started";
            string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(30000);
            return $"{exe} {arguments.Split(' ')[0]}: {(p.HasExited ? p.ExitCode : -1)} {output.Trim().Replace("\r\n", " ")}";
        }
        catch (System.ComponentModel.Win32Exception e) { return $"{exe}: {e.Message}"; }
    }
}
