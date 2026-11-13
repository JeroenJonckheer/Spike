using System.Drawing.Drawing2D;

namespace Spike;

/// <summary>
/// The red screen Spike shows the moment a remote-access program starts. It covers the
/// whole screen, stays on top and cannot be closed with the cross, Escape or Alt+F4: the
/// only ways out are the two big buttons.
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
    private readonly TableLayoutPanel _column;
    private bool _closingAllowed;

    internal AlarmForm(string program, IReadOnlyList<Helper> helpers, bool test = false)
    {
        if (helpers.Count == 0) throw new ArgumentException("At least one helper is needed.", nameof(helpers));
        _helpers = helpers;
        var t = Text_;
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

        _column = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.Transparent,
        };

        if (test) _column.Controls.Add(Line(t.Test, 16f, FontStyle.Bold, Color.FromArgb(255, 224, 130)));
        _column.Controls.Add(Line(t.AlarmTitle, 40f, FontStyle.Bold));
        _column.Controls.Add(Line(string.Format(t.AlarmProgram, program), 24f));
        _column.Controls.Add(Line(t.NeverAsks, 24f));
        _column.Controls.Add(HelperCard(first, helpers.Skip(1)));

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 36, 0, 0), BackColor = Color.Transparent };
        var distrust = BigButton(t.Distrust, PostboxRed, Color.White);
        distrust.FlatAppearance.BorderColor = Color.White;
        distrust.FlatAppearance.BorderSize = 6;
        distrust.Click += (_, _) => Finish(Distrusted);
        var trusted = BigButton(string.Format(t.Trusted, first.Name), Color.White, Navy);
        trusted.Click += (_, _) => ConfirmHelper();
        buttons.Controls.Add(distrust);
        buttons.Controls.Add(trusted);
        _column.Controls.Add(buttons);

        Controls.Add(_column);
        Resize += (_, _) => Centre();
        Shown += (_, _) => { Centre(); Activate(); };
    }

    private static Text.Language Text_ => Spike.Text.Current;

    private void Centre() =>
        _column.Location = new Point(Math.Max(0, (ClientSize.Width - _column.Width) / 2), Math.Max(0, (ClientSize.Height - _column.Height) / 2));

    private static Label Line(string text, float size, FontStyle style = FontStyle.Regular, Color? colour = null) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(1400, 0),
        Font = new Font("Segoe UI", size, style),
        ForeColor = colour ?? Color.White,
        Margin = new Padding(0, 0, 0, 14),
    };

    private Control HelperCard(Helper first, IEnumerable<Helper> others)
    {
        var t = Text_;
        var card = new RoundedPanel
        {
            BackColor = Color.White,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(40, 28, 40, 28),
            Margin = new Padding(0, 24, 0, 0),
        };
        var lines = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White };
        lines.Controls.Add(Line(string.Format(t.CallFirst, first.Name, first.Phone), 40f, FontStyle.Bold, Navy));
        foreach (var h in others)
            lines.Controls.Add(Line(string.Format(t.AlsoCall, h.Name, h.Phone), 24f, FontStyle.Regular, Navy));
        card.Controls.Add(lines);
        return card;
    }

    private static Button BigButton(string text, Color back, Color fore)
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
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    /// <summary>One extra question, because a scammer on the phone can say "press that button".</summary>
    private void ConfirmHelper()
    {
        var t = Text_;
        var first = _helpers[0];
        _column.Controls.Clear();
        _column.Controls.Add(Line(string.Format(t.ReallyHelper, first.Name, first.Phone), 32f, FontStyle.Bold));
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 36, 0, 0), BackColor = Color.Transparent };
        var yes = BigButton(string.Format(t.YesItIs, first.Name), Color.White, Navy);
        yes.Click += (_, _) => Finish(Trusted);
        var no = BigButton(t.NoCutIt, PostboxRed, Color.White);
        no.FlatAppearance.BorderColor = Color.White;
        no.FlatAppearance.BorderSize = 6;
        no.Click += (_, _) => Finish(Distrusted);
        buttons.Controls.Add(no);
        buttons.Controls.Add(yes);
        _column.Controls.Add(buttons);
        Centre();
    }

    private void Finish(EventHandler? handler)
    {
        handler?.Invoke(this, EventArgs.Empty);
        _closingAllowed = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Alt+F4 and the task manager's "close window" end up here: refuse them.
        if (!_closingAllowed && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) =>
        keyData is Keys.Escape or (Keys.Alt | Keys.F4) || base.ProcessCmdKey(ref msg, keyData);

    /// <summary>A white card with rounded corners.</summary>
    private sealed class RoundedPanel : Panel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor is { A: > 0 } c ? c : Red);
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
