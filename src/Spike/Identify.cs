using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace Spike;

/// <summary>Reads the facts Spike needs from a program file: signature and version information.</summary>
internal static partial class Identify
{
    internal static FileFacts Read(string path)
    {
        var signers = new List<string>();
        bool valid = false;
        try
        {
#pragma warning disable SYSLIB0057 // the only API that reads an embedded Authenticode certificate
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            signers.AddRange(Names(cert.Subject));
            valid = Trusted(path);
        }
        catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or IOException or UnauthorizedAccessException)
        {
            // Not signed, or not readable: Spike then relies on the version information.
        }

        FileVersionInfo? version = null;
        try { version = FileVersionInfo.GetVersionInfo(path); }
        catch (Exception e) when (e is FileNotFoundException or IOException or UnauthorizedAccessException) { }

        return new FileFacts(
            path,
            valid,
            signers,
            Clean(version?.ProductName),
            Clean(version?.CompanyName),
            Clean(version?.OriginalFilename));
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>The CN and O parts of a certificate subject, without quotes; duplicates removed.</summary>
    internal static IEnumerable<string> Names(string subject)
    {
        var found = new List<string>();
        foreach (System.Text.RegularExpressions.Match m in SubjectPart().Matches(subject))
        {
            var value = m.Groups["v"].Value.Trim();
            if (value.Length > 1 && value[0] == '"' && value[^1] == '"') value = value[1..^1].Replace("\"\"", "\"");
            if (value.Length > 0 && !found.Contains(value, StringComparer.OrdinalIgnoreCase)) found.Add(value);
        }
        return found;
    }

    [GeneratedRegex(@"(?:^|,\s*)(?:CN|O)=(?<v>""(?:[^""]|"""")*""|[^,]*)")]
    private static partial Regex SubjectPart();

    // ---- WinVerifyTrust: does Windows trust this file's signature? ----

    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
    private const uint WTD_UI_NONE = 2, WTD_REVOKE_NONE = 0, WTD_CHOICE_FILE = 1;
    private const uint WTD_STATEACTION_VERIFY = 1, WTD_STATEACTION_CLOSE = 2;
    private const uint WTD_CACHE_ONLY_URL_RETRIEVAL = 0x1000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref WINTRUST_DATA data);

    /// <summary>True when the embedded signature is intact and chains to a trusted root. Offline: no revocation check.</summary>
    private static bool Trusted(string path)
    {
        var file = new WINTRUST_FILE_INFO { cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(), pcwszFilePath = path };
        IntPtr pFile = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
        try
        {
            Marshal.StructureToPtr(file, pFile, false);
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = pFile,
                dwStateAction = WTD_STATEACTION_VERIFY,
                dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL,
            };
            var action = GenericVerifyV2;
            int result = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            data.dwStateAction = WTD_STATEACTION_CLOSE;
            WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            return result == 0;
        }
        finally
        {
            Marshal.DestroyStructure<WINTRUST_FILE_INFO>(pFile);
            Marshal.FreeHGlobal(pFile);
        }
    }
}
