using Android.Content;
using Android.Database;
using Android.Graphics;
using Android.Provider;
using AuroraMediaPlayer.Models;
using AuroraMediaPlayer.Services;
using AUri = Android.Net.Uri;

namespace AuroraMediaPlayer.Droid;

/// <summary>
/// MediaStore üzerinden cihazdaki tüm video ve müzik dosyalarını otomatik bulur.
/// Depolama klasörlerini tek tek gezmeye gerek kalmaz; SD kart dahil tüm birimler taranır.
/// </summary>
public sealed class AndroidMediaScanner : IMediaScanner
{
	static ContentResolver Resolver => Platform.AppContext.ContentResolver!;

	public Task<List<MediaItem>> ScanAsync(CancellationToken token = default)
	{
		var list = new List<MediaItem>();
		ScanVideos(list, token);
		ScanAudio(list, token);
		return Task.FromResult(list);
	}

	// -------------------------------------------------------------- video

	static void ScanVideos(List<MediaItem> list, CancellationToken token)
	{
		var baseUri = OperatingSystem.IsAndroidVersionAtLeast(29)
			? MediaStore.Video.Media.GetContentUri(MediaStore.VolumeExternal)!
			: MediaStore.Video.Media.ExternalContentUri!;

		var projection = new List<string>
		{
			"_id", "_display_name", "title", "duration", "_size", "date_added",
			"width", "height", "mime_type", "bucket_display_name", "_data"
		};
		if (OperatingSystem.IsAndroidVersionAtLeast(29))
			projection.Add("relative_path");

		using var cursor = Query(baseUri, projection.ToArray(), null, "date_added DESC");
		if (cursor is null)
			return;

		var c = new Cols(cursor);
		while (cursor.MoveToNext())
		{
			if (token.IsCancellationRequested) break;

			var id = cursor.GetLong(c["_id"]);
			var item = new MediaItem
			{
				Key = "v" + id,
				StoreId = id,
				IsVideo = true,
				Uri = ContentUris.WithAppendedId(baseUri, id).ToString()!,
				DisplayName = c.Str(cursor, "_display_name"),
				Title = CleanTitle(c.Str(cursor, "title"), c.Str(cursor, "_display_name")),
				DurationMs = c.Long(cursor, "duration"),
				SizeBytes = c.Long(cursor, "_size"),
				DateAdded = c.Long(cursor, "date_added"),
				Width = (int)c.Long(cursor, "width"),
				Height = (int)c.Long(cursor, "height"),
				MimeType = c.Str(cursor, "mime_type"),
			};
			FillFolder(item, c, cursor);
			list.Add(item);
		}
	}

	// -------------------------------------------------------------- müzik

	static void ScanAudio(List<MediaItem> list, CancellationToken token)
	{
		var baseUri = OperatingSystem.IsAndroidVersionAtLeast(29)
			? MediaStore.Audio.Media.GetContentUri(MediaStore.VolumeExternal)!
			: MediaStore.Audio.Media.ExternalContentUri!;

		var projection = new List<string>
		{
			"_id", "_display_name", "title", "artist", "album", "duration", "_size",
			"date_added", "mime_type", "_data"
		};
		if (OperatingSystem.IsAndroidVersionAtLeast(29))
		{
			projection.Add("relative_path");
			projection.Add("bucket_display_name");
		}

		// Zil sesi / bildirim seslerini ayıkla, yalnızca müzik
		using var cursor = Query(baseUri, projection.ToArray(), "is_music != 0", "date_added DESC");
		if (cursor is null)
			return;

		var c = new Cols(cursor);
		while (cursor.MoveToNext())
		{
			if (token.IsCancellationRequested) break;

			var duration = c.Long(cursor, "duration");
			if (duration > 0 && duration < 5000)
				continue; // 5 sn altı kısa sesleri atla

			var id = cursor.GetLong(c["_id"]);
			var item = new MediaItem
			{
				Key = "a" + id,
				StoreId = id,
				IsVideo = false,
				Uri = ContentUris.WithAppendedId(baseUri, id).ToString()!,
				DisplayName = c.Str(cursor, "_display_name"),
				Title = CleanTitle(c.Str(cursor, "title"), c.Str(cursor, "_display_name")),
				Artist = c.Str(cursor, "artist"),
				Album = c.Str(cursor, "album"),
				DurationMs = duration,
				SizeBytes = c.Long(cursor, "_size"),
				DateAdded = c.Long(cursor, "date_added"),
				MimeType = c.Str(cursor, "mime_type"),
			};
			if (item.Artist == "<unknown>") item.Artist = "";
			if (item.Album == "<unknown>") item.Album = "";
			FillFolder(item, c, cursor);
			list.Add(item);
		}
	}

	public Task<string?> ResolvePlaybackUriAsync(MediaItem item, CancellationToken token = default)
		=> Task.FromResult<string?>(item.Uri);

	// -------------------------------------------------------------- küçük resim

	public async Task<bool> CreateThumbnailAsync(MediaItem item, string targetPath, CancellationToken token = default)
	{
		return await Task.Run(() =>
		{
			Bitmap? bmp = null;
			try
			{
				var uri = AUri.Parse(item.Uri)!;
				if (OperatingSystem.IsAndroidVersionAtLeast(29))
				{
					var size = item.IsVideo ? new global::Android.Util.Size(384, 216) : new global::Android.Util.Size(256, 256);
					try
					{
						bmp = Resolver.LoadThumbnail(uri, size, null);
					}
					catch (Java.IO.FileNotFoundException)
					{
						bmp = null; // kapak resmi yok
					}
				}
				else if (item.IsVideo)
				{
#pragma warning disable CA1422, CS0618
					bmp = MediaStore.Video.Thumbnails.GetThumbnail(Resolver, item.StoreId, ThumbnailKind.MiniKind, null);
#pragma warning restore CA1422, CS0618
				}
				else
				{
					bmp = LoadEmbeddedArt(uri);
				}

				if (bmp is null)
					return false;

				using var fs = File.Create(targetPath);
				bmp.Compress(Bitmap.CompressFormat.Jpeg!, 82, fs);
				return true;
			}
			catch
			{
				return false;
			}
			finally
			{
				bmp?.Recycle();
				bmp?.Dispose();
			}
		}, token);
	}

	static Bitmap? LoadEmbeddedArt(AUri uri)
	{
		using var mmr = new global::Android.Media.MediaMetadataRetriever();
		try
		{
			mmr.SetDataSource(Platform.AppContext, uri);
			var art = mmr.GetEmbeddedPicture();
			return art is null ? null : BitmapFactory.DecodeByteArray(art, 0, art.Length);
		}
		catch
		{
			return null;
		}
		finally
		{
			mmr.Release();
		}
	}

	// -------------------------------------------------------------- yardımcılar

	static ICursor? Query(AUri uri, string[] projection, string? selection, string sort)
	{
		try
		{
			return Resolver.Query(uri, projection, selection, null, sort);
		}
		catch (Java.Lang.SecurityException)
		{
			return null; // izin yok
		}
	}

	static void FillFolder(MediaItem item, Cols c, ICursor cursor)
	{
		var bucket = c.Str(cursor, "bucket_display_name");
		var relative = c.Str(cursor, "relative_path");
		var data = c.Str(cursor, "_data");

		string folderPath;
		if (!string.IsNullOrEmpty(relative))
			folderPath = relative.TrimEnd('/');
		else if (!string.IsNullOrEmpty(data))
			folderPath = System.IO.Path.GetDirectoryName(data) ?? "";
		else
			folderPath = bucket;

		if (string.IsNullOrEmpty(bucket))
			bucket = System.IO.Path.GetFileName(folderPath.TrimEnd('/'));

		item.Folder = string.IsNullOrEmpty(bucket) ? "Dahili Depolama" : bucket;
		item.FolderPath = string.IsNullOrEmpty(folderPath) ? item.Folder : folderPath;
	}

	static string CleanTitle(string title, string displayName)
	{
		if (!string.IsNullOrWhiteSpace(title))
			return title.Trim();
		return System.IO.Path.GetFileNameWithoutExtension(displayName);
	}

	/// <summary>Sütun indekslerini bir kere bulur; olmayan sütunlar güvenle boş döner.</summary>
	sealed class Cols
	{
		readonly Dictionary<string, int> _map = [];

		public Cols(ICursor cursor)
		{
			var names = cursor.GetColumnNames() ?? [];
			for (int i = 0; i < names.Length; i++)
				_map[names[i]] = i;
		}

		public int this[string name] => _map.TryGetValue(name, out var i) ? i : -1;

		public string Str(ICursor cursor, string name)
		{
			var i = this[name];
			if (i < 0 || cursor.IsNull(i)) return "";
			return cursor.GetString(i) ?? "";
		}

		public long Long(ICursor cursor, string name)
		{
			var i = this[name];
			if (i < 0 || cursor.IsNull(i)) return 0;
			try { return cursor.GetLong(i); } catch { return 0; }
		}
	}
}
