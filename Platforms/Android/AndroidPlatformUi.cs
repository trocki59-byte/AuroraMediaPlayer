using Android.Content;
using Android.Content.PM;
using AndroidX.Core.View;
using AuroraMediaPlayer.Services;
using ASettings = Android.Provider.Settings;
using AUri = Android.Net.Uri;

namespace AuroraMediaPlayer.Droid;

public sealed class AndroidPlatformUi : IPlatformUi
{
	public void SetFullscreen(bool fullscreen, bool landscape)
	{
		var activity = Platform.CurrentActivity;
		var window = activity?.Window;
		if (activity is null || window is null)
			return;

		var controller = WindowCompat.GetInsetsController(window, window.DecorView);
		if (fullscreen)
		{
			controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
			controller.Hide(WindowInsetsCompat.Type.SystemBars());
			if (landscape)
				activity.RequestedOrientation = ScreenOrientation.SensorLandscape;
		}
		else
		{
			controller.Show(WindowInsetsCompat.Type.SystemBars());
			activity.RequestedOrientation = ScreenOrientation.FullUser;
		}
	}

	public void OpenCastSettings()
	{
		// Üreticiye göre farklı ekranlar: önce Cast, sonra kablosuz ekran, en son ekran ayarları
		string[] actions =
		[
			"android.settings.CAST_SETTINGS",
			"android.settings.WIFI_DISPLAY_SETTINGS",
			ASettings.ActionDisplaySettings!,
		];

		foreach (var action in actions)
		{
			try
			{
				var intent = new Intent(action);
				intent.AddFlags(ActivityFlags.NewTask);
				Platform.AppContext.StartActivity(intent);
				return;
			}
			catch (ActivityNotFoundException)
			{
				// sıradakini dene
			}
		}
	}

	public void OpenAppSettings()
	{
		try
		{
			var intent = new Intent(ASettings.ActionApplicationDetailsSettings);
			intent.SetData(AUri.FromParts("package", Platform.AppContext.PackageName, null));
			intent.AddFlags(ActivityFlags.NewTask);
			Platform.AppContext.StartActivity(intent);
		}
		catch
		{
			AppInfo.ShowSettingsUI();
		}
	}
}
