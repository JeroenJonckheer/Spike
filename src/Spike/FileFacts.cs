namespace Spike;

/// <summary>
/// What Spike knows about a program file. None of this changes when the file is renamed,
/// except <see cref="Path"/> itself.
/// </summary>
/// <param name="Path">Full path of the file.</param>
/// <param name="SignatureValid">Windows trusts the file's digital signature.</param>
/// <param name="Signers">The signer's common name (CN) and organisation (O), when signed.</param>
/// <param name="Product">ProductName from the version information.</param>
/// <param name="Company">CompanyName from the version information.</param>
/// <param name="OriginalFileName">OriginalFilename from the version information.</param>
internal sealed record FileFacts(
    string Path,
    bool SignatureValid = false,
    IReadOnlyList<string>? Signers = null,
    string? Product = null,
    string? Company = null,
    string? OriginalFileName = null)
{
    internal IReadOnlyList<string> SignerNames => Signers ?? [];

    internal string FileName => System.IO.Path.GetFileName(Path);
}
