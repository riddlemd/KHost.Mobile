using KHost.Mobile.Abstractions.Clients.CoverArt;
using KHost.Mobile.Abstractions.Clients.Lyrics;
using KHost.Mobile.Abstractions.Clients.Metadata;
using KHost.Mobile.Abstractions.Clients.Spotify;
using KHost.Mobile.Abstractions.Clients.Updates;
using KHost.Mobile.Abstractions.Clients.YouTubeMusic;
using KHost.Mobile.Clients.Apple;
using KHost.Mobile.Clients.Deezer;
using KHost.Mobile.Clients.GitHub;
using KHost.Mobile.Clients.LrcLib;
using KHost.Mobile.Clients.Spotify;
using KHost.Mobile.Clients.YouTubeMusic;
using Microsoft.Extensions.DependencyInjection;

namespace KHost.Mobile.Clients;

/// <summary>
/// Registers every vendor backend. The implementations are <c>internal</c>, so a caller names only the
/// capability interface and never learns which vendor answers it.
/// </summary>
public static class Project
{
    /// <summary>
    /// Adds the metadata, cover-art, spelling, lyrics, update and playlist-import clients as typed
    /// <see cref="HttpClient"/>s.
    /// </summary>
    /// <param name="userAgent">
    /// Identifies this app to LRCLIB (whose fair-use policy asks for it) and to GitHub (which rejects a request
    /// without one). Passed in because the library can't read the app's version itself.
    /// </param>
    /// <param name="configureHandler">
    /// Hook for the host to chain its own logging handler onto every client — the library does no logging.
    /// </param>
    public static IServiceCollection AddKHostClients(
        this IServiceCollection services,
        string userAgent,
        Action<IHttpClientBuilder>? configureHandler = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);

        void Add(IHttpClientBuilder builder) => configureHandler?.Invoke(builder);

        Add(services.AddHttpClient<IYouTubeMusicImportService, YouTubeMusicImportService>());
        Add(services.AddHttpClient<ISpotifyImportService, SpotifyImportService>());
        Add(services.AddHttpClient<ITrackMetadataLookup, ITunesTrackMetadataLookup>());
        Add(services.AddHttpClient<ICoverArtLookup, DeezerCoverArtLookup>());
        Add(services.AddHttpClient<ISpellingSuggestionLookup, DeezerSpellingSuggestionLookup>());

        Add(services.AddHttpClient<ILyricsLookup, LrcLibLyricsClient>(http =>
        {
            http.BaseAddress = new Uri("https://lrclib.net/");
            http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        }));

        Add(services.AddHttpClient<IUpdateLookup, GitHubReleaseClient>(http =>
        {
            http.BaseAddress = new Uri("https://api.github.com/");
            http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }));

        return services;
    }
}
