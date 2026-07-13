using KHost.Mobile.Clients.Updates;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class GitHubReleaseParserTests
{
    [Theory]
    [InlineData("v0.4.0", "0.4.0")]
    [InlineData("V1.2.3", "1.2.3")]
    [InlineData("0.6.0", "0.6.0")]
    [InlineData("v0.4.0-beta.1", "0.4.0")]      // prerelease suffix stripped
    [InlineData("1.0.0+build.42", "1.0.0")]      // build metadata stripped
    [InlineData("  v2.0.0  ", "2.0.0")]          // trimmed
    public void TryParseVersion_strips_prefix_and_suffix(string tag, string expectedClean)
    {
        Assert.True(GitHubReleaseParser.TryParseVersion(tag, out var version, out var clean));
        Assert.Equal(expectedClean, clean);
        Assert.Equal(Version.Parse(expectedClean), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nightly")]
    [InlineData("v")]
    [InlineData("vlatest")]
    [InlineData("release-2024")]
    public void TryParseVersion_returns_false_on_non_numeric(string tag)
    {
        Assert.False(GitHubReleaseParser.TryParseVersion(tag, out var version, out var clean));
        Assert.Equal(new Version(0, 0), version);
        Assert.Equal(string.Empty, clean);
    }

    [Fact]
    public void ParseNewest_picks_the_highest_version_regardless_of_feed_order()
    {
        // Deliberately out of order to prove it sorts by parsed version, not array position.
        const string json = """
        [
          { "tag_name": "v0.5.0", "name": "0.5", "html_url": "https://example.com/0.5.0", "draft": false, "prerelease": true },
          { "tag_name": "v0.6.0", "name": "0.6", "html_url": "https://example.com/0.6.0", "draft": false, "prerelease": true },
          { "tag_name": "v0.4.0", "name": "0.4", "html_url": "https://example.com/0.4.0", "draft": false, "prerelease": false }
        ]
        """;

        var release = GitHubReleaseParser.ParseNewest(json);

        Assert.NotNull(release);
        Assert.Equal("0.6.0", release!.Version);
        Assert.Equal("https://example.com/0.6.0", release.HtmlUrl);
        Assert.True(release.IsPrerelease);
    }

    [Fact]
    public void ParseNewest_skips_drafts()
    {
        // The highest version is a draft and must be ignored; the next-highest non-draft wins.
        const string json = """
        [
          { "tag_name": "v9.9.9", "html_url": "https://example.com/draft", "draft": true, "prerelease": false },
          { "tag_name": "v1.0.0", "html_url": "https://example.com/1.0.0", "draft": false, "prerelease": false }
        ]
        """;

        var release = GitHubReleaseParser.ParseNewest(json);

        Assert.NotNull(release);
        Assert.Equal("1.0.0", release!.Version);
        Assert.False(release.IsPrerelease);
    }

    [Fact]
    public void ParseNewest_skips_entries_missing_a_url_tag_or_parseable_version()
    {
        const string json = """
        [
          { "tag_name": "v2.0.0", "draft": false },
          { "html_url": "https://example.com/notag", "draft": false },
          { "tag_name": "nightly", "html_url": "https://example.com/nightly", "draft": false },
          { "tag_name": "v1.5.0", "html_url": "https://example.com/1.5.0", "draft": false, "prerelease": false }
        ]
        """;

        var release = GitHubReleaseParser.ParseNewest(json);

        Assert.NotNull(release);
        Assert.Equal("1.5.0", release!.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{ \"tag_name\": \"v1.0.0\" }")]   // object, not the expected array
    [InlineData("[]")]                               // empty array
    [InlineData("[ { \"tag_name\": \"nightly\", \"html_url\": \"https://x\" } ]")]   // nothing parseable
    public void ParseNewest_returns_null_when_nothing_usable(string json)
    {
        Assert.Null(GitHubReleaseParser.ParseNewest(json));
    }
}
