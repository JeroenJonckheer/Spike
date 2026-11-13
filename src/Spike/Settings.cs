using System.Text.Json;

namespace Spike;

/// <summary>
/// Spike's settings, in %ProgramData%\Spike\settings.json. Only administrators and SYSTEM may
/// write there (see <see cref="Installer"/>), so a scammer working as the user cannot swap the
/// helper's number for his own.
/// </summary>
internal sealed class Settings
{
    internal const int MaxHelpers = 3;

    public List<Helper> Helpers { get; set; } = [];

    /// <summary>The person who uses this computer, so the alarm speaks to him or her by name.</summary>
    public string? Owner { get; set; }

    /// <summary>"nl", "en" … or null for the language of Windows.</summary>
    public string? Language { get; set; }

    internal static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Spike");

    internal static string FilePath => Path.Combine(Folder, "settings.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    internal static Settings Load(string? path = null)
    {
        try
        {
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path ?? FilePath), Json) ?? new Settings();
            s.Normalize();
            return s;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Settings();
        }
    }

    internal void Save(string? path = null)
    {
        Normalize();
        path ??= FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
    }

    /// <summary>Drops empty helpers and keeps at most <see cref="MaxHelpers"/>.</summary>
    internal void Normalize()
    {
        Owner = string.IsNullOrWhiteSpace(Owner) ? null : Owner.Trim();
        Helpers = Helpers
            .Where(h => h is not null && !string.IsNullOrWhiteSpace(h.Name) && !string.IsNullOrWhiteSpace(h.Phone))
            .Select(h => new Helper(h.Name.Trim(), h.Phone.Trim()))
            .Take(MaxHelpers)
            .ToList();
    }

    /// <summary>The helpers to show; the Dutch Fraud Helpdesk when none were set (should not happen after setup).</summary>
    internal IReadOnlyList<Helper> HelpersOrDefault =>
        Helpers.Count > 0 ? Helpers : [new Helper("de Fraudehelpdesk", "088 786 73 72")];
}
