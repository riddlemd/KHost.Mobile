using KHost.Mobile.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;

namespace KHost.Mobile.Infrastructure.Logic;

/// <summary>
/// Converts between an instant and the text an <c>&lt;input type="datetime-local"&gt;</c> reads and writes.
/// The control carries no time zone, so the offset comes from the supplied <see cref="TimeProvider"/>.
/// </summary>
internal sealed class DateTimeInput(TimeProvider? timeProvider = null, ILogger<DateTimeInput>? logger = null) : IDateTimeInput
{
    // Held for future diagnostics: the seam should exist before it's needed, not be retrofitted.
    private readonly ILogger _log = logger ?? NullLogger<DateTimeInput>.Instance;

    /// <summary>The only shape the control accepts, regardless of the device's display locale.</summary>
    public const string Pattern = "yyyy-MM-ddTHH:mm";

    /// <summary>Renders <paramref name="value"/> as local wall-clock text for the control.</summary>
    /// <inheritdoc />
    public string Format(DateTimeOffset value)
    {
        var clock = timeProvider ?? TimeProvider.System;
        return TimeZoneInfo.ConvertTime(value, clock.LocalTimeZone).ToString(Pattern, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses the control's text back to an instant.
    /// </summary>
    /// <remarks>
    /// The offset is the local zone's offset <em>for the date entered</em>, not for today — so backfilling a
    /// performance from the other side of a daylight-saving change keeps the wall-clock time that was typed.
    /// </remarks>
    /// <inheritdoc />
    public bool TryParse(string? text, out DateTimeOffset value)
    {
        var clock = timeProvider ?? TimeProvider.System;
        value = default;

        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var wall))
            return false;

        // GetUtcOffset reads an Unspecified DateTime as local time in that zone, which is exactly what the
        // control gives us; a parsed Local/Utc kind would make the DateTimeOffset constructor throw.
        wall = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
        value = new DateTimeOffset(wall, clock.LocalTimeZone.GetUtcOffset(wall));
        return true;
    }
}
