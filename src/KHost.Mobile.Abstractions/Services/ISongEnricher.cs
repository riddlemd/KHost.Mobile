using KHost.Mobile.Abstractions.Clients.Matching;
using KHost.Mobile.Abstractions.Clients.Metadata;

namespace KHost.Mobile.Abstractions.Services;

/// <summary>
/// Decides what a song's one-shot catalogue lookup should fill in, and in what order the sources are asked —
/// policy over the capability interfaces rather than anything a single backend knows.
/// </summary>
/// <remarks>
/// Returns what to apply rather than writing it: that is what keeps a lookup abandoned half-way from leaving a
/// partly-filled song behind.
/// </remarks>
public interface ISongEnricher
{
    /// <summary>
    /// Whether a lookup is worth running at all: auto-fill is on, this song hasn't been looked up before, and
    /// there is both a title and an artist to search on. Pure — no network.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT gated on genre/year being blank — complete metadata can still carry a wrong title.
    /// </remarks>
    bool ShouldEnrich(string? title, string? artist, bool metadataLookedUp);

    /// <summary>
    /// Runs the lookup and returns what to apply, or <c>null</c> when nothing should be applied — the song
    /// didn't qualify, or the lookup failed transiently and should be retried later. A non-null result always
    /// means the metadata attempt is done, so the caller stamps <c>MetadataLookedUp</c>.
    /// </summary>
    /// <param name="song">The song's current searchable state. Never modified.</param>
    /// <param name="cancellationToken">
    /// Cancelling abandons the lookup: it surfaces as an <see cref="OperationCanceledException"/> rather than a
    /// null result, so a caller can tell "the user navigated away" from "there is genuinely no match".
    /// </param>
    Task<SongEnrichment?> EnrichAsync(SongLookupState song, CancellationToken cancellationToken = default);
}

/// <summary>What a song currently holds, as far as the lookup is concerned.</summary>
/// <param name="Title">The title to search on.</param>
/// <param name="Artist">The artist to search on.</param>
/// <param name="Genre">The current genre, or null/blank when unset — a set one is never overwritten.</param>
/// <param name="Year">The current year, or null when unset — a set one is never overwritten.</param>
/// <param name="MetadataLookedUp">Whether this song's one-shot lookup has already been spent.</param>
public sealed record SongLookupState(
    string Title,
    string Artist,
    string? Genre = null,
    int? Year = null,
    bool MetadataLookedUp = false);

/// <summary>
/// The outcome to apply to the song. A null <see cref="Genre"/>, <see cref="Year"/> or <see cref="ArtworkUrl"/>
/// means "leave what's there alone".
/// </summary>
/// <param name="Genre">A genre to fill in, or null to leave the song's own.</param>
/// <param name="Year">A year to fill in, or null to leave the song's own.</param>
/// <param name="ArtworkUrl">A cover to fill in, or null when neither source had one.</param>
/// <param name="ArtworkLookedUp">
/// False only when the cover-art fallback failed transiently, so the art alone is worth retrying — the metadata
/// attempt itself is always done once a result exists.
/// </param>
/// <param name="Suggestion">
/// The spelling to offer, or null to CLEAR any suggestion the song was carrying — a lookup that now matches
/// must take the stale correction away.
/// </param>
/// <param name="Match">The catalogue entry the fills came from, for the caller's "auto-filled" note. Null when nothing matched.</param>
/// <param name="FilledFields">Which fields this actually fills, in display order — empty when the lookup filled nothing.</param>
public sealed record SongEnrichment(
    string? Genre,
    int? Year,
    string? ArtworkUrl,
    bool ArtworkLookedUp,
    TrackSuggestion? Suggestion,
    TrackMetadata? Match,
    IReadOnlyList<string> FilledFields);
