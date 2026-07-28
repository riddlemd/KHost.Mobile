using KHost.Mobile.Infrastructure.Services;
using Xunit;

namespace KHost.Mobile.UnitTests.Infrastructure.Services;

// The app's own ApplicationDisplayVersion, parsed for comparison against the newest release. A failure here
// is silent in the app — MauiAppUpdateService just reports "no update" — so the boundaries are pinned.
public class AppVersionParserTests
{
    private static readonly AppVersionParser Parser = new();

    [Theory]
    [InlineData("0.13.0", "0.13.0")]
    [InlineData("1.2", "1.2")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    [InlineData("  0.13.0  ", "0.13.0")]           // trimmed
    public void Parses_a_plain_display_version(string input, string expected)
    {
        Assert.True(Parser.TryParse(input, out var v));
        Assert.Equal(Version.Parse(expected), v);
    }

    [Theory]
    [InlineData("0.14.0-beta", "0.14.0")]
    [InlineData("0.14.0-beta.2", "0.14.0")]
    [InlineData("1.0.0+build.42", "1.0.0")]
    [InlineData("2.0.0-rc.1+sha.abc", "2.0.0")]
    public void Tolerates_a_prerelease_or_build_suffix(string input, string expected)
    {
        // A suffixed versionName is ordinary on an Android dev build; refusing it would disable the update
        // check on exactly the builds most likely to want it.
        Assert.True(Parser.TryParse(input, out var v));
        Assert.Equal(Version.Parse(expected), v);
    }

    [Fact]
    public void Does_not_strip_a_leading_v()
    {
        // "v1.2.3" is a git-tag convention, not a display version — iOS requires CFBundleShortVersionString
        // to be period-separated digits, so this is invalid input rather than something to accommodate.
        Assert.False(Parser.TryParse("v1.2.3", out var v));
        Assert.Null(v);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("beta")]
    [InlineData("-1.2.3")]      // suffix cut leaves nothing
    public void Rejects_what_isnt_a_dotted_numeric_version(string? input)
    {
        Assert.False(Parser.TryParse(input, out var v));
        Assert.Null(v);
    }

    [Fact]
    public void A_suffixed_build_still_compares_against_a_release()
    {
        // The point of tolerating the suffix: 0.14.0-beta must not read as older than 0.13.0.
        Assert.True(Parser.TryParse("0.14.0-beta", out var current));
        Assert.True(current > Version.Parse("0.13.0"));
        Assert.False(current > Version.Parse("0.14.0"));   // same release, not newer
    }
}
