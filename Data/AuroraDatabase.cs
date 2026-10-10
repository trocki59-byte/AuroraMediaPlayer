using AuroraMediaPlayer.Models;
using SQLite;

namespace AuroraMediaPlayer.Data;

/// <summary>
/// Uygulama ilk açıldığında otomatik oluşturulan SQLite veritabanı (aurora.db3).
/// Medya kütüphanesi + kullanıcı verileri (favoriler, geçmiş, kaldığı yer) burada tutulur.
/// </summary>
public sealed class AuroraDatabase
{
	public const string FileName = "aurora.db3";

	const SQLiteOpenFlags Flags =
		SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache;

	readonly SemaphoreSlim _initLock = new(1, 1);
	SQLiteAsyncConnection? _db;

	public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, FileName);

	async Task<SQLiteAsyncConnection> GetAsync()
	{
		if (_db is not null)
			return _db;

		await _initLock.WaitAsync();
		try
		{
			if (_db is null)
			{
				var db = new SQLiteAsyncConnection(DatabasePath, Flags);
				await db.EnableWriteAheadLoggingAsync();
				await db.CreateTableAsync<MediaItem>();
				_db = db;
			}
			return _db;
		}
		finally
		{
			_initLock.Release();
		}
	}

	public async Task<List<MediaItem>> GetAllAsync()
	{
		var db = await GetAsync();
		return await db.Table<MediaItem>().ToListAsync();
	}

	public async Task SaveAsync(MediaItem item)
	{
		var db = await GetAsync();
		await db.InsertOrReplaceAsync(item);
	}

	/// <summary>
	/// Taramadan gelen listeyi veritabanıyla birleştirir:
	/// yeni dosyalar eklenir, değişenler güncellenir (favori/geçmiş korunur), silinen dosyalar kaldırılır.
	/// </summary>
	public async Task<List<MediaItem>> MergeScanAsync(IReadOnlyList<MediaItem> scanned)
	{
		var db = await GetAsync();
		var existing = (await db.Table<MediaItem>().ToListAsync()).ToDictionary(m => m.Key);
		var scannedKeys = new HashSet<string>();

		foreach (var item in scanned)
		{
			scannedKeys.Add(item.Key);
			if (existing.TryGetValue(item.Key, out var old))
			{
				item.IsFavorite = old.IsFavorite;
				item.LastPlayed = old.LastPlayed;
				item.PlayCount = old.PlayCount;
				item.LastPositionMs = old.LastPositionMs;
				// Dosya değişmediyse eski küçük resmi koru
				if (old.SizeBytes == item.SizeBytes)
				{
					item.ThumbPath = old.ThumbPath;
					item.ThumbTried = old.ThumbTried;
				}
			}
		}

		var removed = existing.Keys.Where(k => !scannedKeys.Contains(k)).ToList();

		await db.RunInTransactionAsync(conn =>
		{
			foreach (var item in scanned)
				conn.InsertOrReplace(item);
			foreach (var key in removed)
				conn.Delete<MediaItem>(key);
		});

		foreach (var key in removed)
		{
			var thumb = existing[key].ThumbPath;
			if (!string.IsNullOrEmpty(thumb))
			{
				try { File.Delete(thumb); } catch { /* yoksay */ }
			}
		}

		return scanned.ToList();
	}

	public async Task ClearHistoryAsync()
	{
		var db = await GetAsync();
		await db.ExecuteAsync("UPDATE Media SET LastPlayed = 0, PlayCount = 0, LastPositionMs = 0");
	}

	public async Task ClearFavoritesAsync()
	{
		var db = await GetAsync();
		await db.ExecuteAsync("UPDATE Media SET IsFavorite = 0");
	}
}
