using KHost.Mobile.Models;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class SongListItemTests
{
    private static Performance Perf(int howItWent, DateTimeOffset date)
        => new() { HowItWent = howItWent, Date = date };

    [Fact]
    public void AverageHowItWent_is_null_when_there_are_no_performances()
    {
        var song = new SongListItem();

        Assert.Null(song.AverageHowItWent);
    }

    [Fact]
    public void AverageHowItWent_is_null_when_every_performance_is_unrated()
    {
        var song = new SongListItem
        {
            Performances = [Perf(0, DateTimeOffset.Now), Perf(0, DateTimeOffset.Now)],
        };

        Assert.Null(song.AverageHowItWent);
    }

    [Fact]
    public void AverageHowItWent_excludes_unrated_performances_from_the_mean()
    {
        // 0 is "logged but not rated" and must not drag the average down: mean of {4, 2} is 3.0, not 2.0.
        var song = new SongListItem
        {
            Performances = [Perf(0, DateTimeOffset.Now), Perf(4, DateTimeOffset.Now), Perf(2, DateTimeOffset.Now)],
        };

        Assert.Equal(3.0, song.AverageHowItWent);
    }

    [Fact]
    public void LastSungAt_is_null_when_never_sung()
    {
        Assert.Null(new SongListItem().LastSungAt);
    }

    [Fact]
    public void LastSungAt_returns_the_latest_date_not_the_last_added()
    {
        var earliest = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var latest = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var middle = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);

        // Deliberately not in chronological order — the newest date should win regardless of list position.
        var song = new SongListItem
        {
            Performances = [Perf(5, earliest), Perf(3, latest), Perf(4, middle)],
        };

        Assert.Equal(latest, song.LastSungAt);
    }
}
