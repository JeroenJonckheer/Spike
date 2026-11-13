using Xunit;

namespace Spike.Tests;

/// <summary>
/// Recognising remote-access programs. The facts of the four real programs are the values
/// measured in Windows Sandbox on 13-11-2026 (phase 0).
/// </summary>
public class CatalogTests
{
    private static readonly Catalog Catalog = Catalog.Load();

    private static string? Found(FileFacts file) => Catalog.Find(file)?.Tool.Name;

    [Fact]
    public void The_built_in_catalog_lists_the_common_scam_tools()
    {
        var names = Catalog.Tools.Select(t => t.Name).ToList();

        Assert.True(names.Count >= 60, $"only {names.Count} programs");
        Assert.Contains("AnyDesk", names);
        Assert.Contains("TeamViewer", names);
        Assert.Contains("Quick Assist", names);
        Assert.Contains("UltraViewer", names);
    }

    [Fact]
    public void A_renamed_AnyDesk_is_recognised_by_its_signature()
    {
        // AnyDesk.exe has no OriginalFilename at all, so the name could never have helped.
        var file = new FileFacts(@"C:\Users\Ria\Downloads\factuur.exe", true,
            ["AnyDesk Software GmbH"], "AnyDesk", "AnyDesk Software GmbH", null);

        var match = Catalog.Find(file);

        Assert.Equal("AnyDesk", match?.Tool.Name);
        Assert.Equal(MatchReason.Signature, match?.Reason);
    }

    [Fact]
    public void A_broken_signature_does_not_count_but_the_product_name_still_does()
    {
        var file = new FileFacts(@"C:\x\a.exe", false, ["AnyDesk Software GmbH"], "AnyDesk");

        Assert.Equal(MatchReason.Product, Catalog.Find(file)?.Reason);
    }

    [Fact]
    public void A_broken_signature_alone_proves_nothing()
    {
        Assert.Null(Found(new FileFacts(@"C:\x\a.exe", false, ["AnyDesk Software GmbH"])));
    }

    [Theory]
    [InlineData(@"C:\Users\Ria\AppData\Local\Temp\TeamViewer\TeamViewer.exe", "TeamViewer.exe")]
    [InlineData(@"C:\Users\Ria\AppData\Local\Temp\TeamViewer\tv_x64.exe", "tv_ARM64.exe")]
    public void TeamViewer_QuickSupport_is_recognised_after_unpacking(string path, string original)
    {
        var file = new FileFacts(path, true, ["TeamViewer Germany GmbH"], "TeamViewer", "TeamViewer Germany GmbH", original);

        Assert.Equal("TeamViewer", Found(file));
    }

    [Fact]
    public void RustDesk_is_recognised_by_its_signer()
    {
        var file = new FileFacts(@"C:\Users\Ria\AppData\Local\rustdesk\rustdesk.exe", true, ["PURSLANE"], "RustDesk");

        Assert.Equal("RustDesk", Found(file));
    }

    [Fact]
    public void UltraViewer_is_recognised_although_its_names_end_in_spaces()
    {
        var file = new FileFacts(@"C:\Temp\setup.exe", false, [], "UltraViewer                                       ", "DucFabulous      ");

        Assert.Equal("UltraViewer", Found(file));
    }

    [Fact]
    public void Quick_Assist_is_recognised_by_its_Store_folder()
    {
        var file = new FileFacts(@"C:\Program Files\WindowsApps\MicrosoftCorporationII.QuickAssist_2.0.34.0_x64__8wekyb3d8bbwe\Microsoft.RemoteAssistance.QuickAssist\QuickAssist.exe",
            true, ["Microsoft Corporation"], "Microsoft® Windows® Operating System");

        Assert.Equal("Quick Assist", Found(file));
    }

    [Theory]
    [InlineData(@"C:\Windows\notepad.exe", "Microsoft Corporation", "Microsoft® Windows® Operating System")]
    [InlineData(@"C:\Program Files\Realtek\Audio\HDA\RtkAudUService64.exe", "Realtek Semiconductor Corp.", "Realtek Audio Universal Service")]
    [InlineData(@"C:\Program Files\WinRAR\WinRAR.exe", "win.rar GmbH", "WinRAR")]
    [InlineData(@"C:\Program Files\PuTTY\putty.exe", "Simon Tatham", "PuTTY suite")]
    [InlineData(@"C:\Program Files (x86)\AOMEI Backupper\Backupper.exe", "AOMEI International Network Limited", "AOMEI Backupper")]
    [InlineData(@"C:\Program Files\Advanced IP Scanner\advanced_ip_scanner.exe", "Famatech Corp.", "Advanced IP Scanner")]
    [InlineData(@"C:\Games\Something\Game.exe", "Unity Technologies ApS", "Some Game")]
    [InlineData(@"C:\Program Files\Google\Chrome\Application\chrome.exe", "Google LLC", "Google Chrome")]
    [InlineData(@"C:\Users\Ria\AppData\Local\Programs\Tremors\Tremors.exe", null, "Tremors")]
    public void Ordinary_programs_are_left_alone(string path, string? signer, string product)
    {
        var file = new FileFacts(path, signer is not null, signer is null ? [] : [signer], product, signer);

        Assert.Null(Found(file));
    }

    [Theory]
    [InlineData("AnyDesk Service", "AnyDesk")]
    [InlineData("AnyDesk Bank Service", "AnyDesk")]
    [InlineData("ScreenConnect Client (6a3f0c1d2e4b5a69)", "ScreenConnect")]
    [InlineData("UltraViewService", "UltraViewer")]
    public void Services_of_remote_access_programs_are_recognised(string service, string tool)
    {
        Assert.Equal(tool, Catalog.FindService(service)?.Name);
    }

    [Theory]
    [InlineData("Spooler")]
    [InlineData("wuauserv")]
    [InlineData("AudioSrv")]
    public void Windows_services_are_left_alone(string service)
    {
        Assert.Null(Catalog.FindService(service));
    }

    [Theory]
    [InlineData(@"C:\Program Files\AnyDesk\*", @"C:\Program Files\AnyDesk\AnyDesk.exe", true)]
    [InlineData(@"C:\Program Files\RustDesk", @"C:\Program Files\RustDesk\RustDesk.exe", true)]
    [InlineData(@"C:\Program Files\RustDesk", @"C:\Program Files\RustDeskOther\x.exe", false)]
    [InlineData(@"C:\\ProgramData\\AMMYY\\*", @"C:\ProgramData\AMMYY\aa.exe", true)]
    [InlineData(@"*\UltraViewer\", @"C:\Program Files (x86)\UltraViewer\UltraViewer_Desktop.exe", true)]
    public void Paths_from_the_catalog_match_as_intended(string pattern, string path, bool expected)
    {
        Assert.Equal(expected, Catalog.PathPattern(pattern).IsMatch(path));
    }

    [Fact]
    public void Certificate_subjects_give_the_common_name_and_organisation()
    {
        var names = Identify.Names("OID.1.3.6.1.4.1.311.60.2.1.3=VN, OID.2.5.4.15=Private Organization, CN=\"DUC FABULOUS CO.,LTD\", SERIALNUMBER=0105838409, O=\"DUC FABULOUS CO.,LTD\", L=Hanoi, C=VN");

        Assert.Equal(["DUC FABULOUS CO.,LTD"], names);
    }

    [Fact]
    public void A_real_signed_Microsoft_program_is_read_and_left_alone()
    {
        var dotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
        if (!File.Exists(dotnet)) return;

        var facts = Identify.Read(dotnet);

        Assert.True(facts.SignatureValid);
        Assert.Contains("Microsoft Corporation", facts.SignerNames);
        Assert.Null(Catalog.Find(facts));
    }

    [Fact]
    public void An_unsigned_file_has_no_signer()
    {
        var facts = Identify.Read(typeof(CatalogTests).Assembly.Location);

        Assert.False(facts.SignatureValid);
        Assert.Empty(facts.SignerNames);
    }
}
