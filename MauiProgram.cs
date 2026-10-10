using AuroraMediaPlayer.Data;
using AuroraMediaPlayer.Services;
using AuroraMediaPlayer.ViewModels;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace AuroraMediaPlayer;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			// Arka planda çalma + bildirim/kilit ekranı kontrolleri (Ayarlar'dan kapatılabilir; iOS'ta Info.plist 'audio' modu)
			.UseMauiCommunityToolkitMediaElement(AppSettings.BackgroundAudio, options =>
			{
				// Android TextureView: yuvarlak köşeli kart içinde düzgün çizim (iOS'ta etkisizdir)
				options.SetDefaultAndroidViewType(CommunityToolkit.Maui.Core.AndroidViewType.TextureView);
			})
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("Poppins-Light.ttf", "PoppinsLight");
				fonts.AddFont("Poppins-Regular.ttf", "Poppins");
				fonts.AddFont("Poppins-Medium.ttf", "PoppinsMedium");
				fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Veri + servisler
		builder.Services.AddSingleton<AuroraDatabase>();
#if ANDROID
		builder.Services.AddSingleton<IMediaScanner, Droid.AndroidMediaScanner>();
		builder.Services.AddSingleton<IPlatformUi, Droid.AndroidPlatformUi>();
#elif IOS
		builder.Services.AddSingleton<IMediaScanner, Ios.IosMediaScanner>();
		builder.Services.AddSingleton<IPlatformUi, Ios.IosPlatformUi>();
#endif
		builder.Services.AddSingleton<MediaLibrary>();
		builder.Services.AddSingleton<PlayerController>();
		builder.Services.AddSingleton<MainViewModel>();
		builder.Services.AddSingleton<MainPage>();

		return builder.Build();
	}
}
