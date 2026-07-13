namespace KHost.Mobile.Clients.YouTubeMusic;

/// <summary>
/// A YouTube Music import failure worth surfacing to the user — a bad link, a network problem, an HTTP
/// error, or an unrecognized page (e.g. YouTube changed its page shape). The message is written to be
/// shown directly in the UI.
/// </summary>
public sealed class YouTubeMusicImportException(string message, Exception? innerException = null)
    : Exception(message, innerException);
