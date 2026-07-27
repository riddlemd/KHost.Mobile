using System.Text.Json.Serialization;
using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// System.Text.Json source-generation context for the persisted Tonight set — keeps (de)serialization trimming-/
/// AOT-friendly on the MAUI heads, matching <see cref="SongListJsonContext"/>. <c>WriteIndented</c> so the on-disk
/// file stays human-readable.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<TonightEntry>))]
public partial class TonightJsonContext : JsonSerializerContext;
