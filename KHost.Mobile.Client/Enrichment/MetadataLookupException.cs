namespace KHost.Mobile.Client.Enrichment;

/// <summary>
/// A metadata lookup failed for a reason worth telling the user about — a network problem or an HTTP
/// error. A simple "no match" is NOT an exception; the lookup returns null for that. The message is
/// written to be shown directly in the UI.
/// </summary>
public sealed class MetadataLookupException(string message, Exception? innerException = null)
    : Exception(message, innerException);
