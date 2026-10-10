using AuroraMediaPlayer.Data;
using AuroraMediaPlayer.Models;

namespace AuroraMediaPlayer.Services;

/// <summary>
/// Bellekteki medya kütüphanesi: veritabanından yükler, MediaStore'u tarar,
/// küçük resimleri arka planda üretir ve sorgu yardımcıları sunar.
/// </summary>
public sealed class MediaLibrary
{
	readonly AuroraDatabase _db;
	readonly IMediaScanner _scanner;
	CancellationTokenSource? _thumbCts;
	int _scanning;

	public MediaLibrary(AuroraDatabase db, IMediaScanner scanner)
	{
		_db = db;
		_scanner = scanner;
	}

	public IReadOnlyList<MediaItem> All { get; private set; } = [];

	public bool IsScanning => _scanning == 1;

	/// <summary>Kütüphane içeriği değiştiğinde (ana iş parçacığında) tetiklenir.</summary>
	public event EventHandler? Changed;

	/// <summary>Bir küçük resim hazır olduğunda tetiklenir.</summary>
	public event EventHandler<MediaItem>? ThumbnailReady;

	static string ThumbDir
	{
		get
		{
			var dir = Path.Combine(FileSystem.CacheDirectory, "thumbs");
			Directory.CreateDirectory(dir);
			return dir;
		}
	}

	public async Task LoadAsync()
	{
		All = await _db.GetAllAsync();
		RaiseChanged();
	}

	public async Task<int> ScanAsync()
	{
		if (Interlocked.Exchange(ref _scanning, 1) == 1)
			return All.Count;

		try
		{
			var scanned = await Task.Run(() => _scanner.ScanAsync());
			All = await _db.MergeScanAsync(scanned);
			RaiseChanged();
			StartThumbnailWorker();
			return All.Count;
		}
		finally
		{
			Interlocked.Exchange(ref _scanning, 0);
		}
	}

	public void StartThumbnailWorker()
	{
		_thumbCts?.Cancel();
		var cts = _thumbCts = new CancellationTokenSource();
		var pending = All.Where(m => !m.ThumbTried && !m.HasThumb)
						 .OrderByDescending(m => m.IsVideo)
						 .ThenByDescending(m => m.DateAdded)
						 .ToList();
		if (pending.Count == 0)
			return;

		_ = Task.Run(async () =>
		{
			foreach (var item in pending)
			{
				if (cts.IsCancellationRequested)
					break;

				var target = Path.Combine(ThumbDir, $"{item.Key}.jpg");
				bool ok;
				try
				{
					ok = await _scanner.CreateThumbnailAsync(item, target, cts.Token);
				}
				catch
				{
					ok = false;
				}

				item.ThumbTried = true;
				var path = ok ? target : null;
				await MainThread.InvokeOnMainThreadAsync(() => item.ThumbPath = path);
				await _db.SaveAsync(item);
				if (ok)
					MainThread.BeginInvokeOnMainThread(() => ThumbnailReady?.Invoke(this, item));
			}
		}, cts.Token);
	}

	public Task SaveAsync(MediaItem item) => _db.SaveAsync(item);

	public async Task ClearHistoryAsync()
	{
		await _db.ClearHistoryAsync();
		foreach (var m in All)
		{
			m.LastPlayed = 0;
			m.PlayCount = 0;
			m.LastPositionMs = 0;
			m.RefreshComputed();
		}
		RaiseChanged();
	}

	public async Task ClearFavoritesAsync()
	{
		await _db.ClearFavoritesAsync();
		foreach (var m in All)
			m.IsFavorite = false;
		RaiseChanged();
	}

	public void ClearThumbnailCache()
	{
		_thumbCts?.Cancel();
		try
		{
			foreach (var f in Directory.GetFiles(ThumbDir))
				File.Delete(f);
		}
		catch { /* yoksay */ }

		foreach (var m in All)
		{
			m.ThumbPath = null;
			m.ThumbTried = false;
		}
		StartThumbnailWorker();
	}

	// ----------------------------------------------------------------- sorgular

	public IEnumerable<MediaItem> Videos => All.Where(m => m.IsVideo);
	public IEnumerable<MediaItem> Music => All.Where(m => !m.IsVideo);
	public IEnumerable<MediaItem> Favorites => All.Where(m => m.IsFavorite);

	public IEnumerable<MediaItem> Recent =>
		All.Where(m => m.LastPlayed > 0).OrderByDescending(m => m.LastPlayed).Take(200);

	public IEnumerable<MediaItem> ContinueWatching =>
		All.Where(m => m.IsVideo && m.HasProgress).OrderByDescending(m => m.LastPlayed).Take(10);

	public IEnumerable<MediaItem> RecentlyAdded =>
		All.OrderByDescending(m => m.DateAdded).Take(100);

	public IEnumerable<MediaItem> InFolder(string folderPath) =>
		All.Where(m => m.FolderPath == folderPath);

	public List<FolderInfo> Folders() =>
		All.GroupBy(m => m.FolderPath)
		   .Select(g => new FolderInfo
		   {
			   Path = g.Key,
			   Name = g.First().Folder is { Length: > 0 } n ? n : "Dahili Depolama",
			   VideoCount = g.Count(x => x.IsVideo),
			   AudioCount = g.Count(x => !x.IsVideo),
			   CoverThumb = g.Where(x => x.HasThumb).Select(x => x.ThumbPath).FirstOrDefault(),
		   })
		   .OrderBy(f => f.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true))
		   .ToList();

	public MediaItem? Find(string key) => All.FirstOrDefault(m => m.Key == key);

	public static IEnumerable<MediaItem> Sort(IEnumerable<MediaItem> items, SortMode mode, bool descending)
	{
		var cmp = StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true);
		IOrderedEnumerable<MediaItem> ordered = mode switch
		{
			SortMode.Title => descending ? items.OrderByDescending(m => m.Title, cmp) : items.OrderBy(m => m.Title, cmp),
			SortMode.Duration => descending ? items.OrderByDescending(m => m.DurationMs) : items.OrderBy(m => m.DurationMs),
			SortMode.Size => descending ? items.OrderByDescending(m => m.SizeBytes) : items.OrderBy(m => m.SizeBytes),
			_ => descending ? items.OrderByDescending(m => m.DateAdded) : items.OrderBy(m => m.DateAdded),
		};
		return ordered;
	}

	public static IEnumerable<MediaItem> Filter(IEnumerable<MediaItem> items, string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return items;

		var culture = new System.Globalization.CultureInfo("tr-TR");
		var q = text.Trim().ToLower(culture);
		return items.Where(m =>
			m.Title.ToLower(culture).Contains(q) ||
			m.DisplayName.ToLower(culture).Contains(q) ||
			m.Artist.ToLower(culture).Contains(q) ||
			m.Album.ToLower(culture).Contains(q) ||
			m.Folder.ToLower(culture).Contains(q));
	}

	void RaiseChanged()
	{
		if (MainThread.IsMainThread)
			Changed?.Invoke(this, EventArgs.Empty);
		else
			MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke(this, EventArgs.Empty));
	}
}
