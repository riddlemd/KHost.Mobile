namespace KHost.Mobile.Abstractions.Services;

/// <summary>
/// Converts between an instant and the text an <c>&lt;input type="datetime-local"&gt;</c> reads and writes. The
/// control carries no time zone, so the offset comes from the app's clock.
/// </summary>
public interface IDateTimeInputConverter
{
    /// <summary>Renders an instant as local wall-clock text for the control.</summary>
    string Format(DateTimeOffset value);

    /// <summary>
    /// Parses the control's text back to an instant. The offset is the local zone's offset <em>for the date
    /// entered</em>, not for today — so backfilling across a daylight-saving change keeps the time that was typed.
    /// </summary>
    bool TryParse(string? text, out DateTimeOffset value);
}
