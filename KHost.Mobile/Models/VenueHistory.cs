namespace KHost.Mobile.Models;

/// <summary>One song's tally at a venue: how many times sung there and the average "how it went" of the rated sings.</summary>
public sealed record VenueSongStat(string Title, int Count, double Average);

/// <summary>A single sing logged at a venue — the song, its "how it went" (0 = unrated), and when.</summary>
public sealed record VenueSing(string Title, int Rating, DateTimeOffset Date);

/// <summary>
/// A venue's derived history — everything the venue detail shows, computed purely from the performances tagged with
/// that venue (<see cref="Performance.VenueId"/>). No stored state beyond the tag; kept out of the Blazor component
/// so the aggregation is unit-testable.
/// </summary>
public sealed record VenueHistory(
    int Sings,
    double? Average,
    DateTimeOffset? LastSung,
    IReadOnlyList<VenueSongStat> GoTo,
    IReadOnlyList<VenueSing> Recent)
{
    /// <summary>An empty history — no sings logged at the venue yet.</summary>
    public static VenueHistory Empty { get; } = new(0, null, null, [], []);

    /// <summary>
    /// Roll up every performance tagged with <paramref name="venueId"/> across <paramref name="songs"/>:
    /// total sings, the average rating there (over rated sings only, null when none are rated), the last-sung date,
    /// the top <paramref name="topGoTo"/> go-to songs (by average rating then play count, rated songs only), and the
    /// most recent <paramref name="recent"/> sings (newest first).
    /// </summary>
    public static VenueHistory ForVenue(IEnumerable<SongListItem> songs, Guid venueId, int topGoTo = 3, int recent = 5)
    {
        ArgumentNullException.ThrowIfNull(songs);

        var here = songs
            .SelectMany(s => s.Performances.Where(p => p.VenueId == venueId).Select(p => (Song: s, Perf: p)))
            .ToList();

        if (here.Count == 0)
            return Empty;

        var rated = here.Where(x => x.Perf.HowItWent >= 1).Select(x => x.Perf.HowItWent).ToList();

        var goTo = here
            .GroupBy(x => x.Song)
            .Select(g => new VenueSongStat(
                g.Key.Title,
                g.Count(),
                g.Where(x => x.Perf.HowItWent >= 1).Select(x => x.Perf.HowItWent).DefaultIfEmpty(0).Average()))
            .Where(x => x.Average >= 1)   // a song with no rated sing here isn't a "go-to"
            .OrderByDescending(x => x.Average).ThenByDescending(x => x.Count)
            .Take(topGoTo)
            .ToList();

        var recentSings = here
            .OrderByDescending(x => x.Perf.Date)
            .Take(recent)
            .Select(x => new VenueSing(x.Song.Title, x.Perf.HowItWent, x.Perf.Date))
            .ToList();

        return new VenueHistory(
            here.Count,
            rated.Count > 0 ? rated.Average() : null,
            here.Max(x => x.Perf.Date),
            goTo,
            recentSings);
    }
}
