using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.IntegrationTests;

public sealed class AppSessionTests
{
    [Fact]
    public void MySongsViewFor_returns_one_stable_instance_per_singer()
    {
        var session = new AppSession();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var aView = session.MySongsViewFor(a);
        Assert.Same(aView, session.MySongsViewFor(a));   // same singer → same instance (state sticks)
        Assert.NotSame(aView, session.MySongsViewFor(b)); // different singer → separate state
    }

    [Fact]
    public void MySongsViewFor_null_singer_returns_a_shared_fallback()
    {
        var session = new AppSession();
        Assert.Same(session.MySongsViewFor(null), session.MySongsViewFor(null));
    }

    [Fact]
    public void SetActiveSinger_raises_the_event_only_on_a_real_change()
    {
        var session = new AppSession();
        var fired = 0;
        session.ActiveSingerChanged += (_, _) => fired++;

        var id = Guid.NewGuid();
        session.SetActiveSinger(id);
        session.SetActiveSinger(id);   // same → no event
        session.SetActiveSinger(null); // change → event

        Assert.Equal(2, fired);
        Assert.Null(session.ActiveSingerId);
    }
}
