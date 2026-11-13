using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spike;

/// <summary>
/// What the Guard (service) and the Dog (tray program) say to each other: one JSON object per line.
/// </summary>
internal sealed record PipeMessage
{
    // Guard -> Dog
    internal const string Alarm = "alarm";            // Id, Tool, Test
    internal const string AlarmEnd = "alarmEnd";      // Id: the program went away by itself
    internal const string Offline = "offline";        // the red button has done its work
    internal const string Banner = "banner";          // Tool: a trusted helper is watching
    internal const string BannerEnd = "bannerEnd";
    internal const string StopAttempt = "stopAttempt"; // someone is stopping the service
    internal const string Download = "download";      // Tool: an installer appeared in Downloads
    internal const string Record = "record";          // On: take screenshots or stop
    internal const string Reload = "reload";          // settings changed

    // Dog -> Guard
    internal const string Choice = "choice";          // Id, Pick
    internal const string RemoteInput = "remoteInput"; // Count: someone else moves the mouse
    internal const string Screenshot = "screenshot";  // Data: JPEG, base64
    internal const string TestAlarm = "test";

    internal const string Distrust = "distrust";
    internal const string Trusted = "trusted";
    internal const string Done = "done";              // the helper is done: close the program

    public string Type { get; init; } = "";
    public int Id { get; init; }
    public string? Tool { get; init; }
    public bool Test { get; init; }
    public bool Again { get; init; }   // alarm for a new connection of a program the user trusted before
    public bool On { get; init; }
    public string? Pick { get; init; }
    public int Count { get; init; }
    public string? Data { get; init; }

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    internal string ToLine() => JsonSerializer.Serialize(this, Json);

    internal static PipeMessage? FromLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try { return JsonSerializer.Deserialize<PipeMessage>(line, Json); }
        catch (JsonException) { return null; }
    }
}
