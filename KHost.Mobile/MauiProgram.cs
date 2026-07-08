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

		// On-device song list. Singleton so its in-memory cache and Changed event are shared app-wide.
		builder.Services.AddSingleton<ISongListStore, JsonFileSongListStore>();

		// Opens external links (e.g. a YouTube search) in the OS browser / matching app.
		builder.Services.AddSingleton<ILinkLauncher, MauiLinkLauncher>();

		// Token-free import of public YouTube Music playlists (title + artist) via the playlist page.
		builder.Services.AddSingleton<IYouTubeMusicImportService>(_ => new YouTubeMusicImportService(new HttpClient()));

		// Token-free import of public Spotify playlists (title + artist) via the embed endpoint.
		builder.Services.AddSingleton<ISpotifyImportService>(_ => new SpotifyImportService(new HttpClient()));

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
