using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// The roster of singers who share this device. UI binds to this interface only; today it's backed by a local JSON
/// file, and a future implementation could sync behind the same contract. <see cref="Changed"/> drives UI refresh,
/// matching the other stores. Which singer is <em>active</em> (whose personal lists the app currently shows) is a
/// separate ephemeral pointer on <see cref="IAppSession.ActiveSingerId"/>, not stored here.
/// </summary>
public interface ISingerStore
{
    /// <summary>Raised after any mutation so components can refresh. Fired on the caller's context.</summary>
    event EventHandler? Changed;

    /// <summary>All singers in display order (<see cref="Singer.Order"/> then add time).</summary>
    Task<IReadOnlyList<Singer>> GetAllAsync();

    /// <summary>The singer with this id, or null if not present.</summary>
    Task<Singer?> GetAsync(Guid id);

    /// <summary>Persist a new singer. A blank <see cref="Singer.Id"/> is assigned a fresh GUID and the singer is
    /// appended (its <see cref="Singer.Order"/> set past the current max); the stored singer is returned.</summary>
    Task<Singer> AddAsync(Singer singer);

    /// <summary>Persist edits to an existing singer (matched by <see cref="Singer.Id"/>). No-op if not present.</summary>
    Task UpdateAsync(Singer singer);

    /// <summary>Remove a singer by id and delete their personal data files (song list + tonight set). No-op if not
    /// present. The caller is responsible for not removing the last singer and for re-pointing the active singer.</summary>
    Task RemoveAsync(Guid id);

    /// <summary>Ensure at least one singer exists: on a roster that's empty (a fresh install, or the first launch
    /// after this feature ships) create a default singer and migrate the legacy single-user song-list / tonight
    /// files into it, so an upgrader's existing list becomes that singer's list. Idempotent — a no-op once any
    /// singer exists. Returns the singer that should be active when nothing else is remembered (the first one).</summary>
    Task<Singer> EnsureSeededAsync();
}
