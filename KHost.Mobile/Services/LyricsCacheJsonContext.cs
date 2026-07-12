using System.Text.Json.Serialization;
using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// System.Text.Json source-generation context for the persisted lyrics cache — keeps (de)serialization
/// trimming-/AOT-friendly on the MAUI heads, matching <see cref="SongListJsonContext"/>. <c>WriteIndented</c>
/// so the on-disk file stays human-readable.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<LyricsCacheEntry>))]
public partial class LyricsCacheJsonContext : JsonSerializerContext;
