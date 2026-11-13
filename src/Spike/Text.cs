using System.Globalization;

namespace Spike;

/// <summary>
/// All user-facing text. By default the display language of Windows is used; any other
/// language falls back to English. French and German follow later, in the same way as Tremors.
/// </summary>
internal static class Text
{
    internal sealed record Language(
        string Code,
        string AlarmTitle,
        string AlarmProgram,
        string NeverAsks,
        string CallFirst,
        string AlsoCall,
        string Distrust,
        string Trusted,
        string ReallyHelper,
        string YesItIs,
        string NoCutIt,
        string OffTitle,
        string OffBody,
        string HelperWatching,
        string Test);

    private static readonly Language English = new(
        Code: "en",
        AlarmTitle: "Stop! Someone is trying to take over your computer.",
        AlarmProgram: "Program: {0}",
        NeverAsks: "Microsoft, your bank and the police never ask for this.",
        CallFirst: "Call {0} first: {1}",
        AlsoCall: "Or call {0}: {1}",
        Distrust: "I don't trust this:\nshut it all down",
        Trusted: "{0} is on the phone\nand helping me",
        ReallyHelper: "Are you really talking to {0}? If in doubt, hang up and call {0} yourself: {1}",
        YesItIs: "Yes, it is {0}",
        NoCutIt: "No, shut it all down",
        OffTitle: "Your computer is now off the internet.",
        OffBody: "Nobody can watch your screen any more. Put the phone down and call {0}: {1}.\nAfter a restart the internet works again.",
        HelperWatching: "{0} is watching with {1}",
        Test: "TEST — this is what Spike shows when someone takes over your computer");

    private static readonly Language Dutch = new(
        Code: "nl",
        AlarmTitle: "Stop! Iemand probeert uw computer over te nemen.",
        AlarmProgram: "Programma: {0}",
        NeverAsks: "Microsoft, de bank en de politie vragen dit nooit.",
        CallFirst: "Bel eerst {0}: {1}",
        AlsoCall: "Of bel {0}: {1}",
        Distrust: "Ik vertrouw dit niet:\nalles eruit",
        Trusted: "{0} is aan de lijn\nen helpt me",
        ReallyHelper: "Spreekt u nu echt met {0}? Twijfelt u, hang dan op en bel {0} zelf: {1}",
        YesItIs: "Ja, het is {0}",
        NoCutIt: "Nee, alles eruit",
        OffTitle: "Uw computer is nu van het internet af.",
        OffBody: "Er kan niemand meer meekijken. Leg de telefoon neer en bel {0}: {1}.\nNa een herstart werkt het internet weer.",
        HelperWatching: "{0} kijkt mee met {1}",
        Test: "TEST: dit ziet u als iemand uw computer probeert over te nemen");

    internal static readonly IReadOnlyList<Language> All = [Dutch, English];

    internal static Language Current { get; set; } = FromCulture(CultureInfo.CurrentUICulture);

    internal static Language FromCulture(CultureInfo culture) =>
        All.FirstOrDefault(l => l.Code == culture.TwoLetterISOLanguageName) ?? English;
}
