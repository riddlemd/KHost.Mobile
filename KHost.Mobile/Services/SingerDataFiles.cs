namespace KHost.Mobile.Services;

/// <summary>
/// Central naming for the per-singer JSON data files, so the per-singer stores (which read/write them by active
/// singer) and <see cref="JsonFileSingerStore"/> (which migrates the legacy files in and deletes a removed
/// singer's files) all agree on the exact file names. A singer's files live alongside the shared stores in the app
/// data directory, suffixed with the singer's id; the legacy single-user names (<c>song-list.json</c> /
/// <c>tonight.json</c>) are what those files were called before multi-singer support and are what a store falls
/// back to when no singer is active yet.
/// </summary>
internal static class SingerDataFiles
{
    /// <summary>The single-user song-list file name, from before multi-singer support. Migrated into the first
    /// seeded singer's file; also the fallback a store reads when no singer is active.</summary>
    public const string LegacySongList = "song-list.json";

    /// <summary>The single-user tonight file name, from before multi-singer support. See <see cref="LegacySongList"/>.</summary>
    public const string LegacyTonight = "tonight.json";

    /// <summary>The song-list file name for a specific singer.</summary>
    public static string SongList(Guid singerId) => $"song-list-{singerId:N}.json";

    /// <summary>The tonight-set file name for a specific singer.</summary>
    public static string Tonight(Guid singerId) => $"tonight-{singerId:N}.json";
}
