using Academy.Domain;
using Academy.Domain.Enums;

namespace Academy.Domain.Tests;

public class SessionPartsTests
{
    private static PartFacts Lesson(bool done) => new(SessionPartKind.LessonVideo, done);
    private static PartFacts Test(bool passed, int failed = 0, int? n = null) =>
        new(SessionPartKind.Test, passed, failed, n);
    private static PartFacts Discussion(bool watched) => new(SessionPartKind.Discussion, watched);

    [Fact]
    public void First_part_is_open_and_later_parts_are_locked()
    {
        var s = SessionParts.Statuses([Lesson(false), Test(false), Lesson(false)]);
        Assert.Equal([PartStatus.Open, PartStatus.Locked, PartStatus.Locked], s);
    }

    [Fact]
    public void A_part_opens_when_every_part_before_it_is_done()
    {
        var s = SessionParts.Statuses([Lesson(true), Test(false), Lesson(false)]);
        Assert.Equal([PartStatus.Done, PartStatus.Open, PartStatus.Locked], s);
    }

    [Fact]
    public void Discussion_opens_when_its_test_is_passed()
    {
        var s = SessionParts.Statuses([Lesson(true), Test(true), Discussion(false)]);
        Assert.Equal(PartStatus.Open, s[2]);
    }

    [Fact]
    public void Discussion_opens_at_exactly_N_failures()
    {
        Assert.Equal(PartStatus.Open, SessionParts.Statuses([Test(false, failed: 3, n: 3), Discussion(false)])[1]);
    }

    [Fact]
    public void Discussion_stays_locked_at_N_minus_1_failures()
    {
        Assert.Equal(PartStatus.Locked, SessionParts.Statuses([Test(false, failed: 2, n: 3), Discussion(false)])[1]);
    }

    [Fact]
    public void Discussion_without_N_opens_only_on_a_pass()
    {
        Assert.Equal(PartStatus.Locked, SessionParts.Statuses([Test(false, failed: 50), Discussion(false)])[1]);
    }

    [Fact]
    public void Discussion_stays_locked_while_its_test_is_locked()
    {
        // Failures cannot exist on a locked test in practice; the rule still must not open it.
        var s = SessionParts.Statuses([Lesson(false), Test(false, failed: 5, n: 1), Discussion(false)]);
        Assert.Equal(PartStatus.Locked, s[2]);
    }

    [Fact]
    public void Part_after_a_pair_needs_the_pass_AND_the_discussion_watched()
    {
        Assert.Equal(PartStatus.Locked,
            SessionParts.Statuses([Test(false, failed: 3, n: 3), Discussion(true), Lesson(false)])[2]);
        Assert.Equal(PartStatus.Locked,
            SessionParts.Statuses([Test(true), Discussion(false), Lesson(false)])[2]);
        Assert.Equal(PartStatus.Open,
            SessionParts.Statuses([Test(true), Discussion(true), Lesson(false)])[2]);
    }

    [Fact]
    public void A_done_part_behind_an_undone_one_shows_locked()
    {
        // An admin inserted a new lesson before a test the learner had already passed.
        var s = SessionParts.Statuses([Lesson(false), Test(true)]);
        Assert.Equal([PartStatus.Open, PartStatus.Locked], s);
    }

    [Fact]
    public void Session_is_complete_only_when_every_part_is_done()
    {
        Assert.False(SessionParts.IsComplete([Lesson(true), Test(true), Discussion(false)]));
        Assert.True(SessionParts.IsComplete([Lesson(true), Test(true), Discussion(true)]));
    }

    [Fact]
    public void A_session_with_no_parts_is_never_complete()
    {
        Assert.False(SessionParts.IsComplete([]));
        Assert.Empty(SessionParts.Statuses([]));
    }
}
