using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Spike;

/// <summary>A remote-access program from catalog.json (built by tools/make_catalog.py).</summary>
internal sealed record Tool(
    string Name,
    string[] OriginalFileNames,
    string[] FileNames,
    string[] Products,
    string[] Signers,
    string[] Services,
    string[] Paths,
    string[] Logs,
    string[] Domains,
    int[] Ports);

/// <summary>Why a file was recognised, strongest first.</summary>
internal enum MatchReason { Signature, Product, OriginalFileName, Path, FileName }

internal sealed record ToolMatch(Tool Tool, MatchReason Reason);

/// <summary>
/// The list of remote-access programs and the rules to recognise them. A valid signature of
/// the maker is the strongest proof; the version information and the install folder also
/// survive renaming; the file name alone is the weakest.
/// </summary>
internal sealed class Catalog
{
    private sealed record Document(string Source, Tool[] Tools);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly (Tool Tool, Regex[] Paths, Regex[] Services)[] _tools;

    internal IReadOnlyList<Tool> Tools { get; }

    private Catalog(Tool[] tools)
    {
        Tools = tools;
        _tools = tools.Select(t => (t, t.Paths.Select(PathPattern).ToArray(), t.Services.Select(ServicePattern).ToArray())).ToArray();
    }

    internal static Catalog FromJson(string json) =>
        new(JsonSerializer.Deserialize<Document>(json, Json)?.Tools ?? []);

    /// <summary>The catalog built into Spike.exe.</summary>
    internal static Catalog Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Spike.catalog.json")
            ?? throw new InvalidOperationException("catalog.json is missing from Spike.exe.");
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }

    /// <summary>The remote-access program this file belongs to, or null.</summary>
    internal ToolMatch? Find(FileFacts file)
    {
        ToolMatch? best = null;
        foreach (var (tool, paths, _) in _tools)
        {
            MatchReason? reason =
                file.SignatureValid && file.SignerNames.Any(s => Same(tool.Signers, s)) ? MatchReason.Signature
                : Same(tool.Products, file.Product) ? MatchReason.Product
                : Same(tool.OriginalFileNames, file.OriginalFileName) ? MatchReason.OriginalFileName
                : paths.Any(p => p.IsMatch(file.Path)) ? MatchReason.Path
                : Same(tool.FileNames, file.FileName) ? MatchReason.FileName
                : null;
            if (reason is { } r && (best is null || r < best.Reason)) best = new ToolMatch(tool, r);
        }
        return best;
    }

    /// <summary>The remote-access program that installs a Windows service with this name, or null.</summary>
    internal Tool? FindService(string serviceName) =>
        _tools.FirstOrDefault(t => t.Services.Any(p => p.IsMatch(serviceName.Trim()))).Tool;

    private static bool Same(string[] known, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim();
        return known.Any(k => string.Equals(k.Trim(), v, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// LOLRMM paths use * as a wildcard, sometimes %VARIABLES% and doubled backslashes. A path
    /// ending in a backslash, or without an extension, is a folder: everything inside matches.
    /// </summary>
    internal static Regex PathPattern(string pattern)
    {
        var p = Environment.ExpandEnvironmentVariables(pattern).Replace(@"\\", @"\").Trim();
        bool folder = p.EndsWith('\\') || !System.IO.Path.HasExtension(p) && !p.EndsWith('*');
        p = p.TrimEnd('\\');
        var regex = Regex.Escape(p).Replace(@"\*", ".*");
        return new Regex("^" + regex + (folder ? @"\\.*" : "") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>Service names use * and placeholders such as &lt;thumbprint&gt; for the variable part.</summary>
    internal static Regex ServicePattern(string pattern)
    {
        var p = Regex.Replace(pattern.Trim(), "<[^>]*>", "*");
        return new Regex("^" + Regex.Escape(p).Replace(@"\*", ".*") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
