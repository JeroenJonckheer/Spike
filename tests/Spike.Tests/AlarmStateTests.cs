using Xunit;
using static Spike.AlarmState;

namespace Spike.Tests;

public class AlarmStateTests
{
    private static readonly DateTime T0 = new(2026, 11, 13, 14, 0, 0);

    private static Dictionary<string, List<int>> Running(params (string Tool, int[] Pids)[] tools) =>
        tools.ToDictionary(t => t.Tool, t => t.Pids.ToList());

    [Fact]
    public void A_program_that_starts_gives_an_alarm_and_is_frozen()
    {
        var s = new AlarmState();

        var actions = s.Seen(Running(("AnyDesk", [10, 11])), T0);

        Assert.Contains(actions, a => a is RaiseAlarm { Tool: "AnyDesk" });
        Assert.Contains(actions, a => a is Freeze f && f.Pids.SequenceEqual([10, 11]));
    }

    [Fact]
    public void A_program_that_keeps_running_does_not_alarm_twice_but_new_processes_are_frozen_too()
    {
        var s = new AlarmState();
        s.Seen(Running(("AnyDesk", [10])), T0);

        var actions = s.Seen(Running(("AnyDesk", [10, 12])), T0.AddSeconds(1));

        Assert.DoesNotContain(actions, a => a is RaiseAlarm);
        Assert.Contains(actions, a => a is Freeze f && f.Pids.SequenceEqual([12]));
    }

    [Fact]
    public void I_do_not_trust_this_shuts_everything_down()
    {
        var s = new AlarmState();
        var id = s.Seen(Running(("AnyDesk", [10])), T0).OfType<RaiseAlarm>().Single().Id;

        var actions = s.Choose(id, PipeMessage.Distrust, T0);

        Assert.Equal([new ShutDown(id)], actions);
        Assert.Empty(s.Open);
    }

    [Fact]
    public void My_helper_is_on_the_phone_unfreezes_and_shows_the_banner()
    {
        var s = new AlarmState();
        var id = s.Seen(Running(("TeamViewer", [20, 21])), T0).OfType<RaiseAlarm>().Single().Id;

        var actions = s.Choose(id, PipeMessage.Trusted, T0);

        Assert.Contains(actions, a => a is Unfreeze u && u.Pids.Order().SequenceEqual([20, 21]));
        Assert.Contains(actions, a => a is ShowBanner { Tool: "TeamViewer" });
    }

    [Fact]
    public void After_my_helper_new_processes_of_the_same_program_are_not_frozen()
    {
        var s = new AlarmState();
        var id = s.Seen(Running(("TeamViewer", [20])), T0).OfType<RaiseAlarm>().Single().Id;
        s.Choose(id, PipeMessage.Trusted, T0);

        var actions = s.Seen(Running(("TeamViewer", [20, 22])), T0.AddSeconds(5));

        Assert.Empty(actions);
    }

    [Fact]
    public void A_trusted_program_that_stops_and_starts_again_gives_a_new_alarm()
    {
        var s = new AlarmState();
        var id = s.Seen(Running(("TeamViewer", [20])), T0).OfType<RaiseAlarm>().Single().Id;
        s.Choose(id, PipeMessage.Trusted, T0);

        var stopped = s.Seen(Running(), T0.AddMinutes(10));
        var again = s.Seen(Running(("TeamViewer", [30])), T0.AddMinutes(20));

        Assert.Contains(stopped, a => a is HideBanner h && h.Id == id);
        Assert.Contains(again, a => a is RaiseAlarm r && r.Id != id);
    }

    [Fact]
    public void Another_program_while_the_helper_watches_gives_an_alarm()
    {
        var s = new AlarmState();
        var id = s.Seen(Running(("TeamViewer", [20])), T0).OfType<RaiseAlarm>().Single().Id;
        s.Choose(id, PipeMessage.Trusted, T0);

        var actions = s.Seen(Running(("TeamViewer", [20]), ("AnyDesk", [40])), T0.AddMinutes(1));

        Assert.Contains(actions, a => a is RaiseAlarm { Tool: "AnyDesk" });
    }

    [Fact]
    public void A_program_that_goes_away_before_a_choice_ends_the_alarm()
    {
        var s = new AlarmState();
        var id = s.Seen(Running(("AnyDesk", [10])), T0).OfType<RaiseAlarm>().Single().Id;

        var actions = s.Seen(Running(), T0.AddSeconds(30));

        Assert.Equal([new EndAlarm(id)], actions);
    }

    [Fact]
    public void Someone_else_moving_the_mouse_gives_an_alarm_for_an_unknown_program()
    {
        var s = new AlarmState();

        var actions = s.RemoteInput(T0);

        Assert.Contains(actions, a => a is RaiseAlarm { Tool: Unknown });
        Assert.Empty(s.RemoteInput(T0.AddSeconds(1)));
    }

    [Fact]
    public void Remote_input_during_a_known_program_does_not_give_a_second_alarm()
    {
        var s = new AlarmState();
        s.Seen(Running(("AnyDesk", [10])), T0);

        Assert.Empty(s.RemoteInput(T0.AddSeconds(2)));
    }

    [Fact]
    public void After_trusting_an_unknown_program_Spike_stays_quiet_for_a_while()
    {
        var s = new AlarmState();
        var id = s.RemoteInput(T0).OfType<RaiseAlarm>().Single().Id;
        s.Choose(id, PipeMessage.Trusted, T0);

        Assert.Empty(s.RemoteInput(T0.AddMinutes(5)));
        Assert.NotEmpty(s.RemoteInput(T0 + QuietAfterTrustedUnknown + TimeSpan.FromSeconds(1)));
    }

    private static (AlarmState State, int Id) Trusted(string tool, params int[] pids)
    {
        var s = new AlarmState();
        var id = s.Seen(Running((tool, pids)), T0).OfType<RaiseAlarm>().Single().Id;
        s.Choose(id, PipeMessage.Trusted, T0);
        return (s, id);
    }

    [Fact]
    public void After_my_helper_the_user_can_still_shut_everything_down()
    {
        var (s, id) = Trusted("AnyDesk", 10);

        Assert.Equal([new ShutDown(id)], s.Choose(id, PipeMessage.Distrust, T0.AddMinutes(2)));
    }

    [Fact]
    public void My_helper_is_done_closes_the_program_and_the_banner()
    {
        var (s, id) = Trusted("AnyDesk", 10, 11);

        var actions = s.Choose(id, PipeMessage.Done, T0.AddMinutes(5));

        Assert.Contains(actions, a => a is HideBanner h && h.Id == id);
        Assert.Contains(actions, a => a is CloseTool c && c.Pids.Order().SequenceEqual([10, 11]));
    }

    [Fact]
    public void A_new_connection_of_a_trusted_program_gives_a_new_alarm_and_freezes_it_again()
    {
        var (s, id) = Trusted("AnyDesk", 10);

        var actions = s.NewConnection("AnyDesk", T0.AddMinutes(10));

        Assert.Contains(actions, a => a is HideBanner h && h.Id == id);
        Assert.Contains(actions, a => a is RaiseAlarm { Tool: "AnyDesk", Again: true } r && r.Id != id);
        Assert.Contains(actions, a => a is Freeze f && f.Pids.SequenceEqual([10]));
    }

    [Fact]
    public void A_new_connection_while_the_alarm_is_still_up_changes_nothing()
    {
        var s = new AlarmState();
        s.Seen(Running(("AnyDesk", [10])), T0);

        Assert.Empty(s.NewConnection("AnyDesk", T0.AddSeconds(5)));
    }

    [Fact]
    public void Remote_input_after_a_quiet_while_counts_as_a_new_connection()
    {
        var (s, _) = Trusted("AnyDesk", 10);
        Assert.Empty(s.RemoteInput(T0.AddMinutes(1)));                 // Sanne still at work

        var later = s.RemoteInput(T0.AddMinutes(1) + TrustIdle + TimeSpan.FromSeconds(1));

        Assert.Contains(later, a => a is RaiseAlarm { Again: true });
    }

    [Fact]
    public void A_choice_counts_only_once()
    {
        var s = new AlarmState();
        var id = s.Seen(Running(("AnyDesk", [10])), T0).OfType<RaiseAlarm>().Single().Id;
        s.Choose(id, PipeMessage.Trusted, T0);

        Assert.Empty(s.Choose(id, PipeMessage.Trusted, T0));
    }
}
