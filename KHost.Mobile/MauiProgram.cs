using KHost.Mobile.Client.Enrichment;
using KHost.Mobile.Client.Spotify;
using KHost.Mobile.Client.YouTubeMusic;
using KHost.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace KHost.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		// On-device song list. Singleton so its in-memory cache and Changed event are shared app-wide. The UI binds
		// to ISongListStore; the concrete type is also registered so both resolve to the one instance.
		builder.Services.AddSingleton<JsonFileSongListStore>();
		builder.Services.AddSingleton<ISongListStore>(sp => sp.GetRequiredService<JsonFileSongListStore>());

		// Opens external links (e.g. a YouTube search) in the OS browser / matching app.
		builder.Services.AddSingleton<ILinkLauncher, MauiLinkLauncher>();

		// Token-free import of public YouTube Music playlists (title + artist) via the playlist page.
		builder.Services.AddSingleton<IYouTubeMusicImportService>(_ => new YouTubeMusicImportService(new HttpClient()));

		// Token-free import of public Spotify playlists (title + artist) via the embed endpoint.
		builder.Services.AddSingleton<ISpotifyImportService>(_ => new SpotifyImportService(new HttpClient()));

		// Keyless release-year + genre lookup (iTunes Search API). Re-lookup is avoided per-song via the
		// SongListItem.MetadataLookedUp flag, so no separate cache layer is needed.
		builder.Services.AddSingleton<ITrackMetadataLookup>(_ => new ITunesTrackMetadataLookup(new HttpClient()));

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
