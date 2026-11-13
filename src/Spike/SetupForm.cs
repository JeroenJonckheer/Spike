namespace Spike;

/// <summary>
/// For the helper, as administrator: fill in who Spike shows on the alarm, and install
/// (first time) or save (later, from the tray menu).
/// </summary>
internal sealed class SetupForm : Form
{
    private readonly bool _install;
    private readonly List<(TextBox Name, TextBox Phone)> _rows = [];
    private readonly TextBox _owner = new() { Width = 260 };
    private readonly ComboBox _language = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _quickAssist = new() { AutoSize = true, Checked = true };
    private readonly CheckBox _remoteAssistance = new() { AutoSize = true, Checked = true };
    private readonly Label _message = new() { AutoSize = true, ForeColor = AlarmForm.Red, MaximumSize = new Size(560, 0) };

    internal SetupForm(bool install)
    {
        _install = install;
        var t = Spike.Text.Current;
        var settings = Settings.Load();

        Text = t.SetupTitle;
        Icon = Program.AppIcon;
        Font = new Font("Segoe UI", 11f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(20);

        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2 };
        grid.Controls.Add(new Label { Text = t.SetupIntro, AutoSize = true, MaximumSize = new Size(560, 0), Margin = new Padding(0, 0, 0, 12) }, 0, 0);
        grid.SetColumnSpan(grid.GetControlFromPosition(0, 0)!, 2);
        var ownerLabel = new Label { Text = t.OwnerLabel, AutoSize = true, MaximumSize = new Size(560, 0), Font = new Font(Font, FontStyle.Bold) };
        grid.Controls.Add(ownerLabel, 0, 1);
        grid.SetColumnSpan(ownerLabel, 2);
        _owner.Text = settings.Owner ?? "";
        grid.Controls.Add(_owner, 0, 2);

        grid.Controls.Add(new Label { Text = t.LanguageLabel, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 12, 3, 0) }, 0, 3);
        _language.Items.Add(t.LanguageWindows);
        foreach (var l in Spike.Text.All) _language.Items.Add(l.LanguageName);
        int chosen = Spike.Text.All.ToList().FindIndex(l => l.Code == settings.Language);
        _language.SelectedIndex = chosen + 1;
        grid.Controls.Add(_language, 0, 4);

        var headerName = new Label { Text = t.HelperName, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 16, 3, 0) };
        var headerPhone = new Label { Text = t.HelperPhone, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 16, 3, 0) };
        grid.Controls.Add(headerName, 0, 5);
        grid.Controls.Add(headerPhone, 1, 5);
        for (int i = 0; i < Settings.MaxHelpers; i++)
        {
            var name = new TextBox { Width = 260, Text = i < settings.Helpers.Count ? settings.Helpers[i].Name : "" };
            var phone = new TextBox { Width = 260, Text = i < settings.Helpers.Count ? settings.Helpers[i].Phone : "" };
            grid.Controls.Add(name, 0, i + 6);
            grid.Controls.Add(phone, 1, i + 6);
            _rows.Add((name, phone));
        }
        int row = Settings.MaxHelpers + 6;
        if (install)
        {
            _quickAssist.Text = t.RemoveQuickAssist;
            _remoteAssistance.Text = t.DisableRemoteAssistance;
            _quickAssist.Margin = new Padding(0, 16, 0, 0);
            grid.Controls.Add(_quickAssist, 0, row);
            grid.SetColumnSpan(_quickAssist, 2);
            grid.Controls.Add(_remoteAssistance, 0, row + 1);
            grid.SetColumnSpan(_remoteAssistance, 2);
            row += 2;
        }
        grid.Controls.Add(_message, 0, row);
        grid.SetColumnSpan(_message, 2);

        var go = new Button { Text = install ? t.Install : t.Save, AutoSize = true, Padding = new Padding(16, 6, 16, 6), Margin = new Padding(0, 16, 0, 0) };
        go.Click += (_, _) => Go();
        grid.Controls.Add(go, 0, row + 1);
        AcceptButton = go;
        Controls.Add(grid);
    }

    private void Go()
    {
        var t = Spike.Text.Current;
        var settings = Settings.Load();
        settings.Helpers = _rows.Select(r => new Helper(r.Name.Text, r.Phone.Text)).ToList();
        settings.Owner = _owner.Text;
        settings.Language = _language.SelectedIndex <= 0 ? null : Spike.Text.All[_language.SelectedIndex - 1].Code;
        settings.Normalize();
        if (settings.Helpers.Count == 0)
        {
            _message.Text = t.NeedHelper;
            return;
        }
        UseWaitCursor = true;
        try
        {
            if (_install) Installer.Install(settings, _quickAssist.Checked, _remoteAssistance.Checked);
            else settings.Save();
        }
        finally { UseWaitCursor = false; }

        if (_install)
        {
            MessageBox.Show(this, t.Installed, "Spike", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        Close();
    }
}

/// <summary>
/// Removing Spike: a warning that a scammer may be behind it, the helper's number and
/// 15 seconds of waiting before the button works.
/// </summary>
internal sealed class UninstallForm : Form
{
    internal const int WaitSeconds = 15;

    internal bool Confirmed { get; private set; }

    internal UninstallForm()
    {
        var t = Spike.Text.Current;
        var helper = Settings.Load().HelpersOrDefault[0];
        Text = "Spike";
        Icon = Program.AppIcon;
        Font = new Font("Segoe UI", 13f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(24);

        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
        panel.Controls.Add(new Label
        {
            Text = string.Format(t.UninstallWarning, helper.Name, helper.Phone),
            AutoSize = true,
            MaximumSize = new Size(620, 0),
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = AlarmForm.Red,
        });
        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 20, 0, 0) };
        var cancel = new Button { Text = t.Cancel, AutoSize = true, Padding = new Padding(16, 6, 16, 6) };
        cancel.Click += (_, _) => Close();
        var yes = new Button { AutoSize = true, Padding = new Padding(16, 6, 16, 6), Enabled = false };
        yes.Click += (_, _) => { Confirmed = true; Close(); };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(yes);
        panel.Controls.Add(buttons);
        Controls.Add(panel);
        CancelButton = cancel;

        int left = WaitSeconds;
        yes.Text = string.Format(t.UninstallWait, left);
        var timer = new System.Windows.Forms.Timer { Interval = 1000, Enabled = true };
        timer.Tick += (_, _) =>
        {
            left--;
            if (left > 0) { yes.Text = string.Format(t.UninstallWait, left); return; }
            timer.Stop();
            yes.Text = t.UninstallYes;
            yes.Enabled = true;
        };
        Shown += (_, _) => Bark.Play();
    }
}
