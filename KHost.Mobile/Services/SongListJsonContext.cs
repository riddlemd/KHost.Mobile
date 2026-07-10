using System.Text.Json.Serialization;
using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// System.Text.Json source-generation context for the persisted / exported song list. Using generated
/// metadata instead of runtime reflection keeps (de)serialization trimming- and AOT-friendly on the MAUI
/// heads, and removes the reflection cost for the one model we actually persist. Shared by
/// <see cref="JsonFileSongListStore"/> (device storage) and the Import/Export page (file round-trip).
/// <c>WriteIndented</c> mirrors the previous hand-written options so the on-disk format is unchanged.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<SongListItem>))]
public partial class SongListJsonContext : JsonSerializerContext;
