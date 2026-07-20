using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class AppSessionTests
{
    [Fact]
    public void ActiveVenueId_is_null_until_set()
    {
        Assert.Null(new AppSession().ActiveVenueId);
    }

    [Fact]
    public void SetActiveVenue_updates_the_id_and_raises_the_event()
    {
        var session = new AppSession();
        var fired = 0;
        session.ActiveVenueChanged += (_, _) => fired++;
        var venue = Guid.NewGuid();

        session.SetActiveVenue(venue);

        Assert.Equal(venue, session.ActiveVenueId);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void SetActiveVenue_is_a_no_op_when_the_value_is_unchanged()
    {
        var session = new AppSession();
        var venue = Guid.NewGuid();
        session.SetActiveVenue(venue);

        var fired = 0;
        session.ActiveVenueChanged += (_, _) => fired++;
        session.SetActiveVenue(venue);   // same value

        Assert.Equal(0, fired);
    }

    [Fact]
    public void SetActiveVenue_null_clears_and_raises_when_previously_set()
    {
        var session = new AppSession();
        session.SetActiveVenue(Guid.NewGuid());

        var fired = 0;
        session.ActiveVenueChanged += (_, _) => fired++;
        session.SetActiveVenue(null);

        Assert.Null(session.ActiveVenueId);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void SetActiveVenue_records_the_pin_flag()
    {
        var session = new AppSession();

        session.SetActiveVenue(Guid.NewGuid());               // defaults to unpinned (auto)
        Assert.False(session.ActiveVenuePinned);

        session.SetActiveVenue(Guid.NewGuid(), pinned: true); // a manual pick
        Assert.True(session.ActiveVenuePinned);
    }

    [Fact]
    public void SetActiveVenue_can_unpin_the_same_venue_without_raising()
    {
        var session = new AppSession();
        var venue = Guid.NewGuid();
        session.SetActiveVenue(venue, pinned: true);

        var fired = 0;
        session.ActiveVenueChanged += (_, _) => fired++;
        session.SetActiveVenue(venue, pinned: false);   // "resume auto-detect" on the same venue

        Assert.False(session.ActiveVenuePinned);
        Assert.Equal(venue, session.ActiveVenueId);
        Assert.Equal(0, fired);   // venue didn't change, so no refresh event
    }

    // ---- Active singer ----

    [Fact]
    public void ActiveSingerId_is_null_until_set()
    {
        Assert.Null(new AppSession().ActiveSingerId);
    }

    [Fact]
    public void SetActiveSinger_updates_the_id_and_raises_the_event()
    {
        var session = new AppSession();
        var fired = 0;
        session.ActiveSingerChanged += (_, _) => fired++;
        var singer = Guid.NewGuid();

        session.SetActiveSinger(singer);

        Assert.Equal(singer, session.ActiveSingerId);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void SetActiveSinger_is_a_no_op_when_the_value_is_unchanged()
    {
        var session = new AppSession();
        var singer = Guid.NewGuid();
        session.SetActiveSinger(singer);

        var fired = 0;
        session.ActiveSingerChanged += (_, _) => fired++;
        session.SetActiveSinger(singer);   // same value

        Assert.Equal(0, fired);
    }

    // ---- Tutorial signals ----

    [Fact]
    public void TutorialResolved_and_LandingResolved_default_false_and_are_settable()
    {
        var session = new AppSession();
        Assert.False(session.TutorialResolved);
        Assert.False(session.LandingResolved);

        session.TutorialResolved = true;
        session.LandingResolved = true;

        Assert.True(session.TutorialResolved);
        Assert.True(session.LandingResolved);
    }

    [Fact]
    public void SetTutorialVenueDetail_drives_the_id_and_raises_only_on_a_real_change()
    {
        var session = new AppSession();
        Assert.Null(session.TutorialVenueDetailId);

        var fired = 0;
        session.TutorialVenueDetailChanged += (_, _) => fired++;
        var venue = Guid.NewGuid();

        session.SetTutorialVenueDetail(venue);
        Assert.Equal(venue, session.TutorialVenueDetailId);
        Assert.Equal(1, fired);

        session.SetTutorialVenueDetail(venue);   // unchanged → no event
        Assert.Equal(1, fired);

        session.SetTutorialVenueDetail(null);     // cleared → the tour closes the detail it opened
        Assert.Null(session.TutorialVenueDetailId);
        Assert.Equal(2, fired);
    }
}
