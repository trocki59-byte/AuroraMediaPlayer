using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;
using AuroraMediaPlayer.Services;

namespace AuroraMediaPlayer;

[Activity(
	Label = "Aurora Media Player",
	Theme = "@style/Maui.SplashTheme",
	MainLauncher = true,
	LaunchMode = LaunchMode.SingleTask,
	ScreenOrientation = ScreenOrientation.FullUser,
	ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
						   ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density |
						   ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation)]
// Dosya yöneticisi / WhatsApp / Galeri'den "Birlikte aç → Aurora"
[IntentFilter(
	new[] { Intent.ActionView },
	Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
	DataSchemes = new[] { "content", "file" },
	DataMimeTypes = new[] { "video/*", "audio/*" })]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		// İçerik sistem çubuklarının arkasına uzansın (aurora görseli durum çubuğunun altında görünsün)
		if (Window is not null)
		{
			WindowCompat.SetDecorFitsSystemWindows(Window, false);
			var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
			controller.AppearanceLightStatusBars = false;
			controller.AppearanceLightNavigationBars = false;
		}

		HandleViewIntent(Intent);
	}

	protected override void OnNewIntent(Intent? intent)
	{
		base.OnNewIntent(intent);
		HandleViewIntent(intent);
	}

	static void HandleViewIntent(Intent? intent)
	{
		if (intent?.Action != Intent.ActionView || intent.Data is null)
			return;

		var uri = intent.Data.ToString();
		if (string.IsNullOrEmpty(uri))
			return;

		var mime = intent.Type ?? Platform.AppContext.ContentResolver?.GetType(intent.Data) ?? "";
		ExternalOpen.Request(uri, mime.StartsWith("video", StringComparison.OrdinalIgnoreCase));
	}
}
