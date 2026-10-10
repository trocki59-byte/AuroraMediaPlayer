using AVFoundation;
using Foundation;
using UIKit;

namespace AuroraMediaPlayer;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
	{
		// Arka planda / sessiz modda da ses çıksın
		try
		{
			var session = AVAudioSession.SharedInstance();
			session.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.AllowAirPlay, out _);
			session.SetActive(true, out _);
		}
		catch
		{
			// ses oturumu kurulamazsa normal devam et
		}

		return base.FinishedLaunching(application, launchOptions);
	}

	/// <summary>Başka bir uygulamadan "Birlikte aç → Aurora" ile gelen dosya.</summary>
	public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
	{
		if (Ios.IosFileImport.HandleIncoming(url))
			return true;
		return base.OpenUrl(application, url, options);
	}
}
