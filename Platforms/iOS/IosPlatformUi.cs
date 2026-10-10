using AuroraMediaPlayer.Services;
using AVKit;
using CoreGraphics;
using UIKit;

namespace AuroraMediaPlayer.Ios;

public sealed class IosPlatformUi : IPlatformUi
{
	AVRoutePickerView? _routePicker;

	public void SetFullscreen(bool fullscreen, bool landscape)
	{
		// Durum çubuğu (Info.plist: UIViewControllerBasedStatusBarAppearance = false)
#pragma warning disable CA1422, CS0618
		UIApplication.SharedApplication.SetStatusBarHidden(fullscreen, UIStatusBarAnimation.Fade);
#pragma warning restore CA1422, CS0618

		if (!OperatingSystem.IsIOSVersionAtLeast(16))
			return;

		var scene = UIApplication.SharedApplication.ConnectedScenes
			.OfType<UIWindowScene>()
			.FirstOrDefault();
		if (scene is null)
			return;

		var isPad = UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad;
		var mask = fullscreen && landscape
			? UIInterfaceOrientationMask.Landscape
			: (isPad ? UIInterfaceOrientationMask.All : UIInterfaceOrientationMask.AllButUpsideDown);

		try
		{
			scene.RequestGeometryUpdate(new UIWindowSceneGeometryPreferencesIOS(mask), _ => { });
			Platform.GetCurrentUIViewController()?.SetNeedsUpdateOfSupportedInterfaceOrientations();
		}
		catch
		{
			// Döndürme desteklenmiyorsa (ör. iPad bölünmüş ekran) yoksay
		}
	}

	/// <summary>AirPlay cihaz seçicisini açar (Apple TV, AirPlay hoparlör, akıllı TV).</summary>
	public void OpenCastSettings()
	{
		var window = Platform.GetCurrentUIViewController()?.View?.Window;
		if (window is null)
			return;

		if (_routePicker is null || _routePicker.Superview is null)
		{
			_routePicker = new AVRoutePickerView(new CGRect(-100, -100, 44, 44))
			{
				PrioritizesVideoDevices = true,
				Alpha = 0.011f,
			};
			window.AddSubview(_routePicker);
		}

		// Seçicinin içindeki butona programla dokun
		foreach (var sub in _routePicker.Subviews)
		{
			if (sub is UIButton button)
			{
				button.SendActionForControlEvents(UIControlEvent.TouchUpInside);
				return;
			}
		}
	}

	public void OpenAppSettings() => AppInfo.ShowSettingsUI();
}
