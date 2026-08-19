using KHost.Mobile.Abstractions.Clients.CoverArt;
using KHost.Mobile.Abstractions.Clients.Matching;
using KHost.Mobile.Abstractions.Clients.Metadata;
using KHost.Mobile.Abstractions.Models;
using KHost.Mobile.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KHost.Mobile.Infrastructure.Services;

/// <inheritdoc />
/// <remarks>Each extra source costs a call against a rate limit the song only gets one shot at.</remarks>
// logger is optional so a test can `new` the enricher without a logging stack; DI supplies the real one.
internal sealed class SongEnricher(
    ITrackMetadataLookup metadata,
    ICoverArtLookup artFallback,
    ISpellingSuggestionLookup spelling,
    IAppSettings settings,
    ILogger<SongEnricher>? logger = null) : ISongEnricher
{
    private readonly ILogger _log = logger ?? NullLogger<SongEnricher>.Instance;

    /// <inheritdoc />
    // Artist is required: the parser rejects an artist-less result, so a call without one can only come back empty.
    public bool ShouldEnrich(string? title, string? artist, bool metadataLookedUp) =>
        settings.AutoFillMetadata &&
        !metadataLookedUp &&
        !string.IsNullOrWhiteSpace(title) &&
        !string.IsNullOrWhiteSpace(artist);

    /// <inheritdoc />
    public async Task<SongEnrichment?> EnrichAsync(SongLookupState song, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(song);

        if (!ShouldEnrich(song.Title, song.Artist, song.MetadataLookedUp))
            return null;

        _log.LogDebug("Auto-fill lookup start: “{Title}” — “{Artist}”", song.Title, song.Artist);

        TrackLookupResult lookup;
        try
        {
            lookup = await metadata.LookupAsync(song.Title, song.Artist, cancellationToken).ConfigureAwait(false);
        }
        catch (MetadataLookupException ex)
        {
            // network/rate-limit failure — no result, so the caller leaves the flag unset and retries later
            _log.LogWarning(ex, "Auto-fill lookup failed for “{Title}” — “{Artist}”; will retry later", song.Title, song.Artist);
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var meta = lookup.Match;
        _log.LogDebug("Auto-fill lookup done: “{Title}” — “{Artist}” → matched {Matched}, year={Year}, genre={Genre}, cover={HasCover}",
            song.Title, song.Artist,
            meta is null ? "(no match)" : $"“{meta.MatchedTitle} — {meta.MatchedArtist}”",
            meta?.Year, meta?.Genre, meta?.ArtworkUrl is not null);

        var filled = new List<string>();
        string? genre = null;
        if (string.IsNullOrWhiteSpace(song.Genre) && Genres.Map(meta?.Genre) is string mapped)
        {
            genre = mapped;
            filled.Add("Genre");
        }

        int? year = null;
        if (!song.Year.HasValue && meta?.Year is int matchedYear)
        {
            year = matchedYear;
            filled.Add("Year");
        }

        // The iTunes match carries the cover for free — capture it so enabling album art later is instant. When
        // iTunes has none and album art is on, fall back to Deezer (art only — never its unreliable year/genre).
        var artUrl = meta?.ArtworkUrl;
        var artLookedUp = true;
        if (artUrl is null && settings.AlbumArtEnabled)
        {
            try
            {
                artUrl = await artFallback.FindCoverArtUrlAsync(song.Title, song.Artist, cancellationToken).ConfigureAwait(false);
                _log.LogDebug("iTunes had no cover for “{Title}” — “{Artist}”; Deezer fallback → {Result}",
                    song.Title, song.Artist, artUrl is null ? "no cover found" : "cover found");
            }
            catch (CoverArtLookupException ex)
            {
                artLookedUp = false;   // transient Deezer failure — leave art unflagged so it retries later
                _log.LogWarning(ex, "Deezer cover fallback failed for “{Title}” — “{Artist}”; will retry later", song.Title, song.Artist);
            }
        }

        // iTunes' near-miss came free with the call already made; Deezer costs an extra one, so it's asked only
        // when nothing else answered — a cover found by exact title+artist proves the spelling is already right.
        // Level 0 drops the suggestion without skipping the lookup: auto-fill still wants the year/genre/art.
        var suggestion = settings.SpellingSuggestionLevel == 0 ? null : lookup.Suggestion;
        if (settings.SpellingSuggestionLevel > 0 && suggestion is null && meta is null && artUrl is null)
        {
            suggestion = await spelling.SuggestAsync(song.Title, song.Artist, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (suggestion is { } offered)
            _log.LogInformation("No match for “{Title}” — “{Artist}”; {Source} suggests “{SuggestedTitle}” — “{SuggestedArtist}”",
                song.Title, song.Artist, lookup.Suggestion is null ? "Deezer" : "iTunes", offered.Title, offered.Artist);

        return new SongEnrichment(genre, year, artUrl, artLookedUp, suggestion, meta, filled);
    }
}
