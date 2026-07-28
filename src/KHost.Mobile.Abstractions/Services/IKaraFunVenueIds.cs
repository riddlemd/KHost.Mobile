namespace KHost.Mobile.Abstractions.Services;

/// <summary>Reads a KaraFun venue ID out of what a user pasted, or out of a scanned QR code.</summary>
public interface IKaraFunVenueIds
{
    /// <summary>Accepts a venue URL or a bare numeric ID.</summary>
    bool TryParseId(string? input, out string id);

    /// <summary>
    /// Accepts only an absolute KaraFun URL — a bare ID is rejected, and so are look-alike hosts. This is the
    /// scanned-QR path, where anything could be in front of the camera.
    /// </summary>
    bool TryParseVenueUrl(string? input, out string id);
}
