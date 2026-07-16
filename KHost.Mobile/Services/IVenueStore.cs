using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// The patron's on-device list of karaoke venues. UI binds to this interface only; today it's backed by a local
/// JSON file, and a future implementation could sync to KHost.Online behind the same contract. <see cref="Changed"/>
/// drives UI refresh, matching the other stores.
/// </summary>
public interface IVenueStore
{
    /// <summary>Raised after any mutation so components can refresh. Fired on the caller's context.</summary>
    event EventHandler? Changed;

    /// <summary>All saved venues, favorites first then by name (case-insensitive).</summary>
    Task<IReadOnlyList<Venue>> GetAllAsync();

    /// <summary>The venue with this id, or null if it isn't saved.</summary>
    Task<Venue?> GetAsync(Guid id);

    /// <summary>Persist a new venue. A blank <see cref="Venue.Id"/> is assigned a fresh GUID; the stored (possibly
    /// id-stamped) venue is returned. <see cref="Venue.Name"/> is expected to be non-blank (the form enforces it).</summary>
    Task<Venue> AddAsync(Venue venue);

    /// <summary>Persist edits to an existing venue (matched by <see cref="Venue.Id"/>). No-op if it isn't present.</summary>
    Task UpdateAsync(Venue venue);

    /// <summary>Remove a venue by id. No-op if it isn't present. Does not touch performances tagged with it — those
    /// keep the id and simply become untagged from any existing venue (an orphan reference).</summary>
    Task RemoveAsync(Guid id);
}
