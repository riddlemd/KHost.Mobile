using KHost.Mobile.Models;
using KHost.Mobile.Services;
using Xunit;

namespace KHost.Mobile.UnitTests;

public class RatingScoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    // A song with one rated performance per given how-it-went value, all dated `at` (defaults to now).
    private static SongListItem Song(params int[] howItWent) => Song(Now, howItWent);

    private static SongListItem Song(DateTimeOffset at, params int[] howItWent)
    {
        var song = new SongListItem();
        foreach (var value in howItWent)
            song.Performances.Add(new Performance { HowItWent = value, Date = at });
        return song;
    }

    // ---- BuildContext (the prior) ----

    [Fact]
    public void BuildContext_prior_is_null_when_nothing_is_rated()
    {
        var songs = new[] { new SongListItem(), Song(0, 0) };   // no performances / only unrated ones

        var context = RatingScore.BuildContext(songs, RatingConfig.Default, Now);

        Assert.Null(context.PriorMean);
    }

    [Fact]
    public void BuildContext_prior_is_the_grand_mean_of_every_rated_sing()
    {
        var songs = new[] { Song(5), Song(4, 4, 3) };   // values 5,4,4,3 → mean 4.0

        var context = RatingScore.BuildContext(songs, RatingConfig.Default, Now);

        Assert.Equal(4.0, context.PriorMean!.Value, 6);
    }

    [Fact]
    public void BuildContext_ignores_logged_but_unrated_sings()
    {
        var songs = new[] { Song(5, 0, 0) };   // the two zeros must not drag the mean down

        var context = RatingScore.BuildContext(songs, RatingConfig.Default, Now);

        Assert.Equal(5.0, context.PriorMean!.Value, 6);
    }

    // ---- StarFor (the shrinkage) ----

    [Fact]
    public void StarFor_is_null_when_the_song_has_no_rated_sings()
    {
        var context = new RatingContext(4.0, RatingConfig.Default, Now);

        Assert.Null(RatingScore.StarFor(new SongListItem(), context));
        Assert.Null(RatingScore.StarFor(Song(0, 0), context));
    }

    [Fact]
    public void StarFor_is_null_when_the_list_has_no_prior()
    {
        var context = new RatingContext(PriorMean: null, RatingConfig.Default, Now);

        Assert.Null(RatingScore.StarFor(Song(5), context));
    }

    [Fact]
    public void StarFor_shrinks_a_single_rating_toward_the_prior()
    {
        var context = new RatingContext(4.0, RatingConfig.Default, Now);   // m = 3

        // one 5: (1*5 + 3*4) / (1+3) = 17/4 = 4.25 — pulled down from 5 toward the prior.
        Assert.Equal(4.25, RatingScore.StarFor(Song(5), context)!.Value, 6);
    }

    [Fact]
    public void StarFor_trusts_a_heavily_sung_song_close_to_its_own_average()
    {
        var context = new RatingContext(4.0, RatingConfig.Default, Now);

        // ten 4.5s: (10*4.5 + 3*4) / 13 = 57/13 ≈ 4.3846 — barely moved from 4.5.
        Assert.Equal(57.0 / 13.0, RatingScore.StarFor(Song(4, 5, 4, 5, 4, 5, 4, 5, 4, 5), context)!.Value, 6);
    }

    [Fact]
    public void StarFor_lets_ten_solid_sings_beat_one_lucky_five_on_a_mixed_list()
    {
        // Prior 4.0 is a normally-mixed list. This is the headline scenario the whole feature is about.
        var context = new RatingContext(4.0, RatingConfig.Default, Now);

        var oneLuckyFive = RatingScore.StarFor(Song(5), context)!.Value;                 // 4.25
        var tenSolid = RatingScore.StarFor(Song(4, 5, 4, 5, 4, 5, 4, 5, 4, 5), context)!.Value;   // ≈4.385

        Assert.True(tenSolid > oneLuckyFive);
    }

    [Fact]
    public void StarFor_still_ranks_one_five_above_ten_lower_when_the_whole_list_is_excellent()
    {
        // Honest trap the user accepted: when the prior is very high (uniformly great list), a lone 5 is at par,
        // so confidence-only shrinkage does NOT flip it below a true 4.5.
        var context = new RatingContext(4.8, RatingConfig.Default, Now);

        var oneFive = RatingScore.StarFor(Song(5), context)!.Value;                       // (5+14.4)/4 = 4.85
        var tenFourFive = RatingScore.StarFor(Song(4, 5, 4, 5, 4, 5, 4, 5, 4, 5), context)!.Value;   // (45+14.4)/13 ≈ 4.569

        Assert.True(oneFive > tenFourFive);
    }

    [Fact]
    public void StarFor_shrinks_harder_with_a_bigger_prior_weight()
    {
        var song = Song(5);
        var gentle = RatingScore.StarFor(song, new RatingContext(4.0, RatingConfig.Default, Now))!.Value;      // m=3 → 4.25
        var aggressive = RatingScore.StarFor(song, new RatingContext(4.0, RatingConfig.Default with { PriorWeight = 9 }, Now))!.Value;

        Assert.True(aggressive < gentle);   // more shrinkage pulls the lone 5 further toward 4.0
    }

    // ---- Recency ----

    [Fact]
    public void Recency_off_weights_old_and_recent_sings_equally()
    {
        var config = RatingConfig.Default;   // recency off
        var context = new RatingContext(3.0, config, Now);

        var recentHigh = RatingScore.StarFor(Song(Now, 5), context)!.Value;
        var oldHigh = RatingScore.StarFor(Song(Now.AddDays(-3650), 5), context)!.Value;   // 10 years old

        Assert.Equal(recentHigh, oldHigh, 6);   // age is irrelevant when recency is off
    }

    [Fact]
    public void Recency_on_favors_a_recent_sing_over_an_equally_good_old_one()
    {
        var config = RatingConfig.Default with { RecencyEnabled = true };   // 180-day half-life
        var context = new RatingContext(3.0, config, Now);

        var recentFive = RatingScore.StarFor(Song(Now, 5), context)!.Value;
        var oldFive = RatingScore.StarFor(Song(Now.AddDays(-360), 5), context)!.Value;   // two half-lives → weight 0.25

        // The old 5 has a smaller effective count, so it shrinks harder toward the 3.0 prior and scores lower.
        Assert.True(recentFive > oldFive);
    }

    [Fact]
    public void Recency_on_prior_also_weights_recent_sings_more()
    {
        // A recent high sing and an old low sing: the prior should lean toward the recent (high) one.
        var config = RatingConfig.Default with { RecencyEnabled = true };
        var songs = new[] { Song(Now, 5), Song(Now.AddDays(-720), 1) };   // old 1 is heavily decayed

        var context = RatingScore.BuildContext(songs, config, Now);

        Assert.True(context.PriorMean!.Value > 4.0);   // dominated by the recent 5, not dragged to the midpoint (3.0)
    }
}
