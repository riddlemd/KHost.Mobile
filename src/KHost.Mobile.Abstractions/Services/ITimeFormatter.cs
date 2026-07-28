namespace KHost.Mobile.Abstractions.Services;

/// <summary>The app's date/time patterns in one place, so the 12/24-hour setting reaches every surface at once.</summary>
public interface ITimeFormatter
{
    /// <summary>Date without a time of day.</summary>
    string DatePattern { get; }

    /// <summary>Compact date for stat tiles and list metadata.</summary>
    string ShortDatePattern { get; }

    /// <summary>Date plus time of day, honouring <see cref="IAppSettings.Use24HourTime"/>.</summary>
    string DateTimePattern(bool use24Hour);
}
