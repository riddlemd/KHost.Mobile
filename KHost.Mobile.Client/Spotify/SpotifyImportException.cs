namespace KHost.Mobile.Client.Spotify;

/// <summary>
/// An import failure worth surfacing to the user — a bad link, a network problem, an HTTP
/// error, or an unrecognized page (e.g. Spotify changed its embed shape). The message is
/// written to be shown directly in the UI.
/// </summary>
public sealed class SpotifyImportException(string message, Exception? innerException = null)
    : Exception(message, innerException);
