using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using KHost.Mobile.Abstractions.Services;

namespace KHost.Mobile.Infrastructure.Services;

/// <summary>
/// The app's date/time patterns in one place, so the 12/24-hour setting reaches every surface at once.
/// </summary>
internal sealed class TimeFormatter(ILogger<TimeFormatter>? logger = null) : ITimeFormatter
{
    // Held for future diagnostics: the seam should exist before it's needed, not be retrofitted.
    private readonly ILogger _log = logger ?? NullLogger<TimeFormatter>.Instance;

    /// <inheritdoc />
    public string DatePattern => "MMM d, yyyy";

    /// <inheritdoc />
    public string ShortDatePattern => "MMM d";

    /// <inheritdoc />
    public string DateTimePattern(bool use24Hour) =>
        use24Hour ? "MMM d, yyyy · HH:mm" : "MMM d, yyyy · h:mm tt";
}
