using System.Text.Json.Serialization;
using KHost.Mobile.Models;

namespace KHost.Mobile.Serialization;

/// <summary>
/// System.Text.Json source-generation context for the persisted / exported song list — generated metadata instead
/// of runtime reflection keeps (de)serialization trimming-/AOT-friendly on the MAUI heads. Shared by
/// <see cref="JsonFileSongListStore"/> (device storage) and the Import/Export page (file round-trip).
/// <c>WriteIndented</c> so the on-disk file stays human-readable.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<SongListItem>))]
[JsonSerializable(typeof(Performance))]
public partial class SongListJsonContext : JsonSerializerContext;
