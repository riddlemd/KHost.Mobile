using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using KHost.Mobile.Infrastructure.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using KHost.Mobile.Abstractions.Models;
using KHost.Mobile.Abstractions.Services;
using KHost.Mobile.Infrastructure.Logic;

namespace KHost.Mobile.Infrastructure.Services;

/// <inheritdoc />
/// <remarks>
/// Backed by a single JSON file in the app's private data directory. The in-memory list is the source of truth once
/// loaded; every mutation rewrites the file, under a <see cref="SemaphoreSlim"/> so concurrent UI actions can't
/// corrupt either. A corrupt file is quarantined and treated as an empty list.
/// </remarks>
internal sealed class JsonFileSongListStore : JsonFileStore<SongListItem>, ISongListStore
{
    private readonly ISingerFileNames _names;

    private readonly IAppDataDirectory _paths;
    private readonly IAppSession? _session;


    /// <summary>
    /// The song list is per-singer: it reads/writes the active singer's file (<see cref="IAppSession.ActiveSingerId"/>).
    /// <paramref name="session"/> and <paramref name="logger"/> are optional so the integration tests can <c>new</c>
    /// the store bare; with no session it falls back to the single legacy file. DI supplies both.
    /// </summary>
    public JsonFileSongListStore(IAppDataDirectory paths, IAppSession? session = null, ILogger<JsonFileSongListStore>? logger = null, IAtomicFileWriter? writer = null,
        ISingerFileNames? names = null)
        : base(logger ?? NullLogger<JsonFileSongListStore>.Instance, writer)
    {
        _names = names ?? new SingerFileNames();
        _paths = paths;
        _session = session;
        if (_session is not null)
            _session.ActiveSingerChanged += OnActiveSingerChanged;
    }

    // A singer switch invalidates the cache (see LoadAsync's _loadedFor check) and must refresh every subscriber, so
    // re-raise Changed — the UI then reloads this singer's list exactly as it would after any mutation.
    private void OnActiveSingerChanged(object? sender, EventArgs e) => RaiseChanged();

    // The given singer's song-list file, or the legacy single-user file when no singer is active (pre-seed, or the
    // session-less test path). Takes the singer explicitly — LoadAsync captures ActiveSingerId ONCE and SaveAsync
    // writes to the singer the data was LOADED for, so a singer switch landing mid-operation (between LoadAsync's
    // await and the save) can't write one singer's list into another singer's file.
    protected override JsonTypeInfo<List<SongListItem>> TypeInfo => SongListJsonContext.Default.ListSongListItem;
    protected override string Label => "Song list";
    protected override Guid? CurrentKey => _session?.ActiveSingerId;

    protected override string PathFor(Guid? singerId)
    {
        var name = singerId is null ? SingerFileNames.LegacySongList : _names.SongList(singerId.Value);
        return Path.Combine(_paths.AppDataDirectory, name);
    }

    public async Task<IReadOnlyList<SongListItem>> GetAllAsync()
    {
        await Gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            return items.OrderByDescending(i => i.AddedAt).ToList();
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<SongListItem> AddAsync(string title, string artist, string? notes = null, string? genre = null, int? year = null)
    {
        var item = new SongListItem
        {
            Title = title.Trim(),
            Artist = artist.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Genre = string.IsNullOrWhiteSpace(genre) ? null : genre.Trim(),
            Year = year,
            Status = SongListItemStatus.WantToSing,   // new songs start on the wishlist; sung later from the detail sheet
        };

        await Gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            items.Add(item);
            await SaveAsync(items);
        }
        finally
        {
            Gate.Release();
        }

        RaiseChanged();
        return item;
    }

    public async Task UpdateAsync(SongListItem item)
    {
        await Gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            var index = items.FindIndex(i => i.Id == item.Id);
            if (index < 0)
                return;

            items[index] = item;
            await SaveAsync(items);
        }
        finally
        {
            Gate.Release();
        }

        RaiseChanged();
    }

    public async Task UpdateRangeAsync(IEnumerable<SongListItem> incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var changed = false;
        await Gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            foreach (var item in incoming)
            {
                var index = items.FindIndex(i => i.Id == item.Id);
                if (index < 0)
                    continue;

                items[index] = item;
                changed = true;
            }

            if (changed)
                await SaveAsync(items);
        }
        finally
        {
            Gate.Release();
        }

        if (changed)
            RaiseChanged();
    }

    public async Task RemoveAsync(Guid id)
    {
        var changed = false;
        await Gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            if (items.RemoveAll(i => i.Id == id) == 0)
                return;

            changed = true;
            await SaveAsync(items);
        }
        finally
        {
            Gate.Release();
        }

        if (changed)
            RaiseChanged();
    }

    public async Task ClearAsync()
    {
        var changed = false;
        await Gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            if (items.Count == 0)
                return;

            items.Clear();
            changed = true;
            await SaveAsync(items);
        }
        finally
        {
            Gate.Release();
        }

        if (changed)
            RaiseChanged();
    }

    public async Task RestoreAsync(SongListItem item)
    {
        await Gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            if (items.Any(i => i.Id == item.Id))
                return;   // already present — e.g. a double Undo

            // Undo of a removal: re-add the captured copy in its original position by AddedAt ordering.
            items.Add(item);
            await SaveAsync(items);
        }
        finally
        {
            Gate.Release();
        }

        RaiseChanged();
    }

    public async Task<int> ImportAsync(IEnumerable<SongListItem> incoming, bool skipDuplicates = true)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var added = 0;
        await Gate.WaitAsync();
        try
        {
            var items = await LoadAsync();

            // Seed the dedupe set from the existing list. Add() also catches repeats within the batch.
            var seen = skipDuplicates
                ? new HashSet<string>(items.Select(DedupeKey), StringComparer.OrdinalIgnoreCase)
                : null;

            foreach (var item in incoming)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.Title))
                    continue;

                item.Title = item.Title.Trim();
                item.Artist = item.Artist.Trim();

                if (seen is not null && !seen.Add(DedupeKey(item)))
                    continue;   // already in the list, or a duplicate earlier in this batch

                MigrateToPerformances(item);   // fold a legacy-format import (old Cue export) into Performances
                items.Add(item);
                added++;
            }

            if (added > 0)
                await SaveAsync(items);
        }
        finally
        {
            Gate.Release();
        }

        Log.LogInformation("Song list import: added {Added} songs (skipDuplicates={SkipDuplicates})", added, skipDuplicates);
        if (added > 0)
            RaiseChanged();

        return added;
    }

    public async Task<int> MergeByIdAsync(IEnumerable<SongListItem> incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var written = 0;
        await Gate.WaitAsync();
        try
        {
            var items = await LoadAsync();
            foreach (var item in incoming)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.Title))
                    continue;

                MigrateToPerformances(item);   // fold a legacy-format profile into Performances if needed
                var index = items.FindIndex(i => i.Id == item.Id);
                if (index < 0)
                    items.Add(item);            // new id → append
                else
                    items[index] = item;        // existing id → replace verbatim (restore)
                written++;
            }

            if (written > 0)
                await SaveAsync(items);
        }
        finally
        {
            Gate.Release();
        }

        Log.LogInformation("Song list profile-restore: wrote {Written} songs by id", written);
        if (written > 0)
            RaiseChanged();

        return written;
    }

    // Title+artist identity used for de-duplication (trimmed, case-insensitive). The  unit
    // separator keeps "AB"+"C" distinct from "A"+"BC". Mirrors the add-form duplicate guard in MySongs.
    private static string DedupeKey(SongListItem item)
        => $"{item.Title.Trim()}{item.Artist.Trim()}";

    // The one-time migration from the pre-per-performance shape (SungDates + a single Confidence). The base
    // persists the result when this returns true, so later launches read the already-migrated file.
    protected override Task<bool> OnLoadedAsync(List<SongListItem> items)
    {
        var migrated = false;
        foreach (var item in items)
            migrated |= MigrateToPerformances(item);
        return Task.FromResult(migrated);
    }

    private static bool MigrateToPerformances(SongListItem item)
    {
        if (item.Performances.Count > 0)
            return false;
        if (item.SungDates.Count == 0 && item.Confidence == 0)
            return false;

        var rating = Math.Clamp(item.Confidence, 0, 5);
        if (item.SungDates.Count > 0)
        {
            foreach (var date in item.SungDates)
                item.Performances.Add(new Performance { Date = date, HowItWent = rating });
        }
        else
        {
            // Rated with no recorded date (shouldn't normally happen) — anchor a single performance at AddedAt.
            item.Performances.Add(new Performance { Date = item.AddedAt, HowItWent = rating });
        }

        item.Status = item.Performances.Count > 0 ? SongListItemStatus.Sang : SongListItemStatus.WantToSing;
        item.SungDates = [];
        item.Confidence = 0;
        return true;
    }
}
