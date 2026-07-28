using KHost.Mobile.Infrastructure.Services;
using Xunit;

using KHost.Mobile.Infrastructure.Logic;

namespace KHost.Mobile.IntegrationTests.Infrastructure.Logic;

// Pins the per-singer file-naming contract directly. The store tests assert against these names via repeated
// string literals; this is the one place the convention itself is the subject, so a drift in SingerFileNames
// fails HERE with an obvious message rather than as a mysterious missing-file failure elsewhere.
public class SingerFileNamesTests
{
    private static readonly SingerFileNames Names = new();

    [Fact]
    public void Per_singer_names_use_the_dashless_guid_suffix()
    {
        var id = new Guid("79da6549-5e33-4883-959c-d52c44161ce0");

        Assert.Equal("song-list-79da65495e334883959cd52c44161ce0.json", Names.SongList(id));
        Assert.Equal("tonight-79da65495e334883959cd52c44161ce0.json", Names.Tonight(id));
    }

    [Fact]
    public void Legacy_single_user_names_are_stable()
    {
        // These exact names are what pre-multi-singer installs wrote and what the seed migration looks for.
        Assert.Equal("song-list.json", SingerFileNames.LegacySongList);
        Assert.Equal("tonight.json", SingerFileNames.LegacyTonight);
    }
}
