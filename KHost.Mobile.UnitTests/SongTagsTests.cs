using KHost.Mobile.Models;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class SongTagsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#")]
    [InlineData("  #  ")]
    public void Clean_returns_null_for_blank_or_punctuation_only(string? input)
    {
        Assert.Null(SongTags.Clean(input));
    }

    [Theory]
    [InlineData("closer", "closer")]
    [InlineData("  closer  ", "closer")]        // trimmed
    [InlineData("#duet", "duet")]                // leading hash dropped
    [InlineData("# duet", "duet")]
    [InlineData("high   energy", "high energy")] // internal whitespace collapsed
    [InlineData("needs\tpractice", "needs practice")]
    public void Clean_trims_dehashes_and_collapses_whitespace(string input, string expected)
    {
        Assert.Equal(expected, SongTags.Clean(input));
    }

    [Fact]
    public void Clean_caps_length_at_MaxLength()
    {
        var input = new string('a', SongTags.MaxLength + 10);

        var cleaned = SongTags.Clean(input);

        Assert.NotNull(cleaned);
        Assert.Equal(SongTags.MaxLength, cleaned!.Length);
    }

    [Fact]
    public void Normalize_returns_empty_for_null()
    {
        Assert.Empty(SongTags.Normalize(null));
    }

    [Fact]
    public void Normalize_drops_blanks_and_trims_each()
    {
        var result = SongTags.Normalize(["  closer ", "", "   ", "duet"]);

        Assert.Equal(["closer", "duet"], result);
    }

    [Fact]
    public void Normalize_dedupes_case_insensitively_keeping_first_seen_casing()
    {
        var result = SongTags.Normalize(["Duet", "duet", "DUET", "Closer"]);

        Assert.Equal(["Duet", "Closer"], result);
    }

    [Fact]
    public void Normalize_preserves_order()
    {
        var result = SongTags.Normalize(["zebra", "apple", "mango"]);

        Assert.Equal(["zebra", "apple", "mango"], result);
    }

    [Fact]
    public void Normalize_caps_count_at_MaxCount()
    {
        var many = Enumerable.Range(0, SongTags.MaxCount + 5).Select(i => $"tag{i}").ToList();

        var result = SongTags.Normalize(many);

        Assert.Equal(SongTags.MaxCount, result.Count);
        Assert.Equal("tag0", result[0]);   // kept in order, extras past the cap dropped
    }

    [Fact]
    public void Normalize_counts_toward_the_cap_after_deduping()
    {
        // 20 raw entries but only 2 distinct — the cap applies to the deduped result, so both survive.
        var input = Enumerable.Range(0, 20).Select(i => i % 2 == 0 ? "a" : "b").ToList();

        var result = SongTags.Normalize(input);

        Assert.Equal(["a", "b"], result);
    }
}
