using System.Drawing.Drawing2D;

namespace Spike;

/// <summary>
/// The red screen Spike shows the moment a remote-access program starts. It covers the
/// whole screen, stays on top and cannot be closed with the cross, Escape or Alt+F4: the
/// only ways out are the two big buttons. The buttons only count a click by a real hand
/// on the real mouse, so a scammer cannot click them from far away.
/// </summary>
internal sealed class AlarmForm : Form
{
    internal static readonly Color Red = Color.FromArgb(198, 40, 40);
    internal static readonly Color PostboxRed = Color.FromArgb(229, 57, 53);
    internal static readonly Color Navy = Color.FromArgb(28, 52, 78);

    /// <summary>The user does not trust it: close the program and go offline.</summary>
    internal event EventHandler? Distrusted;

    /// <summary>The user confirmed that a trusted helper is on the phone.</summary>
    internal event EventHandler? Trusted;

    private readonly IReadOnlyList<Helper> _helpers;
    private readonly Func<bool> _realClick;
    private readonly string? _owner;
    private readonly TableLayoutPanel _column;
    private bool _closingAllowed;

    /// <param name="program">Name of the program; empty when unknown (someone else moves the mouse).</param>
    /// <param name="realClick">True when the last click came from a real hand.</param>
    /// <param name="owner">Name of the person who uses this computer, so the alarm speaks to them.</param>
    /// <param name="again">A new connection of a program the user trusted before.</param>
    internal AlarmForm(string program, IReadOnlyList<Helper> helpers, bool test = false, Func<bool>? realClick = null,
        string? owner = null, bool again = false)
    {
        if (helpers.Count == 0) throw new ArgumentException("At least one helper is needed.", nameof(helpers));
        _helpers = helpers;
        _owner = owner;
        _realClick = realClick ?? (() => true);
        var t = Spike.Text.Current;
        var first = helpers[0];

        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        BackColor = Red;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 20f);
        Text = "Spike";
        Icon = Program.AppIcon;

        _column = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.Transparent,
        };

        if (test) _column.Controls.Add(Line(t.Test, 16f, FontStyle.Bold, Color.FromArgb(255, 224, 130)));
        _column.Controls.Add(Line(owner is null ? t.AlarmTitle : string.Format(t.AlarmTitleOwner, owner), 40f, FontStyle.Bold));
        var name = string.IsNullOrEmpty(program) ? t.UnknownProgram : again ? string.Format(t.NewConnection, program) : program;
        _column.Controls.Add(Line(string.Format(t.AlarmProgram, name), 24f));
        _column.Controls.Add(Line(t.NeverAsks, 24f));
        _column.Controls.Add(HelperCard(first, helpers.Skip(1)));

        var buttons = Row();
        var distrust = BigButton(t.Distrust, PostboxRed, Color.White, white: true);
        distrust.Click += (_, _) => { if (_realClick()) Finish(Distrusted); };
        var trusted = BigButton(string.Format(t.Trusted, first.Name), Color.White, Navy);
        trusted.Click += (_, _) => { if (_realClick()) ConfirmHelper(); };
        buttons.Controls.Add(distrust);
        buttons.Controls.Add(trusted);
        _column.Controls.Add(buttons);

        Controls.Add(_column);
        Resize += (_, _) => Centre();
        Shown += (_, _) => { Centre(); Activate(); };
    }

    private void Centre() =>
        _column.Location = new Point(Math.Max(0, (ClientSize.Width - _column.Width) / 2), Math.Max(0, (ClientSize.Height - _column.Height) / 2));

    private static FlowLayoutPanel Row() =>
        new() { AutoSize = true, WrapContents = false, Margin = new Padding(0, 36, 0, 0), BackColor = Color.Transparent };

    private static Label Line(string text, float size, FontStyle style = FontStyle.Regular, Color? colour = null) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(1400, 0),
        Font = new Font("Segoe UI", size, style),
        ForeColor = colour ?? Color.White,
        Margin = new Padding(0, 0, 0, 14),
    };

    private static Control HelperCard(Helper first, IEnumerable<Helper> others)
    {
        var t = Spike.Text.Current;
        var card = new RoundedPanel
        {
            BackColor = Color.White,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 0, 40, 24),
            Margin = new Padding(0, 24, 0, 0),
        };
        var lines = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.White,
            Location = new Point(40, 24),
        };
        lines.Controls.Add(Line(string.Format(t.CallFirst, first.Name, first.Phone), 40f, FontStyle.Bold, Navy));
        foreach (var h in others)
            lines.Controls.Add(Line(string.Format(t.AlsoCall, h.Name, h.Phone), 24f, FontStyle.Regular, Navy));
        card.Controls.Add(lines);
        return card;
    }

    private static Button BigButton(string text, Color back, Color fore, bool white = false)
    {
        var b = new Button
        {
            Text = text,
            Size = new Size(600, 170),
            Font = new Font("Segoe UI", 24f, FontStyle.Bold),
            BackColor = back,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 40, 0),
            Cursor = Cursors.Hand,
            TabStop = false,
        };
        b.FlatAppearance.BorderSize = white ? 6 : 0;
        b.FlatAppearance.BorderColor = Color.White;
        return b;
    }

    /// <summary>One extra question, because a scammer on the phone can say "press that button".</summary>
    private void ConfirmHelper()
    {
        var t = Spike.Text.Current;
        var first = _helpers[0];
        _column.Controls.Clear();
        _column.Controls.Add(Line(_owner is null
            ? string.Format(t.ReallyHelper, first.Name, first.Phone)
            : string.Format(t.ReallyHelperOwner, _owner, first.Name, first.Phone), 32f, FontStyle.Bold));
        var buttons = Row();
        var no = BigButton(t.NoCutIt, PostboxRed, Color.White, white: true);
        no.Click += (_, _) => { if (_realClick()) Finish(Distrusted); };
        var yes = BigButton(string.Format(t.YesItIs, first.Name), Color.White, Navy);
        yes.Click += (_, _) => { if (_realClick()) Finish(Trusted); };
        buttons.Controls.Add(no);
        buttons.Controls.Add(yes);
        _column.Controls.Add(buttons);
        Centre();
    }

    /// <summary>After the red button: the computer is off the internet. Now one button closes the screen.</summary>
    internal void ShowOffline()
    {
        var t = Spike.Text.Current;
        var first = _helpers[0];
        _column.Controls.Clear();
        _column.Controls.Add(Line(t.OffTitle, 40f, FontStyle.Bold));
        _column.Controls.Add(Line(string.Format(t.OffBody, first.Name, first.Phone), 26f));
        var row = Row();
        var ok = BigButton(t.Understood, Color.White, Navy);
        ok.Click += (_, _) => { if (_realClick()) Finish(null); };
        row.Controls.Add(ok);
        _column.Controls.Add(row);
        Centre();
        Show();
        Activate();
    }

    private void Finish(EventHandler? handler)
    {
        handler?.Invoke(this, EventArgs.Empty);
        if (handler == Distrusted) return; // stays open until the Guard reports "offline"
        _closingAllowed = true;
        Close();
    }

    /// <summary>Closes the screen from code (the program went away by itself).</summary>
    internal void Dismiss()
    {
        _closingAllowed = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Alt+F4 and "close window" in the task manager end up here: refuse them.
        if (!_closingAllowed && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) =>
        keyData is Keys.Escape or (Keys.Alt | Keys.F4) or Keys.Enter or Keys.Space || base.ProcessCmdKey(ref msg, keyData);

    /// <summary>A white card with rounded corners.</summary>
    private sealed class RoundedPanel : Panel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Red);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = new GraphicsPath();
            int r = 28;
            var b = new Rectangle(0, 0, Width - 1, Height - 1);
            path.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90);
            path.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90);
            path.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillPath(brush, path);
        }
    }
}
