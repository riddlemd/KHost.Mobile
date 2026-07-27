using KHost.Mobile.Abstractions.Services;

namespace KHost.Mobile.Infrastructure.Logic;

/// <summary>
/// The app's date/time patterns in one place, so the 12/24-hour setting reaches every surface at once.
/// </summary>
public static class TimeFormat
{
    /// <summary>Date without a time of day.</summary>
    public const string Date = "MMM d, yyyy";

    /// <summary>Compact date for stat tiles and list metadata.</summary>
    public const string ShortDate = "MMM d";

    /// <summary>Date plus time of day, honouring <see cref="IAppSettings.Use24HourTime"/>.</summary>
    public static string DateTimePattern(bool use24Hour) =>
        use24Hour ? "MMM d, yyyy · HH:mm" : "MMM d, yyyy · h:mm tt";
}
