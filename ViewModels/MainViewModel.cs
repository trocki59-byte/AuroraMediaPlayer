using System.Collections.ObjectModel;
using System.Windows.Input;
using AuroraMediaPlayer.Models;
using AuroraMediaPlayer.Services;

namespace AuroraMediaPlayer.ViewModels;

public sealed class MainViewModel : ObservableObject
{
	readonly MediaLibrary _library;
	readonly IPlatformUi _ui;

	LibraryTab _tab = LibraryTab.Home;
	FolderInfo? _openFolder;
	ObservableCollection<MediaItem> _items = [];
	ObservableCollection<FolderInfo> _folders = [];
	string _searchText = "";
	bool _isSearchVisible;
	bool _isBusy;
	bool _hasPermission = true;
	bool _initialized;

	public MainViewModel(MediaLibrary library, PlayerController player, IPlatformUi ui)
	{
		_library = library;
		_ui = ui;
		Player = player;

		_library.Changed += (_, _) => Refresh();
		_library.ThumbnailReady += (_, item) =>
		{
			Player.NotifyArtworkMaybeChanged(item);
			if (_tab == LibraryTab.Folders && _openFolder is null)
				Refresh();
		};
		Player.CurrentChanged += (_, _) =>
		{
			if (_tab is LibraryTab.Recent or LibraryTab.Queue or LibraryTab.Home)
				Refresh();
		};

		SelectTabCommand = new Command<string>(t =>
		{
			if (Enum.TryParse<LibraryTab>(t, out var tab))
				SelectTab(tab);
		});
		PlayItemCommand = new Command<MediaItem>(PlayItem);
		FavoriteItemCommand = new Command<MediaItem>(async m =>
		{
			await Player.ToggleFavoriteAsync(m);
			OnPropertyChanged(nameof(FavoriteCount));
			if (_tab == LibraryTab.Favorites) Refresh();
		});
		OpenFolderCommand = new Command<FolderInfo>(f =>
		{
			_openFolder = f;
			Refresh();
		});
		BackCommand = new Command(() => GoBack());
		RescanCommand = new Command(async () => await RescanAsync());
		ToggleSearchCommand = new Command(() => IsSearchVisible = !IsSearchVisible);
		GrantPermissionCommand = new Command(async () => await RequestPermissionAndScanAsync(openSettingsIfDenied: true));
		ClearHistoryCommand = new Command(async () => await _library.ClearHistoryAsync());
		ClearFavoritesCommand = new Command(async () =>
		{
			await _library.ClearFavoritesAsync();
			Player.RefreshFavorite(); // güncel öğenin kalp ikonunu tazele
		});
		ClearThumbsCommand = new Command(() => _library.ClearThumbnailCache());
		ImportFilesCommand = new Command(async () => await ImportFilesAsync());
		OpenAppSettingsCommand = new Command(() => _ui.OpenAppSettings());
	}

	public PlayerController Player { get; }

	// ================================================================ komutlar

	public ICommand SelectTabCommand { get; }
	public ICommand PlayItemCommand { get; }
	public ICommand FavoriteItemCommand { get; }
	public ICommand OpenFolderCommand { get; }
	public ICommand BackCommand { get; }
	public ICommand RescanCommand { get; }
	public ICommand ToggleSearchCommand { get; }
	public ICommand GrantPermissionCommand { get; }
	public ICommand ClearHistoryCommand { get; }
	public ICommand ClearFavoritesCommand { get; }
	public ICommand ClearThumbsCommand { get; }
	public ICommand ImportFilesCommand { get; }
	public ICommand OpenAppSettingsCommand { get; }

	/// <summary>iPhone'da "Dosya Ekle" görünür (Android tüm dosyaları zaten tarar).</summary>
	public bool IsImportSupported => MediaImport.IsSupported;

	public string PermissionHint => MediaImport.IsSupported
		? "Fotoğraflar ve Müzik erişimini iPhone Ayarlar › Aurora bölümünden değiştirebilirsiniz."
		: "Medya erişim iznini Android Ayarlar › Uygulamalar › Aurora bölümünden değiştirebilirsiniz.";

	// ================================================================ sekme durumu

	public LibraryTab Tab => _tab;

	public bool IsHomeTab => _tab == LibraryTab.Home;
	public bool IsVideosTab => _tab == LibraryTab.Videos;
	public bool IsMusicTab => _tab == LibraryTab.Music;
	public bool IsFoldersTab => _tab == LibraryTab.Folders;
	public bool IsFavoritesTab => _tab == LibraryTab.Favorites;
	public bool IsRecentTab => _tab == LibraryTab.Recent;
	public bool IsSettingsTab => _tab == LibraryTab.Settings;
	public bool IsQueueTab => _tab == LibraryTab.Queue;

	public bool ShowFolderList => _tab == LibraryTab.Folders && _openFolder is null;
	public bool ShowItemList => !IsSettingsTab && !ShowFolderList;
	public bool ShowBack => (_tab == LibraryTab.Folders && _openFolder is not null) || _tab == LibraryTab.Queue;
	public bool ShowSortButton => ShowItemList && _tab is not (LibraryTab.Recent or LibraryTab.Queue);
	public bool ShowHomeStats => IsHomeTab && _library.All.Count > 0 && string.IsNullOrWhiteSpace(SearchText);

	public string SectionTitle => _tab switch
	{
		LibraryTab.Home => "Son Eklenenler",
		LibraryTab.Videos => "Videolar",
		LibraryTab.Music => "Müzikler",
		LibraryTab.Folders => _openFolder?.Name ?? "Klasörler",
		LibraryTab.Favorites => "Favoriler",
		LibraryTab.Recent => "Son Oynatılanlar",
		LibraryTab.Settings => "Ayarlar",
		LibraryTab.Queue => "Oynatma Sırası",
		_ => "",
	};

	public string SectionCount => _tab switch
	{
		LibraryTab.Settings => "",
		LibraryTab.Folders when _openFolder is null => $"{Folders.Count} klasör",
		_ => $"{Items.Count} öğe",
	};

	public ObservableCollection<MediaItem> Items
	{
		get => _items;
		private set => SetProperty(ref _items, value);
	}

	public ObservableCollection<FolderInfo> Folders
	{
		get => _folders;
		private set => SetProperty(ref _folders, value);
	}

	public int VideoCount => _library.All.Count(m => m.IsVideo);
	public int MusicCount => _library.All.Count(m => !m.IsVideo);
	public int FavoriteCount => _library.All.Count(m => m.IsFavorite);

	public string SearchText
	{
		get => _searchText;
		set
		{
			if (SetProperty(ref _searchText, value ?? ""))
				Refresh();
		}
	}

	public bool IsSearchVisible
	{
		get => _isSearchVisible;
		set
		{
			if (SetProperty(ref _isSearchVisible, value) && !value && SearchText.Length > 0)
				SearchText = "";
		}
	}

	public bool IsBusy
	{
		get => _isBusy;
		private set
		{
			if (SetProperty(ref _isBusy, value))
				OnPropertyChanged(nameof(EmptyTitle));
		}
	}

	public bool HasPermission
	{
		get => _hasPermission;
		private set
		{
			if (SetProperty(ref _hasPermission, value))
			{
				OnPropertyChanged(nameof(EmptyTitle));
				OnPropertyChanged(nameof(EmptyText));
				OnPropertyChanged(nameof(EmptyButtonText));
			}
		}
	}

	public bool IsEmpty => ShowItemList ? Items.Count == 0 : ShowFolderList && Folders.Count == 0;

	public string EmptyTitle =>
		!HasPermission ? "Medya izni gerekli"
		: IsBusy ? "Cihaz taranıyor…"
		: !string.IsNullOrWhiteSpace(SearchText) ? "Sonuç bulunamadı"
		: _tab switch
		{
			LibraryTab.Favorites => "Henüz favori yok",
			LibraryTab.Recent => "Henüz bir şey oynatılmadı",
			LibraryTab.Queue => "Oynatma sırası boş",
			_ => "Medya bulunamadı",
		};

	public string EmptyText =>
		!HasPermission ? "Aurora'nın video ve müziklerinizi bulabilmesi için cihazdaki medya dosyalarına erişim izni verin."
		: !string.IsNullOrWhiteSpace(SearchText) ? $"\"{SearchText}\" ile eşleşen dosya yok."
		: _tab switch
		{
			LibraryTab.Favorites => "Bir dosyayı favorilere eklemek için yanındaki kalbe dokunun.",
			LibraryTab.Recent => "İzlediğiniz ve dinlediğiniz dosyalar burada listelenir.",
			LibraryTab.Queue => "Bir listeden dosya seçtiğinizde sıra oluşur.",
			_ => MediaImport.IsSupported
				? "Fotoğraflar'daki videolar ve Müzik arşivindeki şarkılar otomatik listelenir. Diğer dosyaları \"Dosya Ekle\" ile ya da Dosyalar uygulamasında iPhone'umda › Aurora klasörüne kopyalayarak ekleyin."
				: "Cihazda video veya müzik dosyası bulunamadı. Yeniden taramayı deneyin.",
		};

	public string EmptyButtonText => HasPermission ? "Yeniden Tara" : "İzin Ver";
	public bool ShowEmptyImport => IsImportSupported && ShowEmptyButton;

	public bool ShowEmptyButton => !IsBusy && string.IsNullOrWhiteSpace(SearchText) &&
								   _tab is LibraryTab.Home or LibraryTab.Videos or LibraryTab.Music or LibraryTab.Folders;

	// ================================================================ ayarlar

	public bool ResumePlayback
	{
		get => AppSettings.ResumePlayback;
		set { AppSettings.ResumePlayback = value; OnPropertyChanged(); }
	}

	public bool AutoPlayNext
	{
		get => AppSettings.AutoPlayNext;
		set { AppSettings.AutoPlayNext = value; OnPropertyChanged(); }
	}

	public bool BackgroundAudio
	{
		get => AppSettings.BackgroundAudio;
		set { AppSettings.BackgroundAudio = value; OnPropertyChanged(); }
	}

	public bool KeepScreenOn
	{
		get => AppSettings.KeepScreenOn;
		set { AppSettings.KeepScreenOn = value; Player.ApplyKeepScreenOn(); OnPropertyChanged(); }
	}

	public bool AutoRotateFullscreen
	{
		get => AppSettings.AutoRotateFullscreen;
		set { AppSettings.AutoRotateFullscreen = value; OnPropertyChanged(); }
	}

	public bool ScanOnStart
	{
		get => AppSettings.ScanOnStart;
		set { AppSettings.ScanOnStart = value; OnPropertyChanged(); }
	}

	public SortMode Sort
	{
		get => AppSettings.Sort;
		set { AppSettings.Sort = value; OnPropertyChanged(); OnPropertyChanged(nameof(SortText)); Refresh(); }
	}

	public bool SortDescending
	{
		get => AppSettings.SortDescending;
		set { AppSettings.SortDescending = value; OnPropertyChanged(); OnPropertyChanged(nameof(SortText)); Refresh(); }
	}

	public string SortText => (AppSettings.Sort switch
	{
		SortMode.Title => "Ada göre",
		SortMode.Duration => "Süreye göre",
		SortMode.Size => "Boyuta göre",
		_ => "Tarihe göre",
	}) + (AppSettings.SortDescending ? " ↓" : " ↑");

	public string LibraryInfo => $"{VideoCount} video  •  {MusicCount} müzik  •  {_library.Folders().Count} klasör";

	public string VersionText => $"Sürüm {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

	public string DatabaseInfo => $"Veritabanı: {Data.AuroraDatabase.FileName}";

	// ================================================================ işlemler

	public async Task InitializeAsync()
	{
		if (_initialized) return;
		_initialized = true;

		await _library.LoadAsync();
		RestoreLastSession();

		HasPermission = await MediaPermissions.HasMediaAsync();
		if (!HasPermission)
		{
			await RequestPermissionAndScanAsync(openSettingsIfDenied: false);
		}
		else
		{
			_library.StartThumbnailWorker();
			if (AppSettings.ScanOnStart || _library.All.Count == 0)
				await RescanAsync();
		}

		if (AppSettings.BackgroundAudio)
			await MediaPermissions.EnsureNotificationsAsync();
	}

	async Task RequestPermissionAndScanAsync(bool openSettingsIfDenied)
	{
		if (HasPermission)
		{
			await RescanAsync();
			return;
		}

		HasPermission = await MediaPermissions.EnsureMediaAsync();
		if (HasPermission)
			await RescanAsync();
		else if (openSettingsIfDenied)
			_ui.OpenAppSettings();
	}

	public async Task RescanAsync()
	{
		if (IsBusy) return;
		if (!HasPermission)
		{
			HasPermission = await MediaPermissions.HasMediaAsync();
			if (!HasPermission)
			{
				await RequestPermissionAndScanAsync(openSettingsIfDenied: true);
				return;
			}
		}

		IsBusy = true;
		Refresh();
		try
		{
			await _library.ScanAsync();
			if (Player.Current is null)
				RestoreLastSession();
		}
		finally
		{
			IsBusy = false;
			Refresh();
		}
	}

	/// <summary>iPhone: Dosyalar'dan video/müzik seçip Aurora klasörüne kopyalar, sonra tarar.</summary>
	public async Task<int> ImportFilesAsync()
	{
		if (IsBusy) return 0;
		int count;
		try
		{
			count = await MediaImport.PickAndImportAsync();
		}
		catch
		{
			count = 0;
		}
		if (count > 0)
			await RescanAsync();
		return count;
	}

	void RestoreLastSession()
	{
		if (Player.HasMedia) return;
		var last = _library.Find(AppSettings.LastMediaKey);
		if (last is null) return;
		var list = last.IsVideo ? _library.Videos : _library.Music;
		Player.Prepare(MediaLibrary.Sort(list, AppSettings.Sort, AppSettings.SortDescending), last);
	}

	public void SelectTab(LibraryTab tab)
	{
		if (_tab == tab && _openFolder is null)
			return;

		_tab = tab;
		_openFolder = null;
		IsSearchVisible = false;
		OnPropertyChanged(nameof(Tab));
		OnPropertyChanged(nameof(IsHomeTab));
		OnPropertyChanged(nameof(IsVideosTab));
		OnPropertyChanged(nameof(IsMusicTab));
		OnPropertyChanged(nameof(IsFoldersTab));
		OnPropertyChanged(nameof(IsFavoritesTab));
		OnPropertyChanged(nameof(IsRecentTab));
		OnPropertyChanged(nameof(IsSettingsTab));
		OnPropertyChanged(nameof(IsQueueTab));
		Refresh();
	}

	/// <summary>Geri tuşu: klasörden çık / sıradan çık. İşlendiyse true.</summary>
	public bool GoBack()
	{
		if (IsSearchVisible)
		{
			IsSearchVisible = false;
			return true;
		}
		if (_tab == LibraryTab.Folders && _openFolder is not null)
		{
			_openFolder = null;
			Refresh();
			return true;
		}
		if (_tab == LibraryTab.Queue)
		{
			SelectTab(Player.IsVideo ? LibraryTab.Videos : LibraryTab.Music);
			return true;
		}
		if (_tab != LibraryTab.Home)
		{
			SelectTab(LibraryTab.Home);
			return true;
		}
		return false;
	}

	public MediaItem? FindByUri(string uri) => _library.All.FirstOrDefault(m => m.Uri == uri);

	void PlayItem(MediaItem item)
	{
		if (_tab == LibraryTab.Queue)
		{
			Player.PlayQueueItem(item);
			return;
		}
		// Çalma sırası: ekrandaki liste (videolar videolarla, müzikler müziklerle devam eder)
		var list = Items.Where(m => m.IsVideo == item.IsVideo).ToList();
		Player.PlayFromList(list, item);
	}

	public void Refresh()
	{
		IEnumerable<MediaItem> source;
		var keepOrder = false;

		switch (_tab)
		{
			case LibraryTab.Videos:
				source = _library.Videos;
				break;
			case LibraryTab.Music:
				source = _library.Music;
				break;
			case LibraryTab.Favorites:
				source = _library.Favorites;
				break;
			case LibraryTab.Recent:
				source = _library.Recent;
				keepOrder = true;
				break;
			case LibraryTab.Queue:
				source = Player.OrderedQueue();
				keepOrder = true;
				break;
			case LibraryTab.Folders:
				if (_openFolder is null)
				{
					var folders = _library.Folders().AsEnumerable();
					if (!string.IsNullOrWhiteSpace(SearchText))
					{
						var q = SearchText.Trim();
						folders = folders.Where(f => f.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase));
					}
					Folders = new ObservableCollection<FolderInfo>(folders);
					source = [];
				}
				else
				{
					source = _library.InFolder(_openFolder.Path);
				}
				break;
			case LibraryTab.Settings:
				source = [];
				break;
			default: // Home
				if (string.IsNullOrWhiteSpace(SearchText))
				{
					// Önce yarım kalan videolar, sonra son eklenenler
					var cont = _library.ContinueWatching.ToList();
					source = cont.Concat(_library.RecentlyAdded.Where(m => !cont.Contains(m)));
					keepOrder = true;
				}
				else
				{
					source = _library.All;
				}
				break;
		}

		source = MediaLibrary.Filter(source, SearchText);
		if (!keepOrder)
			source = MediaLibrary.Sort(source, AppSettings.Sort, AppSettings.SortDescending);

		Items = new ObservableCollection<MediaItem>(source);

		OnPropertyChanged(nameof(SectionTitle));
		OnPropertyChanged(nameof(SectionCount));
		OnPropertyChanged(nameof(ShowFolderList));
		OnPropertyChanged(nameof(ShowItemList));
		OnPropertyChanged(nameof(ShowBack));
		OnPropertyChanged(nameof(ShowSortButton));
		OnPropertyChanged(nameof(ShowHomeStats));
		OnPropertyChanged(nameof(IsEmpty));
		OnPropertyChanged(nameof(EmptyTitle));
		OnPropertyChanged(nameof(EmptyText));
		OnPropertyChanged(nameof(EmptyButtonText));
		OnPropertyChanged(nameof(ShowEmptyButton));
		OnPropertyChanged(nameof(ShowEmptyImport));
		OnPropertyChanged(nameof(VideoCount));
		OnPropertyChanged(nameof(MusicCount));
		OnPropertyChanged(nameof(FavoriteCount));
		OnPropertyChanged(nameof(LibraryInfo));
	}
}
