using System.Text.Json.Serialization;
using KHost.Mobile.Infrastructure.Models;
using KHost.Mobile.Abstractions.Models;

using KHost.Mobile.Abstractions.Services;
namespace KHost.Mobile.Infrastructure.Serialization;

/// <summary>
/// System.Text.Json source-generation context for the persisted venue list — keeps (de)serialization trimming-/
/// AOT-friendly on the MAUI heads, matching <see cref="TonightJsonContext"/>. <c>WriteIndented</c> so the on-disk
/// file stays human-readable.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<Venue>))]
public partial class VenueJsonContext : JsonSerializerContext;
