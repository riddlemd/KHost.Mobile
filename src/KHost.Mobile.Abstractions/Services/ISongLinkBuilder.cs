namespace KHost.Mobile.Abstractions.Services;

/// <summary>Builds the outbound search links a song offers — YouTube, Spotify, and KaraFun at a venue.</summary>
public interface ISongLinkBuilder
{
    /// <summary>YouTube search URL for "Title Artist".</summary>
    string YouTubeUrlFor(string title, string? artist);

    /// <summary>Spotify search URL for "Title Artist".</summary>
    string SpotifyUrlFor(string title, string? artist);

    /// <summary>
    /// KaraFun search URL for "Title Artist" under a venue. KaraFun's catalogue search lives under a venue, so
    /// the venue ID is part of the path — unlike YouTube and Spotify.
    /// </summary>
    string KaraFunUrlFor(string venueId, string title, string? artist);

    /// <summary>A venue's KaraFun catalogue home — the whole songbook, with no song query.</summary>
    string KaraFunCatalogUrlFor(string venueId);
}
