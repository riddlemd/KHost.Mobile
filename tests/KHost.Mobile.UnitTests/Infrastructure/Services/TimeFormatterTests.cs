using System.Globalization;
using KHost.Mobile.Infrastructure.Services;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Services;

// Asserted through a real format call, not string equality: a swapped "MM"/"mm" or "HH"/"hh" reads fine in the
// source and only shows up on screen. Invariant culture so the expected text survives another machine's locale.
public class TimeFormatterTests
{
    private static readonly TimeFormatter Formatter = new();

    private static readonly DateTimeOffset Evening =
        new(2026, 3, 4, 20, 15, 0, TimeSpan.FromHours(-5));

    private static string Format(string pattern) => Evening.ToString(pattern, CultureInfo.InvariantCulture);

    [Fact]
    public void DatePattern_renders_an_unambiguous_month_day_year()
    {
        Assert.Equal("Mar 4, 2026", Format(Formatter.DatePattern));
    }

    [Fact]
    public void ShortDatePattern_drops_the_year_but_keeps_the_month()
    {
        Assert.Equal("Mar 4", Format(Formatter.ShortDatePattern));
    }

    [Fact]
    public void DateTimePattern_renders_a_24_hour_clock_with_no_meridiem()
    {
        var rendered = Format(Formatter.DateTimePattern(use24Hour: true));

        Assert.Equal("Mar 4, 2026 · 20:15", rendered);
        Assert.DoesNotContain("PM", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DateTimePattern_renders_a_12_hour_clock_with_a_meridiem()
    {
        // Without the meridiem, 8:15 PM and 8:15 AM render identically in the history list.
        Assert.Equal("Mar 4, 2026 · 8:15 PM", Format(Formatter.DateTimePattern(use24Hour: false)));
    }

    [Fact]
    public void A_morning_time_keeps_the_two_clocks_distinguishable()
    {
        var morning = new DateTimeOffset(2026, 3, 4, 8, 5, 0, TimeSpan.FromHours(-5));

        Assert.Equal("Mar 4, 2026 · 08:05", morning.ToString(Formatter.DateTimePattern(true), CultureInfo.InvariantCulture));
        Assert.Equal("Mar 4, 2026 · 8:05 AM", morning.ToString(Formatter.DateTimePattern(false), CultureInfo.InvariantCulture));
    }
}
