using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Spike;

/// <summary>
/// The Dog: the small program in the notification area of each signed-in user. It shows
/// what the Guard decides (alarm, banner, offline), barks, takes screenshots while an
/// incident lasts and tells the Guard when someone else seems to move the mouse.
/// The Dog has no "quit": the Guard starts it again when it is closed.
/// </summary>
internal sealed class Dog : ApplicationContext
{
    private readonly Control _ui = new();
    private readonly NotifyIcon _tray;
    private readonly PipeClient _pipe = new();
    private readonly InputWatch _input;
    private readonly Dictionary<int, AlarmForm> _alarms = [];
    private readonly System.Windows.Forms.Timer _shots = new() { Interval = 3000 };
    private Settings _settings = Settings.Load();
    private BannerForm? _banner;
    private readonly Dictionary<string, DateTime> _warned = new(StringComparer.OrdinalIgnoreCase);

    internal Dog()
    {
        _ui.CreateControl();
        Text.Current = Text.FromCode(_settings.Language);
        var t = Text.Current;

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(t.MenuWatching) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(t.MenuTest, null, (_, _) => Test());
        menu.Items.Add(t.MenuEvidence, null, (_, _) => OpenEvidence());
        menu.Items.Add(t.MenuSettings, null, (_, _) => OpenSettings());
        menu.Items.Add(t.MenuAbout, null, (_, _) => About());
        _tray = new NotifyIcon { Icon = Program.AppIcon, Text = t.Tooltip, ContextMenuStrip = menu, Visible = true };

        _input = new InputWatch();
        _input.RemoteInput += () => _pipe.Send(new PipeMessage { Type = PipeMessage.RemoteInput });

        _shots.Tick += (_, _) => Screenshot();
        _pipe.Received += m => _ui.BeginInvoke(() => OnMessage(m));
    }

    private void OnMessage(PipeMessage m)
    {
        _settings = Settings.Load(); // the helper may have changed a name or number
        switch (m.Type)
        {
            case PipeMessage.Alarm:
                ShowAlarm(m.Id, m.Tool ?? "", m.Test);
                break;
            case PipeMessage.AlarmEnd:
                if (_alarms.Remove(m.Id, out var gone)) gone.Dismiss();
                break;
            case PipeMessage.Offline:
                var form = _alarms.Values.FirstOrDefault();
                foreach (var other in _alarms.Values.Skip(1)) other.Dismiss();
                _alarms.Clear();
                if (form is null) { form = NewAlarm("", false); }
                form.ShowOffline();
                break;
            case PipeMessage.Banner:
                _banner?.Close();
                _banner = new BannerForm(string.Format(Text.Current.HelperWatching, _settings.HelpersOrDefault[0].Name,
                    string.IsNullOrEmpty(m.Tool) ? "?" : m.Tool));
                _banner.Show();
                break;
            case PipeMessage.BannerEnd:
                _banner?.Close();
                _banner = null;
                break;
            case PipeMessage.StopAttempt:
                Warn(string.Format(Text.Current.StopAttempt, _settings.HelpersOrDefault[0].Name, _settings.HelpersOrDefault[0].Phone));
                break;
            case PipeMessage.Download:
                // Not on top of an alarm, and once per program per ten minutes.
                if (_alarms.Count > 0 || m.Tool is null) break;
                if (_warned.TryGetValue(m.Tool, out var when) && DateTime.Now - when < TimeSpan.FromMinutes(10)) break;
                _warned[m.Tool] = DateTime.Now;
                var h = _settings.HelpersOrDefault[0];
                Warn(string.Format(Text.Current.DownloadTitle, m.Tool) + "\n\n" + string.Format(Text.Current.DownloadBody, h.Name, h.Phone));
                break;
            case PipeMessage.Record:
                _shots.Enabled = m.On;
                if (m.On) Screenshot();
                break;
            case PipeMessage.Reload:
                _settings = Settings.Load();
                break;
        }
    }

    private AlarmForm NewAlarm(string tool, bool test) =>
        new(tool, _settings.HelpersOrDefault, test, () => _input.RecentClickWasPhysical || !_input.Detecting);

    private void ShowAlarm(int id, string tool, bool test)
    {
        if (_alarms.ContainsKey(id)) return;
        var form = NewAlarm(tool, test);
        _alarms[id] = form;
        form.Distrusted += (_, _) =>
        {
            if (test) { _alarms.Remove(id); form.ShowOffline(); return; }
            _pipe.Send(new PipeMessage { Type = PipeMessage.Choice, Id = id, Pick = PipeMessage.Distrust });
        };
        form.Trusted += (_, _) =>
        {
            _alarms.Remove(id);
            if (!test) _pipe.Send(new PipeMessage { Type = PipeMessage.Choice, Id = id, Pick = PipeMessage.Trusted });
        };
        form.FormClosed += (_, _) => _alarms.Remove(id);
        form.Show();
        form.Activate();
        Bark.Play();
    }

    /// <summary>The test from the tray menu. Through the Guard when it runs, so the whole chain is tested.</summary>
    private void Test()
    {
        if (_pipe.IsConnected) _pipe.Send(new PipeMessage { Type = PipeMessage.TestAlarm });
        else ShowAlarm(0, "AnyDesk", test: true);
    }

    private void Screenshot()
    {
        try
        {
            var area = SystemInformation.VirtualScreen;
            using var full = new Bitmap(area.Width, area.Height);
            using (var g = Graphics.FromImage(full)) g.CopyFromScreen(area.Location, Point.Empty, area.Size);
            int width = Math.Min(1280, area.Width);
            using var small = new Bitmap(full, width, Math.Max(1, area.Height * width / area.Width));
            using var ms = new MemoryStream();
            var jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using var quality = new EncoderParameters(1) { Param = { [0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 60L) } };
            small.Save(ms, jpeg, quality);
            _pipe.Send(new PipeMessage { Type = PipeMessage.Screenshot, Data = Convert.ToBase64String(ms.ToArray()) });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or ExternalException or ArgumentException) { }
    }

    private static void OpenEvidence()
    {
        Directory.CreateDirectory(Evidence.Root);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Evidence.Root}\"") { UseShellExecute = true });
    }

    private static void OpenSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath ?? "Spike.exe", "--settings") { UseShellExecute = true, Verb = "runas" });
        }
        catch (System.ComponentModel.Win32Exception) { } // the user said no to the administrator question
    }

    private static void About() =>
        MessageBox.Show(string.Format(Text.Current.About, Program.Version), "Spike", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private static void Warn(string text)
    {
        Bark.Play();
        using var owner = new Form { TopMost = true, ShowInTaskbar = false, StartPosition = FormStartPosition.CenterScreen, Size = new Size(1, 1) };
        owner.Show();
        MessageBox.Show(owner, text, "Spike", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _input.Dispose();
            _pipe.Dispose();
            _shots.Dispose();
            _ui.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>A small orange bar at the top of the screen while a trusted helper is watching.</summary>
    private sealed class BannerForm : Form
    {
        internal BannerForm(string text)
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(255, 160, 0);
            var label = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = AlarmForm.Navy,
                Padding = new Padding(20, 8, 20, 8),
            };
            Controls.Add(label);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Shown += (_, _) =>
            {
                var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
                Location = new Point(screen.Left + (screen.Width - Width) / 2, screen.Top);
            };
        }

        protected override bool ShowWithoutActivation => true;
    }
}
