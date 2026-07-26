namespace KHost.Mobile.Clients.Lyrics;

/// <summary>
/// A lyrics lookup hit a network or HTTP failure. "No lyrics found" is NOT an exception — the lookup
/// returns null for that. The message is written to be shown directly in the UI.
/// </summary>
public sealed class LyricsLookupException(string message, Exception? innerException = null)
    : Exception(message, innerException);
