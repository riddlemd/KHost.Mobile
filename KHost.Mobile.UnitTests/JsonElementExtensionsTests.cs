using System.Text.Json;
using KHost.Mobile.Clients.Json;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class JsonElementExtensionsTests
{
    // Clone so the element survives the temporary JsonDocument being collected.
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    // --- Str ---

    [Fact]
    public void Str_returns_the_string_value()
        => Assert.Equal("hello", Parse("""{ "name": "hello" }""").Str("name"));

    [Fact]
    public void Str_returns_null_when_the_property_is_absent()
        => Assert.Null(Parse("""{ "name": "hello" }""").Str("missing"));

    [Theory]
    [InlineData("""{ "v": 42 }""")]       // number
    [InlineData("""{ "v": true }""")]     // bool
    [InlineData("""{ "v": null }""")]     // json null
    [InlineData("""{ "v": [1, 2] }""")]   // array
    [InlineData("""{ "v": { } }""")]      // object
    public void Str_returns_null_when_the_property_is_not_a_string(string json)
        => Assert.Null(Parse(json).Str("v"));

    [Fact]
    public void Str_returns_null_when_the_element_is_not_an_object()
    {
        Assert.Null(Parse("""[ "a", "b" ]""").Str("anything"));   // array
        Assert.Null(Parse("\"just a string\"").Str("anything"));  // scalar
        Assert.Null(default(JsonElement).Str("anything"));        // Undefined
    }

    // --- Bool ---

    [Fact]
    public void Bool_is_true_only_for_json_true()
        => Assert.True(Parse("""{ "b": true }""").Bool("b"));

    [Theory]
    [InlineData("""{ "b": false }""")]
    [InlineData("""{ "b": "true" }""")]   // string, not a bool
    [InlineData("""{ "b": 1 }""")]
    [InlineData("""{ "b": null }""")]
    [InlineData("{ }")]                    // absent
    public void Bool_is_false_for_anything_but_json_true(string json)
        => Assert.False(Parse(json).Bool("b"));

    [Fact]
    public void Bool_is_false_when_the_element_is_not_an_object()
    {
        Assert.False(Parse("[ true ]").Bool("b"));
        Assert.False(default(JsonElement).Bool("b"));
    }

    // --- Prop ---

    [Fact]
    public void Prop_returns_the_child_element()
    {
        var album = Parse("""{ "album": { "cover": "art.jpg" } }""").Prop("album");
        Assert.Equal(JsonValueKind.Object, album.ValueKind);
        Assert.Equal("art.jpg", album.Str("cover"));
    }

    [Fact]
    public void Prop_returns_undefined_when_the_property_is_absent()
        => Assert.Equal(JsonValueKind.Undefined, Parse("{ }").Prop("missing").ValueKind);

    [Fact]
    public void Prop_returns_undefined_when_the_element_is_not_an_object()
    {
        Assert.Equal(JsonValueKind.Undefined, Parse("[ 1 ]").Prop("x").ValueKind);
        Assert.Equal(JsonValueKind.Undefined, default(JsonElement).Prop("x").ValueKind);
    }

    [Fact]
    public void Prop_chains_degrade_gracefully_on_a_missing_hop()
    {
        // element.Prop("absent").Prop("b").Str("c") must not throw when a hop is missing.
        var el = Parse("""{ "present": { } }""");
        Assert.Null(el.Prop("absent").Prop("b").Str("c"));
    }
}
