namespace KHost.Mobile.Client.Lyrics;

/// <summary>
/// A lyrics lookup failed for a reason worth telling the user about — a network problem or an HTTP error.
/// A plain "no lyrics found" is NOT an exception; the lookup returns null for that. The message is written
/// to be shown directly in the UI.
/// </summary>
public sealed class LyricsLookupException(string message, Exception? innerException = null)
    : Exception(message, innerException);
