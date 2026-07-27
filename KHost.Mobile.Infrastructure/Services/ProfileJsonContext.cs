using System.Text.Json.Serialization;
using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// System.Text.Json source-generation context for the exported <see cref="SingerProfile"/> — keeps the file
/// round-trip trimming-/AOT-friendly on the MAUI heads, matching the store contexts. The source generator pulls
/// in the referenced <see cref="Singer"/> / <see cref="SongListItem"/> / <see cref="Performance"/> graph.
/// <c>WriteIndented</c> so an exported profile is human-readable.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SingerProfile))]
public partial class ProfileJsonContext : JsonSerializerContext;
