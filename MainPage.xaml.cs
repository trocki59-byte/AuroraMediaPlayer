using System.ComponentModel;
using AuroraMediaPlayer.Models;
using AuroraMediaPlayer.Services;
using AuroraMediaPlayer.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace AuroraMediaPlayer;

public partial class MainPage : ContentPage
{
	readonly MainViewModel _vm;
	readonly PlayerController _player;
	readonly IPlatformUi _ui;
	readonly IDispatcherTimer _chromeTimer;
	readonly VisualElement[] _chrome;

	bool _updatingSlider;
	bool _isFullscreen;
	bool _chromeVisible = true;
	bool _imagesLoaded;
	double _lastWidth = -1, _lastHeight = -1;
	CancellationTokenSource? _seekToastCts;

	public MainPage(MainViewModel vm, PlayerController player, IPlatformUi ui)
	{
		InitializeComponent();
		_vm = vm;
		_player = player;
		_ui = ui;
		BindingContext = vm;

		_player.Attach(MediaPlayer);
		_player.PropertyChanged += OnPlayerPropertyChanged;
		_player.ErrorOccurred += async (_, msg) => await DisplayAlertAsync("Aurora", msg, "Tamam");

		_chrome = [TitleBar, ProgressRow, MainControls, ExtraControls, SideButtons, BottomFade];

		_chromeTimer = Dispatcher.CreateTimer();
		_chromeTimer.Interval = TimeSpan.FromSeconds(3.5);
		_chromeTimer.IsRepeating = false;
		_chromeTimer.Tick += (_, _) =>
		{
			if (_isFullscreen && _player.IsPlaying)
				_ = SetChromeVisibleAsync(false);
		};

		ExternalOpen.Requested += (_, _) => MainThread.BeginInvokeOnMainThread(HandleExternalOpen);
		SizeChanged += (_, _) => ApplyLayout();
	}

	// ================================================================ yaşam döngüsü

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadBackgroundImagesAsync();
		ApplyLayout(force: true);
		await _vm.InitializeAsync();
		HandleExternalOpen();
	}

	async Task LoadBackgroundImagesAsync()
	{
		if (_imagesLoaded) return;
		_imagesLoaded = true;
		var header = await ReadAssetAsync("aurora_bg.jpg");
		var art = await ReadAssetAsync("aurora_art.jpg");
		if (header is not null)
			HeaderBackground.Source = ImageSource.FromStream(() => new MemoryStream(header));
		if (art is not null)
			ArtBackground.Source = ImageSource.FromStream(() => new MemoryStream(art));
	}

	static async Task<byte[]?> ReadAssetAsync(string name)
	{
		try
		{
			await using var s = await FileSystem.OpenAppPackageFileAsync(name);
			using var ms = new MemoryStream();
			await s.CopyToAsync(ms);
			return ms.ToArray();
		}
		catch
		{
			return null;
		}
	}

	void HandleExternalOpen()
	{
		var req = ExternalOpen.Take();
		if (req is null) return;
		var (uri, isVideo) = req.Value;
		var known = _vm.FindByUri(uri);
		_player.PlayExternal(uri, isVideo, known);

		// iPhone: gelen dosya Aurora klasörüne kopyalandı → kütüphanede de görünsün
		if (known is null && _vm.IsImportSupported)
			_ = _vm.RescanAsync();
	}

	protected override bool OnBackButtonPressed()
	{
		if (_isFullscreen)
		{
			_ = ExitFullscreenAsync();
			return true;
		}
		if (_vm.GoBack())
			return true;
		return base.OnBackButtonPressed();
	}

	// ================================================================ yerleşim

	void ApplyLayout(bool force = false)
	{
		var w = Width;
		var h = Height;
		if (w <= 0 || h <= 0) return;
		if (!force && Math.Abs(w - _lastWidth) < 1 && Math.Abs(h - _lastHeight) < 1) return;
		_lastWidth = w;
		_lastHeight = h;

		if (_isFullscreen)
		{
			LayoutFullscreen();
			return;
		}

		var landscape = w > h;
		var phone = Math.Min(w, h) < 600;
		var sideBySide = landscape && w >= 640;
		var shortScreen = h < 520;

		// ---- başlık ----
		LogoImage.HeightRequest = shortScreen ? 58 : phone ? 84 : 118;
		HeaderBackground.HeightRequest = shortScreen ? 170 : phone ? 250 : 330;
		HeaderFadeBox.HeightRequest = HeaderBackground.HeightRequest + 1;
		ShellGrid.Padding = phone ? new Thickness(10, 4, 10, 8) : new Thickness(16, 6, 16, 12);
		ShellGrid.RowSpacing = shortScreen ? 6 : 12;

		// ---- sekmeler ----
		var available = w - ShellGrid.Padding.HorizontalThickness - 4;
		var compactTabs = phone || shortScreen;
		var minTab = compactTabs ? 88 : 112;
		NavGrid.ColumnSpacing = compactTabs ? 8 : 10;
		var needed = 7 * minTab + 6 * NavGrid.ColumnSpacing;
		NavGrid.WidthRequest = Math.Max(available, needed);
		foreach (var tab in NavGrid.Children.OfType<Views.NavTabButton>())
			tab.SetCompact(compactTabs);

		// ---- oynatıcı + kütüphane ----
		ContentGrid.ColumnDefinitions.Clear();
		ContentGrid.RowDefinitions.Clear();
		double playerWidth;

		if (sideBySide)
		{
			ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.7, GridUnitType.Star)));
			ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
			ContentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
			Grid.SetRow(PlayerCard, 0); Grid.SetColumn(PlayerCard, 0);
			Grid.SetRow(LibraryCard, 0); Grid.SetColumn(LibraryCard, 1);
			playerWidth = available * 1.7 / 2.7;
			VideoArea.HeightRequest = -1;
			PlayerGrid.RowDefinitions[1].Height = GridLength.Star;
		}
		else
		{
			ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			ContentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			ContentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
			Grid.SetRow(PlayerCard, 0); Grid.SetColumn(PlayerCard, 0);
			Grid.SetRow(LibraryCard, 1); Grid.SetColumn(LibraryCard, 0);
			playerWidth = available;
			var videoH = (playerWidth - 16) * 9.0 / 16.0;
			VideoArea.HeightRequest = Math.Min(videoH, h * 0.36);
			PlayerGrid.RowDefinitions[1].Height = GridLength.Auto;
		}

		LayoutControls(playerWidth, phone);
	}

	void LayoutControls(double playerWidth, bool phone)
	{
		var oneRow = playerWidth >= 780;

		if (oneRow)
		{
			Grid.SetRow(MainControls, 3); Grid.SetColumn(MainControls, 0); Grid.SetColumnSpan(MainControls, 1);
			Grid.SetRow(ExtraControls, 3); Grid.SetColumn(ExtraControls, 1); Grid.SetColumnSpan(ExtraControls, 1);
			MainControls.Padding = new Thickness(12, 2, 4, 12);
			ExtraControls.Padding = new Thickness(0, 2, 14, 12);
			ExtraControls.HorizontalOptions = LayoutOptions.End;
		}
		else
		{
			Grid.SetRow(MainControls, 3); Grid.SetColumn(MainControls, 0); Grid.SetColumnSpan(MainControls, 2);
			Grid.SetRow(ExtraControls, 4); Grid.SetColumn(ExtraControls, 0); Grid.SetColumnSpan(ExtraControls, 2);
			MainControls.Padding = new Thickness(6, 0, 6, 2);
			ExtraControls.Padding = new Thickness(8, 0, 8, 10);
			ExtraControls.HorizontalOptions = LayoutOptions.Fill;
		}

		// Dar ekranda butonları küçült
		var small = playerWidth < 420;
		PlayPauseButton.WidthRequest = PlayPauseButton.HeightRequest = small ? 64 : 78;
		PlayPauseButton.CornerRadius = small ? 32 : 39;
		PlayPauseButton.Padding = small ? 19 : 24;
		PlayPauseButton.Margin = small ? new Thickness(4, 0) : new Thickness(10, 0);
		MainControls.ColumnSpacing = small ? 0 : 4;

		VolumeSlider.WidthRequest = oneRow ? 150 : -1;
		CenterPlayButton.WidthRequest = CenterPlayButton.HeightRequest = phone ? 72 : 92;
		CenterPlayButton.CornerRadius = phone ? 36 : 46;
		CenterPlayButton.Padding = phone ? new Thickness(22, 20, 17, 20) : new Thickness(28, 26, 22, 26);
		SideButtons.Spacing = phone ? 8 : 12;
		foreach (var b in SideButtons.Children.OfType<ImageButton>())
		{
			b.WidthRequest = b.HeightRequest = phone ? 42 : 50;
			b.CornerRadius = phone ? 21 : 25;
			b.Padding = phone ? 11 : 13;
		}
		TitleLabel.FontSize = phone ? 14 : 16;
		InfoLabel.FontSize = phone ? 12 : 14;
	}

	// ================================================================ tam ekran

	async Task EnterFullscreenAsync()
	{
		if (_isFullscreen) return;
		_isFullscreen = true;

		var landscapeVideo = _player.IsVideo &&
			(MediaPlayer.MediaWidth >= MediaPlayer.MediaHeight || MediaPlayer.MediaWidth == 0);
		_ui.SetFullscreen(true, AppSettings.AutoRotateFullscreen && landscapeVideo);

		HeaderGrid.IsVisible = false;
		NavScroll.IsVisible = false;
		LibraryCard.IsVisible = false;
		HeaderBackground.IsVisible = false;
		HeaderFadeBox.IsVisible = false;
		FullscreenButton.Source = "ic_fullscreen_exit.png";

		LayoutFullscreen();
		await SetChromeVisibleAsync(true);
	}

	void LayoutFullscreen()
	{
		ShellGrid.Padding = 0;
		ShellGrid.RowSpacing = 0;
		ContentGrid.ColumnDefinitions.Clear();
		ContentGrid.RowDefinitions.Clear();
		ContentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		ContentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
		Grid.SetRow(PlayerCard, 0); Grid.SetColumn(PlayerCard, 0);
		Grid.SetRow(ContentGrid, 0);
		Grid.SetRowSpan(ContentGrid, 3);

		PlayerCard.StrokeThickness = 0;
		PlayerCard.StrokeShape = new RoundRectangle { CornerRadius = 0 };
		PlayerCard.Shadow = null!;
		PlayerCard.Background = new SolidColorBrush(Colors.Black);

		Grid.SetRow(VideoArea, 0);
		Grid.SetRowSpan(VideoArea, 5);
		VideoArea.Margin = 0;
		VideoArea.HeightRequest = -1;
		PlayerGrid.RowDefinitions[1].Height = GridLength.Star;

		TitleBar.Background = (Brush)Application.Current!.Resources["ChromeTopFade"];
		BottomFade.IsVisible = true;

		LayoutControls(Width, Math.Min(Width, Height) < 600);
	}

	async Task ExitFullscreenAsync()
	{
		if (!_isFullscreen) return;
		_isFullscreen = false;
		_chromeTimer.Stop();
		_ui.SetFullscreen(false, false);

		HeaderGrid.IsVisible = true;
		NavScroll.IsVisible = true;
		LibraryCard.IsVisible = true;
		HeaderBackground.IsVisible = true;
		HeaderFadeBox.IsVisible = true;
		FullscreenButton.Source = "ic_fullscreen.png";

		Grid.SetRow(ContentGrid, 2);
		Grid.SetRowSpan(ContentGrid, 1);
		PlayerCard.StrokeThickness = 1.5;
		PlayerCard.StrokeShape = new RoundRectangle { CornerRadius = 22 };
		PlayerCard.Background = (Brush)Application.Current!.Resources["CardFill"];
		PlayerCard.Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#3B7BFF")), Opacity = 0.35f, Radius = 22 };

		Grid.SetRow(VideoArea, 1);
		Grid.SetRowSpan(VideoArea, 1);
		VideoArea.Margin = new Thickness(8, 0);
		TitleBar.Background = null;
		BottomFade.IsVisible = false;

		await SetChromeVisibleAsync(true);
		ApplyLayout(force: true);
	}

	async Task SetChromeVisibleAsync(bool visible)
	{
		_chromeVisible = visible;
		foreach (var el in _chrome)
		{
			if (el == BottomFade && !_isFullscreen) continue;
			el.InputTransparent = !visible && el != BottomFade;
		}
		await Task.WhenAll(_chrome.Select(el => el.FadeToAsync(visible ? 1 : 0, 220)));
		if (visible) RestartChromeTimer();
	}

	void RestartChromeTimer()
	{
		_chromeTimer.Stop();
		if (_isFullscreen) _chromeTimer.Start();
	}

	// ================================================================ oynatıcı olayları

	void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(PlayerController.Progress):
				if (!_player.IsUserSeeking)
				{
					_updatingSlider = true;
					ProgressSlider.Value = _player.Progress;
					_updatingSlider = false;
				}
				break;
			case nameof(PlayerController.IsPlaying):
				if (_isFullscreen)
				{
					if (!_player.IsPlaying) _ = SetChromeVisibleAsync(true);
					else RestartChromeTimer();
				}
				break;
			case nameof(PlayerController.IsVideo):
				if (_isFullscreen && !_player.IsVideo)
					_ = ExitFullscreenAsync();
				break;
		}
	}

	void OnSeekDragStarted(object? sender, EventArgs e)
	{
		_player.IsUserSeeking = true;
		_chromeTimer.Stop();
	}

	async void OnSeekDragCompleted(object? sender, EventArgs e)
	{
		await _player.SeekToFractionAsync(ProgressSlider.Value);
		_player.IsUserSeeking = false;
		RestartChromeTimer();
	}

	async void OnSeekValueChanged(object? sender, ValueChangedEventArgs e)
	{
		if (_updatingSlider) return;
		if (_player.IsUserSeeking)
		{
			_player.PreviewFraction(e.NewValue);
			return;
		}
		// Çubuğa dokunarak atlama (sürüklemeden)
		if (Math.Abs(e.NewValue - _player.Progress) > 0.005)
			await _player.SeekToFractionAsync(e.NewValue);
	}

	void OnPlayPauseClicked(object? sender, EventArgs e)
	{
		_player.TogglePlay();
		RestartChromeTimer();
	}

	void OnPrevClicked(object? sender, EventArgs e) { _player.Previous(); RestartChromeTimer(); }
	void OnNextClicked(object? sender, EventArgs e) { _player.Next(); RestartChromeTimer(); }
	async void OnReplayClicked(object? sender, EventArgs e) { await _player.SkipAsync(-10); RestartChromeTimer(); }
	async void OnForwardClicked(object? sender, EventArgs e) { await _player.SkipAsync(10); RestartChromeTimer(); }
	void OnShuffleClicked(object? sender, EventArgs e) => _player.Shuffle = !_player.Shuffle;
	void OnRepeatClicked(object? sender, EventArgs e) => _player.CycleRepeat();
	void OnMuteClicked(object? sender, EventArgs e) => _player.ToggleMute();
	void OnCastClicked(object? sender, EventArgs e) => _ui.OpenCastSettings();
	async void OnFavoriteClicked(object? sender, EventArgs e) => await _player.ToggleFavoriteAsync();

	async void OnFullscreenClicked(object? sender, EventArgs e)
	{
		if (_isFullscreen) await ExitFullscreenAsync();
		else await EnterFullscreenAsync();
	}

	void OnQueueClicked(object? sender, EventArgs e)
	{
		if (_isFullscreen) _ = ExitFullscreenAsync();
		_vm.SelectTab(LibraryTab.Queue);
	}

	async void OnVideoTapped(object? sender, TappedEventArgs e)
	{
		if (_isFullscreen)
			await SetChromeVisibleAsync(!_chromeVisible);
		else if (!_player.HasMedia)
			_player.TogglePlay();
	}

	async void OnVideoDoubleTapped(object? sender, TappedEventArgs e)
	{
		if (!_player.HasMedia) return;
		var pos = e.GetPosition(TouchLayer);
		var forward = pos is null || pos.Value.X >= TouchLayer.Width / 2;
		await _player.SkipAsync(forward ? 10 : -10);
		await ShowSeekToastAsync(forward ? "10 sn  »" : "«  10 sn");
	}

	async Task ShowSeekToastAsync(string text)
	{
		_seekToastCts?.Cancel();
		var cts = _seekToastCts = new CancellationTokenSource();
		SeekToast.Text = text;
		SeekToast.HorizontalOptions = text.StartsWith('«') ? LayoutOptions.Start : LayoutOptions.End;
		SeekToast.Margin = new Thickness(60, 0);
		await SeekToast.FadeToAsync(1, 120);
		try { await Task.Delay(650, cts.Token); } catch (TaskCanceledException) { return; }
		await SeekToast.FadeToAsync(0, 250);
	}

	async void OnPlayerMoreClicked(object? sender, EventArgs e)
	{
		const string speed = "Oynatma hızı";
		const string info = "Medya bilgisi";
		var repeat = $"Tekrar: {_player.RepeatText}";
		const string stop = "Durdur";

		var choice = await DisplayActionSheetAsync("Oynatıcı", "Kapat", null, speed, repeat, info, stop);
		switch (choice)
		{
			case speed:
				var s = await DisplayActionSheetAsync($"Hız ({_player.SpeedText})", "Kapat", null,
					"0.5x", "0.75x", "1x (Normal)", "1.25x", "1.5x", "2x");
				if (s is not null && s != "Kapat")
				{
					var val = s.Split('x')[0];
					if (double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
						_player.Speed = d;
				}
				break;
			case info:
				await ShowMediaInfoAsync();
				break;
			case stop:
				_player.Stop();
				break;
			default:
				if (choice == repeat) _player.CycleRepeat();
				break;
		}
	}

	async Task ShowMediaInfoAsync()
	{
		var m = _player.Current;
		if (m is null)
		{
			await DisplayAlertAsync("Medya bilgisi", _player.Title, "Tamam");
			return;
		}

		var lines = new List<string>
		{
			$"Ad: {m.FileName}",
			$"Tür: {(m.IsVideo ? "Video" : "Müzik")}  ({m.MimeType})",
			$"Süre: {m.DurationText}",
		};
		if (m.IsVideo && MediaPlayer.MediaWidth > 0)
			lines.Add($"Çözünürlük: {MediaPlayer.MediaWidth} × {MediaPlayer.MediaHeight}");
		if (!m.IsVideo)
		{
			if (!string.IsNullOrWhiteSpace(m.Artist)) lines.Add($"Sanatçı: {m.Artist}");
			if (!string.IsNullOrWhiteSpace(m.Album)) lines.Add($"Albüm: {m.Album}");
		}
		lines.Add($"Boyut: {Formatters.Size(m.SizeBytes)}");
		lines.Add($"Klasör: {m.FolderPath}");
		lines.Add($"Eklenme: {Formatters.Date(m.DateAdded)}");
		lines.Add($"Oynatılma: {m.PlayCount} kez");
		await DisplayAlertAsync("Medya bilgisi", string.Join("\n", lines), "Tamam");
	}

	// ================================================================ başlık / menü

	void OnSearchClicked(object? sender, EventArgs e)
	{
		if (_vm.IsSettingsTab)
			_vm.SelectTab(LibraryTab.Home);
		_vm.IsSearchVisible = !_vm.IsSearchVisible;
		if (_vm.IsSearchVisible)
			Dispatcher.Dispatch(() => SearchBox.Focus());
	}

	async void OnMenuClicked(object? sender, EventArgs e)
	{
		const string rescan = "Kütüphaneyi yeniden tara";
		const string import = "Dosya ekle";
		const string sort = "Sıralama";
		const string queue = "Oynatma sırası";
		const string settings = "Ayarlar";
		const string about = "Hakkında";

		var options = new List<string> { rescan };
		if (_vm.IsImportSupported) options.Add(import);
		options.AddRange([sort, queue, settings, about]);

		var choice = await DisplayActionSheetAsync("Aurora Media Player", "Kapat", null, options.ToArray());
		switch (choice)
		{
			case import:
				var added = await _vm.ImportFilesAsync();
				if (added > 0)
					await DisplayAlertAsync("Aurora", $"{added} dosya kütüphaneye eklendi.", "Tamam");
				break;
			case rescan:
				await _vm.RescanAsync();
				break;
			case sort:
				OnSortTapped(null, EventArgs.Empty);
				break;
			case queue:
				_vm.SelectTab(LibraryTab.Queue);
				break;
			case settings:
				_vm.SelectTab(LibraryTab.Settings);
				break;
			case about:
				await DisplayAlertAsync("TROÇKİ® AURORA MEDIA PLAYER",
					$"{_vm.VersionText}\n\nSen İzle, Dünya Güzelleşsin…\n\nTROÇKİ® Software\nDüşün • Keşfet • Üret • Paylaş",
					"Tamam");
				break;
		}
	}

	async void OnSortTapped(object? sender, EventArgs e)
	{
		const string byDate = "Tarihe göre";
		const string byTitle = "Ada göre";
		const string byDuration = "Süreye göre";
		const string bySize = "Boyuta göre";
		var dir = _vm.SortDescending ? "Artan sıraya çevir ↑" : "Azalan sıraya çevir ↓";

		var choice = await DisplayActionSheetAsync("Sırala", "Kapat", null, byDate, byTitle, byDuration, bySize, dir);
		switch (choice)
		{
			case byDate: _vm.Sort = SortMode.DateAdded; break;
			case byTitle: _vm.Sort = SortMode.Title; break;
			case byDuration: _vm.Sort = SortMode.Duration; break;
			case bySize: _vm.Sort = SortMode.Size; break;
			default:
				if (choice == dir) _vm.SortDescending = !_vm.SortDescending;
				break;
		}
	}

	// ================================================================ liste

	void OnItemTapped(object? sender, TappedEventArgs e)
	{
		if ((sender as BindableObject)?.BindingContext is MediaItem item)
			_vm.PlayItemCommand.Execute(item);
	}

	void OnItemFavoriteClicked(object? sender, EventArgs e)
	{
		if ((sender as BindableObject)?.BindingContext is MediaItem item)
			_vm.FavoriteItemCommand.Execute(item);
	}

	void OnFolderTapped(object? sender, TappedEventArgs e)
	{
		if ((sender as BindableObject)?.BindingContext is FolderInfo folder)
			_vm.OpenFolderCommand.Execute(folder);
	}

	async void OnClearHistoryClicked(object? sender, EventArgs e)
	{
		if (await DisplayAlertAsync("Geçmişi temizle", "Son oynatılanlar ve kaldığınız yerler silinsin mi?", "Temizle", "Vazgeç"))
			_vm.ClearHistoryCommand.Execute(null);
	}

	async void OnClearFavoritesClicked(object? sender, EventArgs e)
	{
		if (await DisplayAlertAsync("Favorileri temizle", "Tüm favoriler kaldırılsın mı?", "Temizle", "Vazgeç"))
			_vm.ClearFavoritesCommand.Execute(null);
	}
}
