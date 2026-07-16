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
}
