using System.Reflection;
using System.ServiceProcess;

namespace Spike;

internal static class Program
{
    internal static Icon AppIcon { get; } =
        Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath) ?? SystemIcons.Shield;

    internal static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

    /// <summary>
    /// One Spike.exe, several roles:
    ///   --service              the Windows service (started by Windows)
    ///   --guard                the Guard in this window, for testing (as administrator)
    ///   --dog                  the tray program (started by the service for every user)
    ///   --install / --uninstall / --settings   for the helper, as administrator
    ///   --alarm                show the alarm as a test
    ///   --render-alarm x.png   save a picture of the alarm (for the design)
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        string role = args.Length > 0 ? args[0].ToLowerInvariant() : "";

        if (role == "--service")
        {
            ServiceBase.Run(new SpikeService());
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Text.Current = Text.FromCode(Settings.Load().Language);

        switch (role)
        {
            case "--guard":
                using (var guard = new Guard(startDogs: false))
                {
                    guard.Start();
                    MessageBox.Show("Spike Guard runs. Press OK to stop.", "Spike");
                }
                return 0;

            case "--dog":
                using (var single = new Mutex(true, @"Local\SpikeDog", out bool first))
                {
                    if (!first) return 0;
                    Application.Run(new Dog());
                }
                return 0;

            case "--render-alarm" when args.Length == 2:
                RenderAlarm(args[1]);
                return 0;

            case "--alarm":
                Application.Run(new AlarmForm("AnyDesk", Settings.Load().HelpersOrDefault, test: true));
                return 0;

            case "--install":
            case "--settings":
                if (!Elevated(role)) return 1;
                Application.Run(new SetupForm(install: role == "--install" || !Installer.IsInstalled()));
                return 0;

            case "--uninstall":
                if (!Elevated(role)) return 1;
                using (var ask = new UninstallForm())
                {
                    ask.ShowDialog();
                    if (!ask.Confirmed) return 0;
                }
                Installer.Uninstall();
                MessageBox.Show(Text.Current.Uninstalled, "Spike", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;

            default:
                // Double-clicked: install when Spike is not there yet, otherwise start the Dog.
                if (!Installer.IsInstalled()) goto case "--install";
                goto case "--dog";
        }
    }

    /// <summary>Restarts this role as administrator when needed. False when this instance should stop.</summary>
    private static bool Elevated(string role)
    {
        if (Installer.IsAdministrator()) return true;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath ?? "Spike.exe", role)
            { UseShellExecute = true, Verb = "runas" });
        }
        catch (System.ComponentModel.Win32Exception) { } // the user said no
        return false;
    }

    private static void RenderAlarm(string path)
    {
        Helper[] helpers = [new("Sanne", "06-12 34 56 78"), new("Mark", "06-87 65 43 21")];
        using var form = new AlarmForm("AnyDesk", helpers, test: true)
        {
            WindowState = FormWindowState.Normal,
            Bounds = new Rectangle(0, 0, 1920, 1080),
            Opacity = 0,
        };
        form.Show();
        Application.DoEvents();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        form.Dismiss();
    }
}
