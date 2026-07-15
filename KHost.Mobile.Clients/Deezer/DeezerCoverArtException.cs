namespace KHost.Mobile.Clients.Deezer;

/// <summary>
/// A Deezer cover-art lookup failed for a reason worth surfacing — a network problem or an HTTP error.
/// A simple "no cover found" is NOT an exception; the lookup returns null for that. Deezer is only ever a
/// best-effort fallback behind iTunes, so callers are expected to swallow this and carry on without art.
/// </summary>
public sealed class DeezerCoverArtException(string message, Exception? innerException = null)
    : Exception(message, innerException);
