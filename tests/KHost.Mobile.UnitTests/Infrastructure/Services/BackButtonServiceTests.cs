using KHost.Mobile.Infrastructure.Services;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Services;

// The stack the Android back button consults. Order is the whole point — the newest overlay must close first,
// or Back dismisses the sheet *behind* the one you're looking at — and a component that forgot to dispose its
// registration would keep eating Back presses for the rest of the launch.
public class BackButtonServiceTests
{
    [Fact]
    public void With_nothing_registered_Back_is_not_handled()
    {
        // False is what lets Android do its default thing (navigate back / leave the app).
        Assert.False(new BackButtonService().HandleBack());
    }

    [Fact]
    public void The_newest_handler_gets_first_refusal()
    {
        var order = new List<string>();
        var svc = new BackButtonService();
        svc.Register(() => { order.Add("first"); return true; });
        svc.Register(() => { order.Add("second"); return true; });

        Assert.True(svc.HandleBack());
        Assert.Equal(["second"], order);   // newest ran, and the older one was never consulted
    }

    [Fact]
    public void A_handler_that_declines_falls_through_to_the_one_beneath()
    {
        var order = new List<string>();
        var svc = new BackButtonService();
        svc.Register(() => { order.Add("bottom"); return true; });
        svc.Register(() => { order.Add("top"); return false; });   // nothing open to close

        Assert.True(svc.HandleBack());
        Assert.Equal(["top", "bottom"], order);
    }

    [Fact]
    public void When_every_handler_declines_Back_is_not_handled()
    {
        var svc = new BackButtonService();
        svc.Register(() => false);
        svc.Register(() => false);

        Assert.False(svc.HandleBack());
    }

    [Fact]
    public void Disposing_a_registration_takes_that_handler_out()
    {
        var svc = new BackButtonService();
        var ran = false;
        var reg = svc.Register(() => { ran = true; return true; });

        reg.Dispose();

        Assert.False(svc.HandleBack());
        Assert.False(ran);
    }

    [Fact]
    public void Disposing_twice_does_not_remove_someone_elses_handler()
    {
        // Both registrations share a delegate instance, so a second Dispose that still called Remove would
        // take the OTHER component's handler with it — List.Remove drops the first match.
        var svc = new BackButtonService();
        Func<bool> shared = () => true;
        var first = svc.Register(shared);
        svc.Register(shared);

        first.Dispose();
        first.Dispose();

        Assert.True(svc.HandleBack());   // the second registration must still be live
    }

    [Fact]
    public void Disposing_the_middle_registration_leaves_the_order_of_the_rest()
    {
        var order = new List<string>();
        var svc = new BackButtonService();
        svc.Register(() => { order.Add("bottom"); return false; });
        var middle = svc.Register(() => { order.Add("middle"); return false; });
        svc.Register(() => { order.Add("top"); return false; });

        middle.Dispose();
        svc.HandleBack();

        Assert.Equal(["top", "bottom"], order);
    }

    [Fact]
    public void Registering_null_throws_rather_than_failing_later()
    {
        Assert.Throws<ArgumentNullException>(() => new BackButtonService().Register(null!));
    }
}
