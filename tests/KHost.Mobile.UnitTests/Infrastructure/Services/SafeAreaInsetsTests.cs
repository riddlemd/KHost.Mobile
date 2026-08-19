using KHost.Mobile.Infrastructure.Services;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Services;

// Android's inset listener fires on every window-insets pass, so the unchanged-value guard is what stops the
// layout re-rendering on every frame.
public class SafeAreaInsetsTests
{
    [Fact]
    public void Set_publishes_the_values_and_announces_the_change()
    {
        var insets = new SafeAreaInsets();
        var fired = 0;
        insets.Changed += (_, _) => fired++;

        insets.Set(24, 48);

        Assert.Equal(24, insets.Top);
        Assert.Equal(48, insets.Bottom);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Re_setting_the_same_insets_does_not_announce_a_change()
    {
        var insets = new SafeAreaInsets();
        insets.Set(24, 48);
        var fired = 0;
        insets.Changed += (_, _) => fired++;

        insets.Set(24, 48);

        Assert.Equal(0, fired);
    }

    [Theory]
    [InlineData(25d, 48d)]   // top moved
    [InlineData(24d, 49d)]   // bottom moved
    public void A_move_on_either_edge_announces_a_change(double top, double bottom)
    {
        var insets = new SafeAreaInsets();
        insets.Set(24, 48);
        var fired = 0;
        insets.Changed += (_, _) => fired++;

        insets.Set(top, bottom);

        Assert.Equal(1, fired);
        Assert.Equal(top, insets.Top);
        Assert.Equal(bottom, insets.Bottom);
    }

    [Fact]
    public void The_first_set_of_zero_insets_is_not_a_change()
    {
        // A device with no cutout reports 0/0, which is already the starting state — nothing to redraw.
        var insets = new SafeAreaInsets();
        var fired = 0;
        insets.Changed += (_, _) => fired++;

        insets.Set(0, 0);

        Assert.Equal(0, fired);
    }
}
