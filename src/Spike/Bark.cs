using System.Media;

namespace Spike;

/// <summary>
/// Spike's bark: two short "woofs", made in code (a falling tone with overtones and a little
/// noise), so no sound file is needed. Played once when the alarm appears.
/// </summary>
internal static class Bark
{
    private const int Rate = 22050;

    private static readonly Lazy<byte[]> Wave = new(Make);

    internal static void Play()
    {
        try
        {
            using var player = new SoundPlayer(new MemoryStream(Wave.Value));
            player.Play();
        }
        catch (Exception e) when (e is InvalidOperationException or IOException) { }
    }

    /// <summary>The bark as a complete WAV file (16 bit, mono).</summary>
    internal static byte[] Make()
    {
        var samples = new List<short>();
        var random = new Random(7);
        foreach (var (length, from, to) in new[] { (0.20, 520.0, 250.0), (0.10, 0.0, 0.0), (0.24, 470.0, 220.0) })
        {
            int n = (int)(length * Rate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                if (from == 0) { samples.Add(0); continue; }
                double t = (double)i / n;
                double f = from + (to - from) * Math.Pow(t, 0.6);
                phase += 2 * Math.PI * f / Rate;
                double tone = Math.Sin(phase) + 0.6 * Math.Sin(2 * phase) + 0.35 * Math.Sin(3 * phase) + 0.2 * Math.Sin(5 * phase);
                double noise = (random.NextDouble() * 2 - 1) * 0.35;
                double envelope = Math.Min(1, t / 0.04) * Math.Pow(1 - t, 1.6);
                samples.Add((short)Math.Clamp((tone * 0.45 + noise) * envelope * 22000, short.MinValue, short.MaxValue));
            }
        }

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int dataBytes = samples.Count * 2;
        w.Write("RIFF"u8.ToArray()); w.Write(36 + dataBytes); w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(dataBytes);
        foreach (var s in samples) w.Write(s);
        w.Flush();
        return ms.ToArray();
    }
}
