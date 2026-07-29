using KHost.Mobile.Abstractions.Services;
namespace KHost.Mobile.Infrastructure.Services;

/// <summary>
/// Central naming for the per-singer JSON data files, so the per-singer stores and
/// <see cref="JsonFileSingerStore"/> (which deletes a removed singer's files) agree on the exact names. A singer's
/// files sit in the app data directory suffixed with their id; the unsuffixed names are what a store reads when no
/// singer is active — before the roster is seeded, and on the session-less test path.
/// </summary>
internal sealed class SingerFileNames : ISingerFileNames
{
    /// <summary>Song list read when no singer is active.</summary>
    public const string DefaultSongList = "song-list.json";

    /// <summary>Tonight set read when no singer is active. See <see cref="DefaultSongList"/>.</summary>
    public const string DefaultTonight = "tonight.json";

    /// <summary>The song-list file name for a specific singer.</summary>
    public string SongList(Guid singerId) => $"song-list-{singerId:N}.json";

    /// <summary>The tonight-set file name for a specific singer.</summary>
    public string Tonight(Guid singerId) => $"tonight-{singerId:N}.json";
}
