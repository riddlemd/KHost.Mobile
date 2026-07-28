using KHost.Mobile.Infrastructure.Services;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Services;

public class DateTimeInputConverterTests
{
    private static readonly DateTimeInputConverter Input = new();

    // A zone with daylight saving, so the "offset for the date entered" rule is actually exercised.
    private static readonly TimeZoneInfo Eastern =
        TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York");

    private sealed class FixedZoneClock(TimeZoneInfo zone, DateTimeOffset now) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => zone;
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static TimeProvider Clock(DateTimeOffset now) => new FixedZoneClock(Eastern, now);

    [Fact]
    public void Formats_in_the_shape_the_control_accepts()
    {
        // 2026-07-27 18:30 UTC is 14:30 in Eastern daylight time.
        var clock = Clock(new DateTimeOffset(2026, 7, 27, 18, 30, 0, TimeSpan.Zero));

        Assert.Equal("2026-07-27T14:30", new DateTimeInputConverter(clock).Format(clock.GetUtcNow()));
    }

    [Fact]
    public void Round_trips_a_value()
    {
        var clock = Clock(new DateTimeOffset(2026, 7, 27, 18, 30, 0, TimeSpan.Zero));
        var text = new DateTimeInputConverter(clock).Format(clock.GetUtcNow());

        Assert.True(new DateTimeInputConverter(clock).TryParse(text, out var parsed));
        Assert.Equal(clock.GetUtcNow(), parsed);
    }

    [Fact]
    public void Keeps_the_typed_wall_clock_time()
    {
        var clock = Clock(new DateTimeOffset(2026, 7, 27, 18, 30, 0, TimeSpan.Zero));

        Assert.True(new DateTimeInputConverter(clock).TryParse("2026-07-20T21:15", out var parsed));
        Assert.Equal(new DateTime(2026, 7, 20, 21, 15, 0), parsed.DateTime);
    }

    [Fact]
    public void Uses_the_offset_of_the_date_entered_not_of_today()
    {
        // "Now" is summer (EDT, -04:00); the performance being backfilled is in January (EST, -05:00).
        var clock = Clock(new DateTimeOffset(2026, 7, 27, 18, 30, 0, TimeSpan.Zero));

        Assert.True(new DateTimeInputConverter(clock).TryParse("2026-01-15T20:00", out var winter));

        Assert.Equal(TimeSpan.FromHours(-5), winter.Offset);
        // The wall-clock time the user typed is what survives, not a time shifted by an hour.
        Assert.Equal(new DateTime(2026, 1, 15, 20, 0, 0), winter.DateTime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a date")]
    public void Rejects_unusable_text(string? text)
    {
        var clock = Clock(new DateTimeOffset(2026, 7, 27, 18, 30, 0, TimeSpan.Zero));

        Assert.False(new DateTimeInputConverter(clock).TryParse(text, out _));
    }
}
