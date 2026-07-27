using KHost.Mobile.Models;
using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Services;

public class SurprisePickerTests
{
    private static readonly DateTime Today = new(2026, 7, 25);

    private static SongListItem Song(
        string title = "song", bool favorite = false, DateTime? sungOn = null, int howItWent = 3)
    {
        var song = new SongListItem { Title = title, IsFavorite = favorite };
        if (sungOn is { } day)
            song.Performances.Add(new Performance { HowItWent = howItWent, Date = new DateTimeOffset(day) });
        return song;
    }

    private static SurpriseOptions Options(
        bool skipToday = false, bool favourWell = false, bool neverSung = false, bool favorites = false) =>
        new(skipToday, favourWell, neverSung, favorites);

    // ---- Narrow ----

    [Fact]
    public void Narrow_keeps_everything_when_no_restriction_is_on()
    {
        var pool = new[] { Song("a"), Song("b", favorite: true) };

        var result = SurprisePicker.Narrow(pool, Options(), Today);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Narrow_drops_songs_sung_today_but_keeps_ones_sung_earlier()
    {
        var pool = new[]
        {
            Song("sung today", sungOn: Today),
            Song("sung yesterday", sungOn: Today.AddDays(-1)),
            Song("never sung"),
        };

        var result = SurprisePicker.Narrow(pool, Options(skipToday: true), Today);

        Assert.Equal(["sung yesterday", "never sung"], result.Select(s => s.Title));
    }

    [Fact]
    public void Narrow_keeps_only_favorites()
    {
        var pool = new[] { Song("plain"), Song("starred", favorite: true) };

        var result = SurprisePicker.Narrow(pool, Options(favorites: true), Today);

        Assert.Equal("starred", Assert.Single(result).Title);
    }

    [Fact]
    public void Narrow_keeps_only_songs_with_no_history()
    {
        var pool = new[] { Song("fresh"), Song("sung", sungOn: Today.AddDays(-30)) };

        var result = SurprisePicker.Narrow(pool, Options(neverSung: true), Today);

        Assert.Equal("fresh", Assert.Single(result).Title);
    }

    [Fact]
    public void Narrow_combines_restrictions()
    {
        var pool = new[]
        {
            Song("fav sung today", favorite: true, sungOn: Today),
            Song("fav fresh", favorite: true),
            Song("plain fresh"),
        };

        var result = SurprisePicker.Narrow(pool, Options(skipToday: true, favorites: true), Today);

        Assert.Equal("fav fresh", Assert.Single(result).Title);
    }

    // A restriction that would empty the pool is dropped: a one-tap picker has nowhere to explain "no matches".

    [Fact]
    public void Narrow_ignores_skip_today_when_everything_was_sung_today()
    {
        var pool = new[] { Song("a", sungOn: Today), Song("b", sungOn: Today) };

        var result = SurprisePicker.Narrow(pool, Options(skipToday: true), Today);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Narrow_ignores_favorites_only_when_nothing_is_a_favorite()
    {
        var pool = new[] { Song("a"), Song("b") };

        var result = SurprisePicker.Narrow(pool, Options(favorites: true), Today);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Narrow_ignores_never_sung_when_everything_has_history()
    {
        var pool = new[] { Song("a", sungOn: Today.AddDays(-5)), Song("b", sungOn: Today.AddDays(-9)) };

        var result = SurprisePicker.Narrow(pool, Options(neverSung: true), Today);

        Assert.Equal(2, result.Count);
    }

    // ---- Pick ----

    [Fact]
    public void Pick_returns_null_for_an_empty_pool()
    {
        var pick = SurprisePicker.Pick([], Options(), 0.5, _ => 3, neutralStar: 3);

        Assert.Null(pick);
    }

    [Theory]
    [InlineData(0.0, "a")]
    [InlineData(0.34, "b")]
    [InlineData(0.99, "c")]
    public void Pick_unweighted_spreads_the_roll_evenly(double roll, string expected)
    {
        var pool = new[] { Song("a"), Song("b"), Song("c") };

        var pick = SurprisePicker.Pick(pool, Options(), roll, _ => 5, neutralStar: 3);

        Assert.Equal(expected, pick!.Title);
    }

    [Fact]
    public void Pick_weighted_gives_a_higher_rated_song_more_of_the_range()
    {
        var pool = new[] { Song("weak"), Song("strong") };
        var stars = (SongListItem s) => (double?)(s.Title == "strong" ? 5 : 1);

        // Weights 1 and 5 over a total of 6: the first sixth is "weak", the rest "strong".
        Assert.Equal("weak", SurprisePicker.Pick(pool, Options(favourWell: true), 0.10, stars, 3)!.Title);
        Assert.Equal("strong", SurprisePicker.Pick(pool, Options(favourWell: true), 0.20, stars, 3)!.Title);
        Assert.Equal("strong", SurprisePicker.Pick(pool, Options(favourWell: true), 0.99, stars, 3)!.Title);
    }

    [Fact]
    public void Pick_weighted_uses_the_neutral_star_for_unrated_songs()
    {
        var pool = new[] { Song("unrated"), Song("rated") };
        var stars = (SongListItem s) => s.Title == "rated" ? (double?)4 : null;

        // Unrated draws on the neutral 4, so the two are equally weighted and split the range evenly.
        Assert.Equal("unrated", SurprisePicker.Pick(pool, Options(favourWell: true), 0.25, stars, neutralStar: 4)!.Title);
        Assert.Equal("rated", SurprisePicker.Pick(pool, Options(favourWell: true), 0.75, stars, neutralStar: 4)!.Title);
    }

    [Fact]
    public void Pick_weighted_still_reaches_a_zero_rated_song_through_the_floor()
    {
        var pool = new[] { Song("zero"), Song("great") };
        var stars = (SongListItem s) => (double?)(s.Title == "great" ? 5 : 0);

        // The 0.25 floor over a 5.25 total leaves the zero-rated song a small but real slice.
        Assert.Equal("zero", SurprisePicker.Pick(pool, Options(favourWell: true), 0.01, stars, 3)!.Title);
    }

    [Fact]
    public void Pick_handles_a_roll_of_exactly_one()
    {
        var pool = new[] { Song("a"), Song("b") };

        var pick = SurprisePicker.Pick(pool, Options(favourWell: true), 1.0, _ => 3, neutralStar: 3);

        Assert.NotNull(pick);   // must not index past the end
    }
}
