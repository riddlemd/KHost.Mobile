using KHost.Mobile.Infrastructure.Services;
using Xunit;

using KHost.Mobile.Abstractions.Models;

namespace KHost.Mobile.UnitTests.Infrastructure.Services;

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
    public void Detects_a_profile_and_rejects_everything_else()
    {
        var profileJson = Codec.Serialize(SampleProfile());
        Assert.Equal(ProfileFileKind.Profile, Codec.Detect(profileJson));

        // A bare array was the pre-profile export shape; it is not importable.
        Assert.Equal(ProfileFileKind.Invalid, Codec.Detect("""[{"Title":"X","Artist":"Y"}]"""));

        Assert.Equal(ProfileFileKind.Invalid, Codec.Detect("not json"));
        Assert.Equal(ProfileFileKind.Invalid, Codec.Detect("""{"foo":1}"""));
    }

    [Fact]
    public void ParseProfile_returns_null_for_a_non_profile()
    {
        Assert.Null(Codec.ParseProfile("not json"));
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

    [Fact]
    public void ParseVenues_returns_null_for_a_file_that_is_not_a_venue_export()
    {
        // The import screen shows "not a valid export" off a null; throwing instead surfaces as a crash.
        Assert.Null(Codec.ParseVenues("not json"));
        Assert.Null(Codec.ParseVenues("""{"not":"an array"}"""));
    }

    [Fact]
    public void Every_stored_field_of_a_song_survives_the_export_file()
    {
        // The profile IS the user's backup — a field dropped from the wire is data they can't get back.
        var singer = new Singer { Name = "Jordan" };
        var venue = Guid.NewGuid();
        var song = new SongListItem
        {
            Title = "Africa",
            Artist = "Toto",
            Genre = "Rock",
            Year = 1982,
            Notes = "key of B",
            Tags = ["closer", "crowd-pleaser"],
            IsFavorite = true,
            Enjoyment = 4,
            Status = SongListItemStatus.Sang,
            MetadataLookedUp = true,
            ArtworkLookedUp = true,
            Performances =
            [
                new Performance
                {
                    Date = new DateTimeOffset(2026, 3, 4, 20, 15, 0, TimeSpan.FromHours(-5)),
                    HowItWent = 5,
                    Note = "nailed it",
                    VenueId = venue,
                },
            ],
        };

        var parsed = Codec.ParseProfile(Codec.Serialize(SingerProfile.Create(singer, [song])));

        var restored = Assert.Single(parsed!.Songs);
        Assert.Equal("key of B", restored.Notes);
        Assert.Equal(["closer", "crowd-pleaser"], restored.Tags);
        Assert.Equal(4, restored.Enjoyment);
        Assert.True(restored.IsFavorite);
        Assert.Equal(SongListItemStatus.Sang, restored.Status);
        Assert.True(restored.MetadataLookedUp);
        Assert.True(restored.ArtworkLookedUp);

        var performance = Assert.Single(restored.Performances);
        Assert.Equal(song.Performances[0].Id, performance.Id);
        Assert.Equal(new DateTimeOffset(2026, 3, 4, 20, 15, 0, TimeSpan.FromHours(-5)), performance.Date);
        Assert.Equal(5, performance.HowItWent);
        Assert.Equal("nailed it", performance.Note);
        Assert.Equal(venue, performance.VenueId);   // the venue tag, so restored history still resolves
    }
}
