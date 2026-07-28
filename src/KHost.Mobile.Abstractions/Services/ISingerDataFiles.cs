namespace KHost.Mobile.Abstractions.Services;

/// <summary>
/// Names the per-singer data files. The GUID is written <b>dash-less</b> — a dashed name is silently ignored,
/// since nothing reads it.
/// </summary>
public interface ISingerDataFiles
{
    /// <summary>That singer's wishlist file.</summary>
    string SongList(Guid singerId);

    /// <summary>That singer's tonight-set file.</summary>
    string Tonight(Guid singerId);
}
