using System.Text.Json;

namespace KHost.Mobile.Clients.Json;

/// <summary>
/// Null-safe navigation helpers over <see cref="JsonElement"/>, shared by the client parsers. Every hop
/// tolerates a missing property or wrong value kind rather than throwing — a missing/wrong-kind read yields
/// <c>null</c> (<see cref="Str"/>), <c>false</c> (<see cref="Bool"/>), or <c>Undefined</c> (<see cref="Prop"/>),
/// so chained reads (<c>element.Prop("a").Prop("b").Str("c")</c>) degrade gracefully on unexpected shapes.
/// </summary>
internal static class JsonElementExtensions
{
    /// <summary>Returns the string value of <paramref name="propertyName"/>, or null when absent or not a string.</summary>
    public static string? Str(this JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Returns true only when <paramref name="propertyName"/> is present and JSON <c>true</c>; false otherwise.</summary>
    public static bool Bool(this JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var value)
           && value.ValueKind == JsonValueKind.True;

    /// <summary>Returns the child element at <paramref name="propertyName"/>, or a default (Undefined) element when absent.</summary>
    public static JsonElement Prop(this JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value)
            ? value
            : default;
}
