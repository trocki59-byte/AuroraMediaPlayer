using Android.App;
using Android.Runtime;

namespace AuroraMediaPlayer;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
		AuroraMediaPlayer.Services.CrashLog.Hook();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
