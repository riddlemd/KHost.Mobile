using KHost.Mobile.Models;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class VenueHistoryTests
{
    private static readonly Guid VenueA = Guid.NewGuid();
    private static readonly Guid VenueB = Guid.NewGuid();

    private static DateTimeOffset Day(int d) => new(2026, 1, d, 20, 0, 0, TimeSpan.Zero);

    private static SongListItem Song(string title, params Performance[] perfs) =>
        new() { Title = title, Performances = [.. perfs] };

    private static Performance Perf(Guid? venue, int howItWent, int day) =>
        new() { VenueId = venue, HowItWent = howItWent, Date = Day(day) };

    private static List<SongListItem> Library() =>
    [
        Song("Alpha", Perf(VenueA, 5, 3), Perf(VenueA, 3, 1)),
        Song("Beta",  Perf(VenueA, 0, 2), Perf(VenueB, 4, 4)),   // unrated at A, rated at B
        Song("Gamma", Perf(VenueA, 4, 5)),
        Song("Delta", Perf(null, 5, 6)),                          // untagged — never counted
    ];

    [Fact]
    public void ForVenue_counts_every_tagged_sing_ignoring_other_venues_and_untagged()
    {
        var h = VenueHistory.ForVenue(Library(), VenueA);

        Assert.Equal(4, h.Sings);   // Alpha×2 + Beta(unrated) + Gamma; Beta@B and Delta(untagged) excluded
    }

    [Fact]
    public void ForVenue_averages_only_rated_sings()
    {
        var h = VenueHistory.ForVenue(Library(), VenueA);

        Assert.Equal(4.0, h.Average);   // (5 + 3 + 4) / 3; the unrated Beta sing is excluded
    }

    [Fact]
    public void ForVenue_last_sung_is_the_newest_tagged_date()
    {
        var h = VenueHistory.ForVenue(Library(), VenueA);

        Assert.Equal(Day(5), h.LastSung);   // Gamma; Delta (day 6) is untagged
    }

    [Fact]
    public void ForVenue_goto_lists_rated_songs_by_average_then_count()
    {
        var h = VenueHistory.ForVenue(Library(), VenueA);

        // Alpha (avg 4, 2 sings) and Gamma (avg 4, 1 sing); Beta excluded (no rated sing here).
        Assert.Equal(["Alpha", "Gamma"], h.GoTo.Select(g => g.Title).ToArray());
        Assert.Equal(2, h.GoTo[0].Count);
        Assert.Equal(4.0, h.GoTo[0].Average);
    }

    [Fact]
    public void ForVenue_recent_is_newest_first_and_keeps_unrated_sings()
    {
        var h = VenueHistory.ForVenue(Library(), VenueA);

        Assert.Equal(["Gamma", "Alpha", "Beta", "Alpha"], h.Recent.Select(r => r.Title).ToArray());
        Assert.Equal(0, h.Recent[2].Rating);   // the unrated Beta sing is still shown
    }

    [Fact]
    public void ForVenue_respects_the_topGoTo_and_recent_caps()
    {
        var h = VenueHistory.ForVenue(Library(), VenueA, topGoTo: 1, recent: 2);

        Assert.Single(h.GoTo);
        Assert.Equal("Alpha", h.GoTo[0].Title);   // highest avg, most sings
        Assert.Equal(2, h.Recent.Count);
        Assert.Equal(["Gamma", "Alpha"], h.Recent.Select(r => r.Title).ToArray());
    }

    [Fact]
    public void ForVenue_returns_empty_when_nothing_is_tagged_with_the_venue()
    {
        var h = VenueHistory.ForVenue(Library(), Guid.NewGuid());

        Assert.Equal(0, h.Sings);
        Assert.Null(h.Average);
        Assert.Null(h.LastSung);
        Assert.Empty(h.GoTo);
        Assert.Empty(h.Recent);
    }

    [Fact]
    public void ForVenue_average_is_null_when_all_tagged_sings_are_unrated()
    {
        var songs = new List<SongListItem> { Song("Solo", Perf(VenueA, 0, 1), Perf(VenueA, 0, 2)) };

        var h = VenueHistory.ForVenue(songs, VenueA);

        Assert.Equal(2, h.Sings);
        Assert.Null(h.Average);
        Assert.Empty(h.GoTo);   // no rated sing → no go-to
    }
}
