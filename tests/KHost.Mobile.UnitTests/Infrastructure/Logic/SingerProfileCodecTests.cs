using KHost.Mobile.Infrastructure.Search;
using KHost.Mobile.Infrastructure.Services;
using Xunit;

using KHost.Mobile.Abstractions.Models;
using KHost.Mobile.Infrastructure.Logic;

namespace KHost.Mobile.UnitTests.Infrastructure.Logic;

public class SingerProfileCodecTests
{
    private static readonly SingerProfileCodec Codec = new();

    private static SingerProfile SampleProfile()
    {
        var singer = new Singer { Name = "Jordan", Color = "#0d9488", Glyph = "🎸" };
        var song = new SongListItem
        {
            Title = "Bohemian Rhapsody",
            Artist = "Queen",
            Genre = "Rock",
            Year = 1975,
            IsFavorite = true,
            Performances = [new Performance { HowItWent = 5 }, new Performance { HowItWent = 4 }],
        };
        return SingerProfile.Create(singer, [song]);
    }

    [Fact]
    public void Round_trips_a_profile_preserving_ids_and_history()
    {
        var original = SampleProfile();

        var json = Codec.Serialize(original);
        var parsed = Codec.ParseProfile(json);

        Assert.NotNull(parsed);
        Assert.Equal(SingerProfile.CurrentVersion, parsed!.Version);
        Assert.Equal(SingerProfile.Marker, parsed.App);
        Assert.Equal(original.Singer.Id, parsed.Singer.Id);          // ids preserved (true restore)
        Assert.Equal("Jordan", parsed.Singer.Name);
        Assert.Equal("🎸", parsed.Singer.Glyph);
        var song = Assert.Single(parsed.Songs);
        Assert.Equal(original.Songs[0].Id, song.Id);
        Assert.Equal("Bohemian Rhapsody", song.Title);
        Assert.Equal(2, song.Performances.Count);                    // sung history travels with the profile
    }

    [Fact]
    public void Detects_a_profile_a_legacy_song_array_and_garbage()
    {
        var profileJson = Codec.Serialize(SampleProfile());
        Assert.Equal(ProfileFileKind.Profile, Codec.Detect(profileJson));

        // A legacy songs-only export is a bare JSON array.
        Assert.Equal(ProfileFileKind.LegacySongList, Codec.Detect("""[{"Title":"X","Artist":"Y"}]"""));

        Assert.Equal(ProfileFileKind.Invalid, Codec.Detect("not json"));
        Assert.Equal(ProfileFileKind.Invalid, Codec.Detect("""{"foo":1}"""));
    }

    [Fact]
    public void ParseProfile_returns_null_for_a_non_profile()
    {
        Assert.Null(Codec.ParseProfile("not json"));
    }

    [Fact]
    public void ParseLegacySongs_reads_a_bare_array()
    {
        var songs = Codec.ParseLegacySongs("""[{"Title":"Africa","Artist":"Toto"}]""");
        Assert.NotNull(songs);
        Assert.Equal("Africa", Assert.Single(songs!).Title);
    }

    [Fact]
    public void Round_trips_a_venue_list()
    {
        List<Venue> venues =
        [
            new() { Name = "The Mint", Glyph = "🎤", KaraFunVenueId = "012345", IsFavorite = true },
            new() { Name = "Lucky Strike", Glyph = "🎳" },
        ];

        var json = Codec.SerializeVenues(venues);
        var parsed = Codec.ParseVenues(json);

        Assert.NotNull(parsed);
        Assert.Equal(2, parsed!.Count);
        Assert.Equal(venues[0].Id, parsed[0].Id);                 // venue ids preserved so history keeps resolving
        Assert.Equal("The Mint", parsed[0].Name);
        Assert.Equal("012345", parsed[0].KaraFunVenueId);
    }
}
