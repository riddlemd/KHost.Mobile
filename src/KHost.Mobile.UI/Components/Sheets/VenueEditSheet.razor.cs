namespace KHost.Mobile.UI.Components.Sheets;

public sealed partial class VenueEditSheet
{
    /// <summary>The venue being added/edited. Null hides the sheet. The host passes a fresh <see cref="Venue"/> to
    /// add or a working copy to edit; on save its fields are written and it's handed back via <see cref="OnSave"/>.</summary>
    [Parameter] public Venue? Editing { get; set; }

    /// <summary>Whether this is a brand-new venue (drives the title).</summary>
    [Parameter] public bool IsNew { get; set; }

    /// <summary>Raised with the populated venue when the user saves (name guaranteed non-blank).</summary>
    [Parameter] public EventCallback<Venue> OnSave { get; set; }

    /// <summary>Raised when the sheet is dismissed without saving (✕ / Cancel / backdrop).</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    private string _glyph = VenueGlyphs.Default;
    private string _name = string.Empty;
    private string _karaFunId = string.Empty;
    private string _notes = string.Empty;
    private bool _isFavorite;
    private bool _showInSwitcher = true;
    private double? _lat;
    private double? _lng;
    private bool _capturing;
    private string? _locError;
    private Guid? _seededFor;   // the venue id the buffers belong to; a change means a fresh open → reseed
    private bool _scanning;
    private string? _scanError;

    // Hide the scan button where there's no scanner to open.
    private bool ScanSupported => Scanner.IsSupported;

    // Clearing _seededFor on close matters as much as the id check: the component stays mounted while hidden, so
    // without it, reopening the SAME venue keeps the last open's buffers and abandoned edits look saved.
    protected override void OnParametersSet()
    {
        if (Editing is null)
        {
            _seededFor = null;
            return;
        }

        if (Editing is { } v && v.Id != _seededFor)
        {
            _seededFor = v.Id;
            _glyph = string.IsNullOrWhiteSpace(v.Glyph) ? VenueGlyphs.Default : v.Glyph;
            _name = v.Name;
            _karaFunId = v.KaraFunVenueId ?? string.Empty;
            _notes = v.Notes ?? string.Empty;
            _isFavorite = v.IsFavorite;
            _showInSwitcher = v.ShowInSwitcher;
            _lat = v.Latitude;
            _lng = v.Longitude;
            _scanError = null;
            _locError = null;
        }
    }

    private async Task CaptureLocationAsync()
    {
        if (_capturing)
            return;
        _capturing = true;
        _locError = null;
        try
        {
            var here = await Geo.GetCurrentAsync();
            if (here is null)
                _locError = "Couldn't get your location. Check that location is on and permission is granted.";
            else
                (_lat, _lng) = (here.Latitude, here.Longitude);
        }
        finally
        {
            _capturing = false;
        }
    }

    private void ClearLocation() => (_lat, _lng) = (null, null);

    // Parsed with the strict host check, not the loose paste parser, so an arbitrary scanned string can't silently
    // become a venue ID.
    private async Task ScanAsync()
    {
        if (_scanning)
            return;
        _scanning = true;
        _scanError = null;
        try
        {
            var scanned = await Scanner.ScanQrCodeAsync();
            if (string.IsNullOrWhiteSpace(scanned))
                return; // cancelled, permission denied, or nothing read

            if (VenueUrls.TryParseVenueUrl(scanned, out var id))
                _karaFunId = id;
            else
                _scanError = "That QR code isn't a KaraFun venue link.";
        }
        finally
        {
            _scanning = false;
        }
    }

    private async Task SaveAsync()
    {
        if (Editing is not { } v || string.IsNullOrWhiteSpace(_name))
            return;

        v.Glyph = string.IsNullOrWhiteSpace(_glyph) ? VenueGlyphs.Default : _glyph.Trim();
        v.Name = _name.Trim();
        // Keep only a parseable venue ID (accepts a pasted link or a bare id); blank clears it.
        v.KaraFunVenueId = VenueUrls.TryParseId(_karaFunId, out var parsed) ? parsed : null;
        v.Notes = string.IsNullOrWhiteSpace(_notes) ? null : _notes.Trim();
        v.IsFavorite = _isFavorite;
        v.ShowInSwitcher = _showInSwitcher;
        v.Latitude = _lat;
        v.Longitude = _lng;

        await OnSave.InvokeAsync(v);
    }
}
