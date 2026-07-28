using KHost.Mobile.Clients.GitHub;
using Xunit;

namespace KHost.Mobile.UnitTests.Clients.GitHub;

public class VersionTagTests
{
    [Theory]
    [InlineData("v0.4.0", "0.4.0")]
    [InlineData("V1.2.3", "1.2.3")]
    [InlineData("0.6.0", "0.6.0")]
    [InlineData("v0.4.0-beta.1", "0.4.0")]      // prerelease suffix stripped
    [InlineData("1.0.0+build.42", "1.0.0")]      // build metadata stripped
    [InlineData("  v2.0.0  ", "2.0.0")]          // trimmed
    public void TryParse_strips_prefix_and_suffix(string tag, string expected)
    {
        Assert.True(VersionTag.TryParse(tag, out var version));
        Assert.Equal(Version.Parse(expected), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nightly")]
    [InlineData("v")]
    [InlineData("vlatest")]
    [InlineData("release-2024")]
    public void TryParse_returns_false_on_non_numeric(string tag)
    {
        Assert.False(VersionTag.TryParse(tag, out var version));
        Assert.Null(version);
    }

    // The app shows this string in the update banner, so a round-trip that renamed the version would be visible.
    [Theory]
    [InlineData("v0.4.0", "0.4.0")]
    [InlineData("v1.2", "1.2")]
    [InlineData("v1.2.3.4", "1.2.3.4")]
    public void TryParse_round_trips_to_the_same_text(string tag, string expected)
    {
        Assert.True(VersionTag.TryParse(tag, out var version));
        Assert.Equal(expected, version.ToString());
    }
}
