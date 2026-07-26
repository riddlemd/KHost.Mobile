namespace KHost.Mobile.Clients.Enrichment;

/// <summary>
/// A metadata lookup hit a network or HTTP failure. "No match" is NOT an exception — the lookup returns
/// <see cref="TrackLookupResult.None"/> for that. The message is written to be shown directly in the UI.
/// </summary>
public sealed class MetadataLookupException(string message, Exception? innerException = null)
    : Exception(message, innerException);
