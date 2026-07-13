namespace KHost.Mobile.Services;

/// <summary>
/// Ephemeral, per-launch app state that isn't persisted (unlike <see cref="IAppSettings"/>). Lives as a singleton
/// so it's shared app-wide for the life of the process and resets on the next launch.
/// </summary>
public interface IAppSession
{
    /// <summary>
    /// Whether the one-time "smart landing" decision has been made this launch. The first time My Songs loads we
    /// route to the Tonight tab if a set is queued, otherwise stay on My List; every later navigation is left
    /// alone (so tapping "My List" doesn't bounce back to Tonight). Set once, then never re-evaluated this launch.
    /// </summary>
    bool LandingResolved { get; set; }
}
