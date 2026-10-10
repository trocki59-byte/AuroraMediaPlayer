using AuroraMediaPlayer.Services;

namespace AuroraMediaPlayer;

public partial class App : Application
{
	readonly MainPage _mainPage;
	readonly PlayerController _player;

	public App(MainPage mainPage, PlayerController player)
	{
		InitializeComponent();
		UserAppTheme = AppTheme.Dark;
		_mainPage = mainPage;
		_player = player;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(_mainPage) { Title = "Aurora Media Player" };

		// Uygulama arka plana alınırken kaldığı yeri kaydet
		window.Stopped += (_, _) => _player.SaveProgress(force: true);
		window.Destroying += (_, _) => _player.SaveProgress(force: true);
		return window;
	}
}
