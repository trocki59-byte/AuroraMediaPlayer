using AuroraMediaPlayer.Models;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

namespace AuroraMediaPlayer.Services;

/// <summary>
/// Oynatıcı beyni: çalma sırası, karıştırma, tekrar, kaldığı yerden devam,
/// ses, hız ve arayüze bağlanan tüm oynatma durumları.
/// </summary>
public sealed class PlayerController : ObservableObject
{
	readonly MediaLibrary _library;
	readonly IMediaScanner _scanner;
	int _loadVersion;
	readonly Random _rng = new();

	MediaElement? _player;
	List<MediaItem> _queue = [];
	List<int> _order = [];
	int _orderPos = -1;
	bool _pendingAutoPlay;
	DateTime _lastSave = DateTime.MinValue;

	MediaItem? _current;
	bool _isPlaying;
	bool _isBuffering;
	TimeSpan _position;
	TimeSpan _duration;
	double _volume = 80;
	bool _isMuted;
	bool _shuffle;
	RepeatMode _repeat;
	double _speed = 1.0;
	string _externalTitle = "";
	bool _externalIsVideo;

	public PlayerController(MediaLibrary library, IMediaScanner scanner)
	{
		_library = library;
		_scanner = scanner;
		_volume = Math.Round(AppSettings.Volume * 100);
		_shuffle = AppSettings.Shuffle;
		_repeat = AppSettings.Repeat;
	}

	/// <summary>Kullanıcı ilerleme çubuğunu sürüklerken konum güncellemelerini durdurur.</summary>
	public bool IsUserSeeking { get; set; }

	public event EventHandler<string>? ErrorOccurred;
	public event EventHandler? CurrentChanged;

	// ================================================================ durum

	public MediaItem? Current
	{
		get => _current;
		private set
		{
			if (_current == value) return;
			if (_current is not null) _current.IsNowPlaying = false;
			_current = value;
			if (_current is not null) _current.IsNowPlaying = true;
			OnPropertyChanged();
			OnPropertyChanged(nameof(HasMedia));
			OnPropertyChanged(nameof(Title));
			OnPropertyChanged(nameof(Artist));
			OnPropertyChanged(nameof(IsVideo));
			OnPropertyChanged(nameof(IsAudio));
			OnPropertyChanged(nameof(ArtworkPath));
			OnPropertyChanged(nameof(HasArtwork));
			OnPropertyChanged(nameof(IsFavorite));
			OnPropertyChanged(nameof(FavoriteIcon));
			OnPropertyChanged(nameof(InfoText));
			CurrentChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public IReadOnlyList<MediaItem> Queue => _queue;

	public bool HasMedia => _current is not null || !string.IsNullOrEmpty(_externalTitle);

	public string Title => _current?.FileName ?? (_externalTitle is { Length: > 0 } t ? t : "Aurora Media Player");

	public string Artist => _current is { IsVideo: false } c
		? (string.IsNullOrWhiteSpace(c.Artist) ? c.Folder : c.Artist)
		: "";

	public bool IsVideo => _current?.IsVideo ?? _externalIsVideo;
	public bool IsAudio => HasMedia && !IsVideo;

	public string ArtworkPath => _current?.HasThumb == true ? _current.ThumbPath! : "logo_mark.png";
	public bool HasArtwork => _current?.HasThumb == true;

	public bool IsFavorite => _current?.IsFavorite ?? false;
	public string FavoriteIcon => IsFavorite ? "ic_heart_red.png" : "ic_heart_outline.png";

	public bool IsPlaying
	{
		get => _isPlaying;
		private set
		{
			if (SetProperty(ref _isPlaying, value))
			{
				OnPropertyChanged(nameof(PlayPauseIcon));
				OnPropertyChanged(nameof(ShowCenterPlay));
			}
		}
	}

	public bool IsBuffering
	{
		get => _isBuffering;
		private set => SetProperty(ref _isBuffering, value);
	}

	public string PlayPauseIcon => IsPlaying ? "ic_pause.png" : "ic_play.png";
	public bool ShowCenterPlay => !IsPlaying;

	public TimeSpan Position
	{
		get => _position;
		private set
		{
			if (SetProperty(ref _position, value))
			{
				OnPropertyChanged(nameof(PositionText));
				OnPropertyChanged(nameof(Progress));
				OnPropertyChanged(nameof(InfoText));
			}
		}
	}

	public TimeSpan Duration
	{
		get => _duration;
		private set
		{
			if (SetProperty(ref _duration, value))
			{
				OnPropertyChanged(nameof(DurationText));
				OnPropertyChanged(nameof(Progress));
				OnPropertyChanged(nameof(InfoText));
			}
		}
	}

	public string PositionText => Formatters.Time(Position);
	public string DurationText => Formatters.Time(Duration);

	/// <summary>0..1 ilerleme (Slider'a bağlı).</summary>
	public double Progress => Duration.TotalMilliseconds > 0 ? Math.Clamp(Position.TotalMilliseconds / Duration.TotalMilliseconds, 0, 1) : 0;

	/// <summary>Başlık çubuğundaki "1920 × 1080 | 00:02:17 / 00:05:24".</summary>
	public string InfoText
	{
		get
		{
			var res = "";
			if (_player is not null && IsVideo && _player.MediaWidth > 0 && _player.MediaHeight > 0)
				res = $"{_player.MediaWidth} × {_player.MediaHeight}";
			else if (_current is { IsVideo: true })
				res = _current.ResolutionText;
			else if (_current is not null && !string.IsNullOrWhiteSpace(_current.Album))
				res = _current.Album;

			var time = $"{PositionText} / {DurationText}";
			return string.IsNullOrEmpty(res) ? time : $"{res}   |   {time}";
		}
	}

	/// <summary>0..100</summary>
	public double Volume
	{
		get => _volume;
		set
		{
			value = Math.Clamp(Math.Round(value), 0, 100);
			if (SetProperty(ref _volume, value))
			{
				AppSettings.Volume = value / 100.0;
				if (_player is not null) _player.Volume = value / 100.0;
				if (value > 0 && IsMuted) IsMuted = false;
				OnPropertyChanged(nameof(VolumeText));
				OnPropertyChanged(nameof(VolumeIcon));
			}
		}
	}

	public string VolumeText => ((int)Volume).ToString();

	public bool IsMuted
	{
		get => _isMuted;
		set
		{
			if (SetProperty(ref _isMuted, value))
			{
				if (_player is not null) _player.ShouldMute = value;
				OnPropertyChanged(nameof(VolumeIcon));
			}
		}
	}

	public string VolumeIcon => IsMuted || Volume == 0 ? "ic_mute.png" : "ic_volume.png";

	public bool Shuffle
	{
		get => _shuffle;
		set
		{
			if (SetProperty(ref _shuffle, value))
			{
				AppSettings.Shuffle = value;
				OnPropertyChanged(nameof(ShuffleIcon));
				RebuildOrder(CurrentQueueIndex);
			}
		}
	}

	public string ShuffleIcon => Shuffle ? "ic_shuffle_on.png" : "ic_shuffle.png";

	public RepeatMode Repeat
	{
		get => _repeat;
		set
		{
			if (SetProperty(ref _repeat, value))
			{
				AppSettings.Repeat = value;
				OnPropertyChanged(nameof(RepeatIcon));
				OnPropertyChanged(nameof(RepeatText));
			}
		}
	}

	public string RepeatIcon => Repeat switch
	{
		RepeatMode.All => "ic_repeat_on.png",
		RepeatMode.One => "ic_repeat_one.png",
		_ => "ic_repeat.png",
	};

	public string RepeatText => Repeat switch
	{
		RepeatMode.All => "Tümünü tekrarla",
		RepeatMode.One => "Tek parçayı tekrarla",
		_ => "Tekrar kapalı",
	};

	public double Speed
	{
		get => _speed;
		set
		{
			if (SetProperty(ref _speed, value) && _player is not null)
				_player.Speed = value;
			OnPropertyChanged(nameof(SpeedText));
		}
	}

	public string SpeedText => $"{Speed:0.##}x";

	int CurrentQueueIndex => _current is null ? -1 : _queue.IndexOf(_current);

	// ================================================================ bağlama

	public void Attach(MediaElement player)
	{
		if (_player == player) return;
		Detach();

		_player = player;
		_player.ShouldShowPlaybackControls = false;
		_player.ShouldAutoPlay = false;
		_player.ShouldKeepScreenOn = AppSettings.KeepScreenOn;
		_player.Volume = Volume / 100.0;
		_player.ShouldMute = IsMuted;
		_player.Speed = Speed;

		_player.MediaOpened += OnMediaOpened;
		_player.MediaEnded += OnMediaEnded;
		_player.MediaFailed += OnMediaFailed;
		_player.PositionChanged += OnPositionChanged;
		_player.StateChanged += OnStateChanged;
	}

	public void Detach()
	{
		if (_player is null) return;
		_player.MediaOpened -= OnMediaOpened;
		_player.MediaEnded -= OnMediaEnded;
		_player.MediaFailed -= OnMediaFailed;
		_player.PositionChanged -= OnPositionChanged;
		_player.StateChanged -= OnStateChanged;
		_player = null;
	}

	public void ApplyKeepScreenOn()
	{
		if (_player is not null) _player.ShouldKeepScreenOn = AppSettings.KeepScreenOn;
	}

	// ================================================================ oynatma

	/// <summary>Listeden bir öğe seçildiğinde: listeyi çalma sırası yapar ve öğeyi başlatır.</summary>
	public void PlayFromList(IEnumerable<MediaItem> list, MediaItem start)
	{
		_queue = list.ToList();
		if (!_queue.Contains(start))
			_queue.Insert(0, start);
		RebuildOrder(_queue.IndexOf(start));
		OnPropertyChanged(nameof(Queue));
		Load(start, autoPlay: true);
	}

	/// <summary>Uygulama açılışında son oynatılan öğeyi duraklatılmış olarak hazırlar.</summary>
	public void Prepare(IEnumerable<MediaItem> list, MediaItem item)
	{
		_queue = list.ToList();
		if (!_queue.Contains(item))
			_queue.Insert(0, item);
		RebuildOrder(_queue.IndexOf(item));
		OnPropertyChanged(nameof(Queue));
		Load(item, autoPlay: false);
	}

	/// <summary>Başka uygulamadan gelen dosyayı çalar (kütüphanede olmayabilir).</summary>
	public void PlayExternal(string uri, bool isVideo, MediaItem? known)
	{
		if (known is not null)
		{
			PlayFromList(_queue.Count > 0 ? _queue : new List<MediaItem> { known }, known);
			return;
		}

		if (_player is null) return;
		SaveProgress(force: true);
		_loadVersion++;
		_externalTitle = Path.GetFileName(System.Uri.UnescapeDataString(uri));
		_externalIsVideo = isVideo;
		Current = null;
		OnPropertyChanged(nameof(Title));
		OnPropertyChanged(nameof(IsVideo));
		OnPropertyChanged(nameof(IsAudio));
		OnPropertyChanged(nameof(HasMedia));
		_pendingAutoPlay = true;
		Position = TimeSpan.Zero;
		Duration = TimeSpan.Zero;
		_player.ShouldAutoPlay = true;
		_player.MetadataTitle = _externalTitle;
		_player.MetadataArtist = "Aurora Media Player";
		_player.Source = MediaSource.FromUri(uri);
	}

	void Load(MediaItem item, bool autoPlay)
	{
		if (_player is null) return;

		SaveProgress(force: true);
		_externalTitle = "";
		Current = item;
		_pendingAutoPlay = autoPlay;
		Position = TimeSpan.FromMilliseconds(AppSettings.ResumePlayback ? item.LastPositionMs : 0);
		Duration = item.Duration;

		var version = ++_loadVersion;
		_ = OpenSourceAsync(item, autoPlay, version);

		if (autoPlay)
		{
			item.LastPlayed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			item.PlayCount++;
			_ = _library.SaveAsync(item);
		}
		AppSettings.LastMediaKey = item.Key;
	}

	/// <summary>Gerçek oynatma adresini bulur (iOS'ta iCloud videosu indirilebilir) ve oynatıcıya verir.</summary>
	async Task OpenSourceAsync(MediaItem item, bool autoPlay, int version)
	{
		string? uri;
		IsBuffering = true;
		try
		{
			uri = await _scanner.ResolvePlaybackUriAsync(item);
		}
		catch
		{
			uri = null;
		}

		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			// Bu arada kullanıcı başka bir dosya seçtiyse eski isteği bırak
			if (version != _loadVersion || _player is null)
				return;

			IsBuffering = false;
			if (string.IsNullOrEmpty(uri))
			{
				IsPlaying = false;
				ErrorOccurred?.Invoke(this, $"Dosya açılamadı: {item.FileName}\nDosya silinmiş, taşınmış ya da erişim izni kaldırılmış olabilir.");
				return;
			}

			_player.ShouldAutoPlay = autoPlay && !(AppSettings.ResumePlayback && ShouldResume(item));
			_player.MetadataTitle = item.Title;
			_player.MetadataArtist = string.IsNullOrWhiteSpace(item.Artist) ? "Aurora Media Player" : item.Artist;
			_player.MetadataArtworkUrl = item.HasThumb ? item.ThumbPath! : "";
			_player.Source = MediaSource.FromUri(uri);
		});
	}

	static bool ShouldResume(MediaItem item) =>
		item.LastPositionMs > 5000 && item.DurationMs > 0 && item.LastPositionMs < item.DurationMs - 8000;

	public void TogglePlay()
	{
		if (_player is null) return;

		if (!HasMedia)
		{
			// Hiçbir şey seçilmediyse ilk öğeyi başlat
			var first = _queue.FirstOrDefault() ?? _library.All.FirstOrDefault();
			if (first is not null)
				PlayFromList(_queue.Count > 0 ? _queue : _library.All, first);
			return;
		}

		if (IsPlaying)
		{
			_player.Pause();
			SaveProgress(force: true);
		}
		else
		{
			if (_current is not null && _current.LastPlayed == 0)
			{
				_current.LastPlayed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
				_current.PlayCount++;
			}
			if (Duration > TimeSpan.Zero && Position >= Duration - TimeSpan.FromMilliseconds(500))
				_ = _player.SeekTo(TimeSpan.Zero);
			_player.Play();
		}
	}

	public void Next(bool auto = false)
	{
		if (_order.Count == 0) return;

		if (auto && Repeat == RepeatMode.One)
		{
			_ = RestartAsync();
			return;
		}

		var nextPos = _orderPos + 1;
		if (nextPos >= _order.Count)
		{
			if (Repeat == RepeatMode.All || !auto)
			{
				if (Shuffle) ReshuffleKeepingFirst(-1);
				nextPos = 0;
			}
			else
			{
				SaveProgress(force: true);
				return; // liste bitti
			}
		}

		_orderPos = nextPos;
		Load(_queue[_order[_orderPos]], autoPlay: true);
	}

	public void Previous()
	{
		if (_order.Count == 0) return;

		if (Position > TimeSpan.FromSeconds(3))
		{
			_ = _player?.SeekTo(TimeSpan.Zero);
			return;
		}

		var prev = _orderPos - 1;
		if (prev < 0) prev = _order.Count - 1;
		_orderPos = prev;
		Load(_queue[_order[_orderPos]], autoPlay: true);
	}

	public async Task SkipAsync(int seconds)
	{
		if (_player is null || !HasMedia) return;
		var target = Position + TimeSpan.FromSeconds(seconds);
		if (target < TimeSpan.Zero) target = TimeSpan.Zero;
		if (Duration > TimeSpan.Zero && target > Duration) target = Duration - TimeSpan.FromMilliseconds(300);
		Position = target;
		await _player.SeekTo(target);
	}

	public async Task SeekToFractionAsync(double fraction)
	{
		if (_player is null || Duration <= TimeSpan.Zero) return;
		var target = TimeSpan.FromMilliseconds(Duration.TotalMilliseconds * Math.Clamp(fraction, 0, 1));
		Position = target;
		await _player.SeekTo(target);
	}

	/// <summary>Sürükleme sırasında yalnızca zaman yazısını güncelle.</summary>
	public void PreviewFraction(double fraction)
	{
		if (Duration <= TimeSpan.Zero) return;
		_position = TimeSpan.FromMilliseconds(Duration.TotalMilliseconds * Math.Clamp(fraction, 0, 1));
		OnPropertyChanged(nameof(PositionText));
		OnPropertyChanged(nameof(InfoText));
	}

	async Task RestartAsync()
	{
		if (_player is null) return;
		await _player.SeekTo(TimeSpan.Zero);
		_player.Play();
	}

	public void CycleRepeat() =>
		Repeat = Repeat switch
		{
			RepeatMode.Off => RepeatMode.All,
			RepeatMode.All => RepeatMode.One,
			_ => RepeatMode.Off,
		};

	public void ToggleMute() => IsMuted = !IsMuted;

	public async Task ToggleFavoriteAsync(MediaItem? item = null)
	{
		item ??= _current;
		if (item is null) return;
		item.IsFavorite = !item.IsFavorite;
		await _library.SaveAsync(item);
		if (item == _current)
		{
			OnPropertyChanged(nameof(IsFavorite));
			OnPropertyChanged(nameof(FavoriteIcon));
		}
	}

	public void RefreshFavorite()
	{
		OnPropertyChanged(nameof(IsFavorite));
		OnPropertyChanged(nameof(FavoriteIcon));
	}

	public void Stop()
	{
		SaveProgress(force: true);
		_player?.Stop();
	}

	/// <summary>Sıradan bir öğeyi seçip çalar.</summary>
	public void PlayQueueItem(MediaItem item)
	{
		var idx = _queue.IndexOf(item);
		if (idx < 0) return;
		var pos = _order.IndexOf(idx);
		if (pos < 0) return;
		_orderPos = pos;
		Load(item, autoPlay: true);
	}

	/// <summary>Çalma sırasını (karıştırma durumuna göre) gerçek çalınma düzeninde döndürür.</summary>
	public List<MediaItem> OrderedQueue() => _order.Select(i => _queue[i]).ToList();

	public void NotifyArtworkMaybeChanged(MediaItem item)
	{
		if (item != _current) return;
		OnPropertyChanged(nameof(ArtworkPath));
		OnPropertyChanged(nameof(HasArtwork));
	}

	// ================================================================ sıra düzeni

	void RebuildOrder(int startIndex)
	{
		_order = Enumerable.Range(0, _queue.Count).ToList();
		if (_queue.Count == 0)
		{
			_orderPos = -1;
			return;
		}

		if (Shuffle)
		{
			ReshuffleKeepingFirst(startIndex);
			_orderPos = 0;
		}
		else
		{
			_orderPos = Math.Max(0, startIndex);
		}
	}

	void ReshuffleKeepingFirst(int firstIndex)
	{
		var rest = Enumerable.Range(0, _queue.Count).Where(i => i != firstIndex).OrderBy(_ => _rng.Next()).ToList();
		if (firstIndex >= 0) rest.Insert(0, firstIndex);
		_order = rest;
	}

	// ================================================================ olaylar

	void OnMediaOpened(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(async () =>
	{
		if (_player is null) return;
		if (_player.Duration > TimeSpan.Zero)
			Duration = _player.Duration;
		OnPropertyChanged(nameof(InfoText));

		var item = _current;
		if (item is not null)
		{
			// Gerçek çözünürlük ve süreyi kaydet
			if (_player.MediaWidth > 0 && item.Width == 0)
			{
				item.Width = _player.MediaWidth;
				item.Height = _player.MediaHeight;
			}
			if (item.DurationMs == 0 && Duration > TimeSpan.Zero)
				item.DurationMs = (long)Duration.TotalMilliseconds;

			if (AppSettings.ResumePlayback && ShouldResume(item))
			{
				var pos = TimeSpan.FromMilliseconds(item.LastPositionMs);
				await _player.SeekTo(pos);
				Position = pos;
				if (_pendingAutoPlay) _player.Play();
			}
		}
		_pendingAutoPlay = false;
	});

	void OnMediaEnded(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(() =>
	{
		if (_current is not null)
		{
			_current.LastPositionMs = 0;
			_current.RefreshComputed();
			_ = _library.SaveAsync(_current);
		}

		if (AppSettings.AutoPlayNext || Repeat == RepeatMode.One)
			Next(auto: true);
		else
			IsPlaying = false;
	});

	void OnMediaFailed(object? sender, MediaFailedEventArgs e) => MainThread.BeginInvokeOnMainThread(() =>
	{
		IsPlaying = false;
		ErrorOccurred?.Invoke(this, $"Dosya oynatılamadı: {Title}\n{e.ErrorMessage}");
	});

	void OnPositionChanged(object? sender, MediaPositionChangedEventArgs e) => MainThread.BeginInvokeOnMainThread(() =>
	{
		if (_player is not null && _player.Duration > TimeSpan.Zero && _player.Duration != Duration)
			Duration = _player.Duration;

		if (!IsUserSeeking)
			Position = e.Position;

		if ((DateTime.UtcNow - _lastSave).TotalSeconds >= 5)
			SaveProgress(force: false);
	});

	void OnStateChanged(object? sender, MediaStateChangedEventArgs e) => MainThread.BeginInvokeOnMainThread(() =>
	{
		IsPlaying = e.NewState == MediaElementState.Playing;
		IsBuffering = e.NewState is MediaElementState.Buffering or MediaElementState.Opening;
		if (e.NewState == MediaElementState.Paused || e.NewState == MediaElementState.Stopped)
			SaveProgress(force: true);
	});

	public void SaveProgress(bool force)
	{
		_lastSave = DateTime.UtcNow;
		var item = _current;
		if (item is null || Position <= TimeSpan.Zero) return;

		var ms = (long)Position.TotalMilliseconds;
		if (!force && Math.Abs(ms - item.LastPositionMs) < 2000) return;

		item.LastPositionMs = ms;
		item.RefreshComputed();
		_ = _library.SaveAsync(item);
	}
}
