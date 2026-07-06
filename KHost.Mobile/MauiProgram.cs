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

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
