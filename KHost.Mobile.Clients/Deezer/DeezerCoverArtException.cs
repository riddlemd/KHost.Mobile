namespace KHost.Mobile.Clients.Deezer;

/// <summary>
/// A Deezer cover-art lookup hit a network or HTTP failure. "No cover found" is NOT an exception — the
/// lookup returns null for that.
/// </summary>
public sealed class DeezerCoverArtException(string message, Exception? innerException = null)
    : Exception(message, innerException);
