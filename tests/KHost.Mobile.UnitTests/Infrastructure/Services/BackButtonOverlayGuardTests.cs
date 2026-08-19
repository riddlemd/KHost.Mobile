using KHost.Mobile.Abstractions.Services;
using KHost.Mobile.Infrastructure.Services;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Services;

// Driven through a real BackButtonService: the guard's whole job is what it hands that registry.
public class BackButtonOverlayGuardTests
{
    [Fact]
    public void Back_closes_an_open_overlay_and_re_renders_the_host()
    {
        var svc = new BackButtonService();
        var closed = 0;
        var rendered = 0;
        using var guard = new BackButtonOverlayGuard(svc, () => { closed++; return true; }, () => rendered++);

        Assert.True(svc.HandleBack());   // consumed, so Android doesn't also act on it
        Assert.Equal(1, closed);
        Assert.Equal(1, rendered);
    }

    [Fact]
    public void Back_with_nothing_open_is_not_consumed_and_does_not_re_render()
    {
        var svc = new BackButtonService();
        var rendered = 0;
        using var guard = new BackButtonOverlayGuard(svc, () => false, () => rendered++);

        Assert.False(svc.HandleBack());   // false is what lets Android leave the app
        Assert.Equal(0, rendered);        // nothing closed → nothing to redraw
    }

    [Fact]
    public void Disposing_stops_the_guard_consuming_back_presses()
    {
        // A component that unmounted must not keep eating Back for the rest of the launch.
        var svc = new BackButtonService();
        var closed = 0;
        var guard = new BackButtonOverlayGuard(svc, () => { closed++; return true; }, () => { });

        guard.Dispose();

        Assert.False(svc.HandleBack());
        Assert.Equal(0, closed);
    }

    [Fact]
    public void Disposing_twice_is_safe_and_leaves_another_guard_registered()
    {
        // Blazor can dispose a component twice; doing so must not unregister someone else's handler.
        var svc = new BackButtonService();
        var otherClosed = 0;
        using var other = new BackButtonOverlayGuard(svc, () => { otherClosed++; return true; }, () => { });
        var guard = new BackButtonOverlayGuard(svc, () => true, () => { });

        guard.Dispose();
        guard.Dispose();

        Assert.True(svc.HandleBack());
        Assert.Equal(1, otherClosed);
    }

    [Fact]
    public void The_newest_guard_closes_first_and_the_one_beneath_it_is_untouched()
    {
        // Each guard reports false once its own overlay is gone, which is what lets the press fall through.
        var svc = new BackButtonService();
        var order = new List<string>();
        var sheetOpen = true;
        var pageOpen = true;
        using var page = new BackButtonOverlayGuard(svc, () =>
        {
            if (!pageOpen)
                return false;
            pageOpen = false;
            order.Add("page");
            return true;
        }, () => { });
        using var sheet = new BackButtonOverlayGuard(svc, () =>
        {
            if (!sheetOpen)
                return false;
            sheetOpen = false;
            order.Add("sheet");
            return true;
        }, () => { });

        svc.HandleBack();
        svc.HandleBack();

        Assert.Equal(["sheet", "page"], order);
        Assert.False(svc.HandleBack());   // both closed → the press reaches Android
    }

    [Fact]
    public void A_guard_that_has_nothing_open_defers_to_the_one_beneath_it()
    {
        // The layout's ⋮ menu still closes when the page's guard owns no open overlay.
        var svc = new BackButtonService();
        var menuClosed = 0;
        using var menu = new BackButtonOverlayGuard(svc, () => { menuClosed++; return true; }, () => { });
        using var page = new BackButtonOverlayGuard(svc, () => false, () => { });

        Assert.True(svc.HandleBack());
        Assert.Equal(1, menuClosed);
    }

    [Theory]
    [InlineData("backButton")]
    [InlineData("closeTopMost")]
    [InlineData("notifyStateChanged")]
    public void Rejects_a_null_dependency(string missing)
    {
        var svc = missing == "backButton" ? null : new BackButtonService();
        Func<bool>? close = missing == "closeTopMost" ? null : () => true;
        Action? notify = missing == "notifyStateChanged" ? null : () => { };

        Assert.Throws<ArgumentNullException>(() => new BackButtonOverlayGuard(svc!, close!, notify!));
    }
}
