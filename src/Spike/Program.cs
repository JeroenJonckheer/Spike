namespace Spike;

internal static class Program
{
    /// <summary>
    /// For now only the alarm screen exists:
    ///   Spike.exe --alarm                show it as a test
    ///   Spike.exe --render-alarm x.png   save a picture of it (for the design)
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Helper[] helpers = [new("Sanne", "06-12 34 56 78"), new("Mark", "06-87 65 43 21")];

        if (args.Length == 2 && args[0] == "--render-alarm")
        {
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
            bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
            return;
        }

        Application.Run(new AlarmForm("AnyDesk", helpers, test: true));
    }
}
