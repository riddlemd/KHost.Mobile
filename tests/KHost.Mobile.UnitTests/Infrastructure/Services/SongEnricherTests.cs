using KHost.Mobile.Abstractions.Clients.CoverArt;
using KHost.Mobile.Abstractions.Clients.Matching;
using KHost.Mobile.Abstractions.Clients.Metadata;
using KHost.Mobile.Abstractions.Services;
using KHost.Mobile.Infrastructure.Services;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Services;

// Each source records whether it was called: "didn't ask" is the assertion for most of these, because a
// redundant call spends a rate limit the song only gets one shot at.
public class SongEnricherTests
{
    private static readonly TrackMetadata Match =
        new("Africa", "Toto", 1982, "Rock", "https://cdn/cover.jpg");

    private static readonly TrackSuggestion Suggestion = new("Africa", "Toto");

    private static SongLookupState Song(string? genre = null, int? year = null, bool lookedUp = false)
        => new("Afrika", "Toto", genre, year, lookedUp);

    private static (SongEnricher Enricher, FakeMetadata Meta, FakeArt Art, FakeSpelling Spelling) Build(
        TrackLookupResult? lookup = null,
        string? deezerArt = null,
        TrackSuggestion? deezerSuggestion = null,
        bool autoFill = true,
        bool albumArt = true,
        int spellingLevel = 1)
    {
        var meta = new FakeMetadata(lookup ?? TrackLookupResult.None);
        var art = new FakeArt(deezerArt);
        var spelling = new FakeSpelling(deezerSuggestion);
        var settings = new FakeAppSettings
        {
            AutoFillMetadata = autoFill,
            AlbumArtEnabled = albumArt,
            SpellingSuggestionLevel = spellingLevel,
        };
        return (new SongEnricher(meta, art, spelling, settings), meta, art, spelling);
    }

    [Fact]
    public void ShouldEnrich_is_false_when_auto_fill_is_switched_off()
    {
        var (enricher, _, _, _) = Build(autoFill: false);

        Assert.False(enricher.ShouldEnrich("Africa", "Toto", metadataLookedUp: false));
    }

    [Fact]
    public void ShouldEnrich_is_false_once_the_song_has_been_looked_up()
    {
        // The one-shot rule: a rate-limited lookup must never be re-spent.
        var (enricher, _, _, _) = Build();

        Assert.False(enricher.ShouldEnrich("Africa", "Toto", metadataLookedUp: true));
    }

    [Theory]
    [InlineData(null, "Toto")]
    [InlineData("", "Toto")]
    [InlineData("   ", "Toto")]
    [InlineData("Africa", null)]
    [InlineData("Africa", "  ")]
    public void ShouldEnrich_is_false_without_both_a_title_and_an_artist(string? title, string? artist)
    {
        var (enricher, _, _, _) = Build();

        Assert.False(enricher.ShouldEnrich(title, artist, metadataLookedUp: false));
    }

    [Fact]
    public void ShouldEnrich_is_true_even_when_genre_and_year_are_already_filled()
    {
        // Complete metadata can still carry a wrong title, which is what the lookup also checks for.
        var (enricher, _, _, _) = Build();

        Assert.True(enricher.ShouldEnrich("Africa", "Toto", metadataLookedUp: false));
    }

    [Fact]
    public async Task EnrichAsync_asks_nobody_when_the_song_does_not_qualify()
    {
        var (enricher, meta, art, spelling) = Build();

        Assert.Null(await enricher.EnrichAsync(Song(lookedUp: true)));

        Assert.False(meta.Called);
        Assert.False(art.Called);
        Assert.False(spelling.Called);
    }

    [Fact]
    public async Task Fills_genre_and_year_from_the_match_when_the_song_has_neither()
    {
        var (enricher, _, _, _) = Build(new TrackLookupResult(Match, null));

        var result = await enricher.EnrichAsync(Song());

        Assert.Equal("Rock", result!.Genre);
        Assert.Equal(1982, result.Year);
        Assert.Equal(["Genre", "Year"], result.FilledFields);
    }

    [Fact]
    public async Task Never_overwrites_a_genre_or_year_the_song_already_has()
    {
        var (enricher, _, _, _) = Build(new TrackLookupResult(Match, null));

        var result = await enricher.EnrichAsync(Song(genre: "Pop", year: 1999));

        Assert.Null(result!.Genre);          // null means "leave the song's own alone"
        Assert.Null(result.Year);
        Assert.Empty(result.FilledFields);
    }

    [Fact]
    public async Task A_result_with_nothing_to_fill_still_ends_the_lookup()
    {
        // A non-null result is what stamps MetadataLookedUp; null would re-look-up this song on every open.
        var (enricher, _, _, _) = Build(new TrackLookupResult(Match, null));

        var result = await enricher.EnrichAsync(Song(genre: "Pop", year: 1999));

        Assert.NotNull(result);
        Assert.Empty(result!.FilledFields);
    }

    [Fact]
    public async Task Does_not_apply_the_input_state_back_onto_the_result()
    {
        var (enricher, _, _, _) = Build(TrackLookupResult.None);
        var song = Song(genre: "Pop", year: 1999);

        var result = await enricher.EnrichAsync(song);

        Assert.Null(result!.Genre);
        Assert.Null(result.Year);
        Assert.Equal("Pop", song.Genre);   // the request record is untouched
        Assert.Equal(1999, song.Year);
    }

    [Fact]
    public async Task Does_not_ask_Deezer_for_art_when_the_match_already_carried_a_cover()
    {
        var (enricher, _, art, _) = Build(new TrackLookupResult(Match, null), deezerArt: "https://cdn/deezer.jpg");

        var result = await enricher.EnrichAsync(Song());

        Assert.False(art.Called);
        Assert.Equal("https://cdn/cover.jpg", result!.ArtworkUrl);   // iTunes' own cover
    }

    [Fact]
    public async Task Falls_back_to_Deezer_for_art_when_the_match_had_no_cover()
    {
        var coverless = new TrackMetadata("Africa", "Toto", 1982, "Rock", null);
        var (enricher, _, art, _) = Build(new TrackLookupResult(coverless, null), deezerArt: "https://cdn/deezer.jpg");

        var result = await enricher.EnrichAsync(Song());

        Assert.True(art.Called);
        Assert.Equal("https://cdn/deezer.jpg", result!.ArtworkUrl);
    }

    [Fact]
    public async Task Does_not_ask_Deezer_for_art_when_album_art_is_switched_off()
    {
        var coverless = new TrackMetadata("Africa", "Toto", 1982, "Rock", null);
        var (enricher, _, art, _) = Build(new TrackLookupResult(coverless, null), deezerArt: "https://cdn/deezer.jpg", albumArt: false);

        var result = await enricher.EnrichAsync(Song());

        Assert.False(art.Called);
        Assert.Null(result!.ArtworkUrl);
    }

    [Fact]
    public async Task A_Deezer_fallback_supplies_art_and_never_a_year_or_genre()
    {
        // Deezer's release_date is the digital-availability date, so year/genre stay with iTunes.
        var (enricher, _, art, _) = Build(TrackLookupResult.None, deezerArt: "https://cdn/deezer.jpg");

        var result = await enricher.EnrichAsync(Song());

        Assert.True(art.Called);
        Assert.Equal("https://cdn/deezer.jpg", result!.ArtworkUrl);
        Assert.Null(result.Genre);
        Assert.Null(result.Year);
        Assert.Empty(result.FilledFields);
    }

    [Fact]
    public async Task A_transient_cover_failure_leaves_the_art_worth_retrying()
    {
        var coverless = new TrackMetadata("Africa", "Toto", 1982, "Rock", null);
        var (enricher, _, _, _) = Build(new TrackLookupResult(coverless, null));
        var failing = new SongEnricher(
            new FakeMetadata(new TrackLookupResult(coverless, null)),
            new FakeArt(throws: new CoverArtLookupException("Deezer is down")),
            new FakeSpelling(null),
            new FakeAppSettings { AutoFillMetadata = true, AlbumArtEnabled = true, SpellingSuggestionLevel = 1 });

        var result = await failing.EnrichAsync(Song());

        Assert.NotNull(result);
        Assert.False(result!.ArtworkLookedUp);   // art alone retries; the metadata attempt is still done
        Assert.Equal("Rock", result.Genre);      // and what iTunes did answer is kept
    }

    [Fact]
    public async Task A_completed_lookup_marks_the_art_as_checked()
    {
        var (enricher, _, _, _) = Build(new TrackLookupResult(Match, null));

        var result = await enricher.EnrichAsync(Song());

        Assert.True(result!.ArtworkLookedUp);
    }

    [Fact]
    public async Task Does_not_ask_Deezer_for_a_spelling_when_iTunes_matched()
    {
        var (enricher, _, _, spelling) = Build(new TrackLookupResult(Match, null));

        var result = await enricher.EnrichAsync(Song());

        Assert.False(spelling.Called);
        Assert.Null(result!.Suggestion);
    }

    [Fact]
    public async Task Does_not_ask_Deezer_for_a_spelling_when_iTunes_matched_but_no_cover_turned_up_anywhere()
    {
        // With no cover from either source, the art clause can't be what holds the extra call back.
        var coverless = new TrackMetadata("Africa", "Toto", 1982, "Rock", null);
        var (enricher, _, art, spelling) = Build(new TrackLookupResult(coverless, null), deezerArt: null);

        var result = await enricher.EnrichAsync(Song());

        Assert.True(art.Called);        // art WAS looked for...
        Assert.False(spelling.Called);  // ...found none, and still no spelling call
        Assert.Null(result!.Suggestion);
    }

    [Fact]
    public async Task Does_not_ask_Deezer_for_a_spelling_when_iTunes_already_offered_a_near_miss()
    {
        // The near-miss came free with the call already made; a second one would spend a rate limit for nothing.
        var (enricher, _, _, spelling) = Build(new TrackLookupResult(null, Suggestion));

        var result = await enricher.EnrichAsync(Song());

        Assert.False(spelling.Called);
        Assert.Equal(Suggestion, result!.Suggestion);
    }

    [Fact]
    public async Task Does_not_ask_Deezer_for_a_spelling_when_its_cover_search_found_the_song()
    {
        // A cover found by exact title+artist proves the spelling is already right.
        var (enricher, _, _, spelling) = Build(TrackLookupResult.None, deezerArt: "https://cdn/deezer.jpg");

        var result = await enricher.EnrichAsync(Song());

        Assert.False(spelling.Called);
        Assert.Null(result!.Suggestion);
    }

    [Fact]
    public async Task Asks_Deezer_for_a_spelling_only_when_nothing_else_answered()
    {
        var (enricher, _, art, spelling) = Build(TrackLookupResult.None, deezerSuggestion: Suggestion);

        var result = await enricher.EnrichAsync(Song());

        Assert.True(art.Called);        // art was tried first and came back empty...
        Assert.True(spelling.Called);   // ...only then is the extra call spent
        Assert.Equal(Suggestion, result!.Suggestion);
    }

    [Fact]
    public async Task Suggestion_level_zero_drops_the_suggestion_without_skipping_the_lookup()
    {
        // Auto-fill still wants the year/genre/art; only the correction is suppressed.
        var (enricher, meta, _, spelling) = Build(new TrackLookupResult(null, Suggestion), spellingLevel: 0);

        var result = await enricher.EnrichAsync(Song());

        Assert.True(meta.Called);
        Assert.False(spelling.Called);
        Assert.Null(result!.Suggestion);
    }

    [Fact]
    public async Task A_null_suggestion_is_returned_so_a_stale_correction_gets_cleared()
    {
        // A match now has to take away a suggestion left by an earlier lookup.
        var (enricher, _, _, _) = Build(new TrackLookupResult(Match, null));

        var result = await enricher.EnrichAsync(Song());

        Assert.NotNull(result);
        Assert.Null(result!.Suggestion);
    }

    [Fact]
    public async Task A_failed_metadata_lookup_returns_nothing_so_the_song_is_retried()
    {
        var enricher = new SongEnricher(
            new FakeMetadata(throws: new MetadataLookupException("rate limited")),
            new FakeArt(null),
            new FakeSpelling(null),
            new FakeAppSettings { AutoFillMetadata = true, AlbumArtEnabled = true, SpellingSuggestionLevel = 1 });

        Assert.Null(await enricher.EnrichAsync(Song()));
    }

    [Fact]
    public async Task A_failed_metadata_lookup_does_not_fall_through_to_Deezer()
    {
        var art = new FakeArt("https://cdn/deezer.jpg");
        var spelling = new FakeSpelling(Suggestion);
        var enricher = new SongEnricher(
            new FakeMetadata(throws: new MetadataLookupException("rate limited")),
            art, spelling,
            new FakeAppSettings { AutoFillMetadata = true, AlbumArtEnabled = true, SpellingSuggestionLevel = 1 });

        await enricher.EnrichAsync(Song());

        Assert.False(art.Called);
        Assert.False(spelling.Called);
    }

    [Fact]
    public async Task Cancellation_landing_during_the_lookup_surfaces_rather_than_being_applied()
    {
        // The source answers normally and the cancel lands mid-flight, so only the enricher's own re-check can
        // catch it — otherwise the caller stamps the one-shot flag on a lookup that never finished.
        using var cts = new CancellationTokenSource();
        var enricher = new SongEnricher(
            new FakeMetadata(new TrackLookupResult(Match, null), onCalled: cts.Cancel),
            new FakeArt(null),
            new FakeSpelling(null),
            new FakeAppSettings { AutoFillMetadata = true, AlbumArtEnabled = true, SpellingSuggestionLevel = 1 });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enricher.EnrichAsync(Song(), cts.Token));
    }

    [Fact]
    public async Task A_cancelled_token_stops_the_lookup_before_anything_is_decided()
    {
        var (enricher, _, art, spelling) = Build(new TrackLookupResult(Match, null));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enricher.EnrichAsync(Song(), cts.Token));

        Assert.False(art.Called);
        Assert.False(spelling.Called);
    }

    [Fact]
    public async Task Cancellation_during_the_cover_fallback_is_not_swallowed_as_no_cover_found()
    {
        var coverless = new TrackMetadata("Africa", "Toto", 1982, "Rock", null);
        var enricher = new SongEnricher(
            new FakeMetadata(new TrackLookupResult(coverless, null)),
            new FakeArt(throws: new OperationCanceledException()),
            new FakeSpelling(null),
            new FakeAppSettings { AutoFillMetadata = true, AlbumArtEnabled = true, SpellingSuggestionLevel = 1 });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enricher.EnrichAsync(Song()));
    }

    [Fact]
    public async Task Cancellation_landing_during_the_spelling_call_discards_the_result()
    {
        // The last call is still a call: what came back must not be applied, nor the one-shot flag stamped.
        using var cts = new CancellationTokenSource();
        var enricher = new SongEnricher(
            new FakeMetadata(TrackLookupResult.None),
            new FakeArt(null),
            new FakeSpelling(Suggestion, onCalled: cts.Cancel),
            new FakeAppSettings { AutoFillMetadata = true, AlbumArtEnabled = true, SpellingSuggestionLevel = 1 });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enricher.EnrichAsync(Song(), cts.Token));
    }

    [Fact]
    public async Task The_match_travels_back_for_the_auto_filled_note()
    {
        var (enricher, _, _, _) = Build(new TrackLookupResult(Match, null));

        var result = await enricher.EnrichAsync(Song());

        Assert.Equal("Africa", result!.Match!.MatchedTitle);
        Assert.Equal("Toto", result.Match.MatchedArtist);
    }

    // Deliberately ignores the token: a fake that threw on its own would answer the question under test.
    private sealed class FakeMetadata(TrackLookupResult? result = null, Exception? throws = null, Action? onCalled = null)
        : ITrackMetadataLookup
    {
        public bool Called { get; private set; }

        public Task<TrackLookupResult> LookupAsync(string title, string artist, CancellationToken cancellationToken = default)
        {
            Called = true;
            onCalled?.Invoke();
            return throws is not null
                ? Task.FromException<TrackLookupResult>(throws)
                : Task.FromResult(result ?? TrackLookupResult.None);
        }
    }

    private sealed class FakeArt(string? url = null, Exception? throws = null) : ICoverArtLookup
    {
        public bool Called { get; private set; }

        public Task<string?> FindCoverArtUrlAsync(string title, string artist, CancellationToken cancellationToken = default)
        {
            Called = true;
            return throws is not null ? Task.FromException<string?>(throws) : Task.FromResult(url);
        }
    }

    private sealed class FakeSpelling(TrackSuggestion? suggestion, Action? onCalled = null) : ISpellingSuggestionLookup
    {
        public bool Called { get; private set; }

        public Task<TrackSuggestion?> SuggestAsync(string title, string artist, CancellationToken cancellationToken = default)
        {
            Called = true;
            onCalled?.Invoke();
            return Task.FromResult(suggestion);
        }
    }
}
