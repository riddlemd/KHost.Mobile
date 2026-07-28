using KHost.Mobile.Abstractions.Services;
namespace KHost.Mobile.Infrastructure.Logic;

/// <summary>
/// Central naming for the per-singer JSON data files, so the per-singer stores and
/// <see cref="JsonFileSingerStore"/> (which migrates the legacy files in and deletes a removed singer's files) agree
/// on the exact names. A singer's files sit in the app data directory suffixed with their id; the legacy
/// single-user names are what a store falls back to when no singer is active yet.
/// </summary>
internal sealed class SingerFileNames : ISingerFileNames
{
    /// <summary>The single-user song-list file name, from before multi-singer support. Migrated into the first
    /// seeded singer's file; also the fallback a store reads when no singer is active.</summary>
    public const string LegacySongList = "song-list.json";

    /// <summary>The single-user tonight file name, from before multi-singer support. See <see cref="LegacySongList"/>.</summary>
    public const string LegacyTonight = "tonight.json";

    /// <summary>The song-list file name for a specific singer.</summary>
    public string SongList(Guid singerId) => $"song-list-{singerId:N}.json";

    /// <summary>The tonight-set file name for a specific singer.</summary>
    public string Tonight(Guid singerId) => $"tonight-{singerId:N}.json";
}
