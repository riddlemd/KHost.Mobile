using System.Text.Json.Serialization;
using KHost.Mobile.Models;

namespace KHost.Mobile.Services;

/// <summary>
/// System.Text.Json source-generation context for the persisted singer roster — keeps (de)serialization trimming-/
/// AOT-friendly on the MAUI heads, matching <see cref="VenueJsonContext"/>. <c>WriteIndented</c> so the on-disk
/// file stays human-readable.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<Singer>))]
public partial class SingerJsonContext : JsonSerializerContext;
