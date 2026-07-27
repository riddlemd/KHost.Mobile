namespace KHost.Mobile.Abstractions.Clients.CoverArt;

/// <summary>
/// A cover-art lookup hit a network or HTTP failure. "No cover found" is NOT an exception — the
/// lookup returns null for that.
/// </summary>
public sealed class CoverArtLookupException(string message, Exception? innerException = null)
    : Exception(message, innerException);
