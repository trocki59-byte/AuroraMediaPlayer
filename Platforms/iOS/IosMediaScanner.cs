using AuroraMediaPlayer.Models;
using AuroraMediaPlayer.Services;
using AVFoundation;
using CoreGraphics;
using CoreMedia;
using Foundation;
using MediaPlayer;
using Photos;
using UIKit;

namespace AuroraMediaPlayer.Ios;

/// <summary>
/// iPhone / iPad medya kaynakları:
///  1) Fotoğraflar uygulamasındaki videolar (PhotoKit)
///  2) Cihazdaki müzik arşivi (MPMediaLibrary — Apple Music DRM'li / bulut şarkılar hariç)
///  3) Uygulamanın kendi klasörü: Dosyalar › iPhone'umda › Aurora (Dosya Ekle, AirDrop, Finder ile eklenenler)
/// </summary>
public sealed class IosMediaScanner : IMediaScanner
{
	public const string PhotoPrefix = "p:";
	public const string MusicPrefix = "m:";
	public const string DocPrefix = "d:";

	static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
		{ ".mp4", ".m4v", ".mov", ".3gp", ".mkv", ".webm", ".avi", ".ts", ".mts" };
	static readonly HashSet<string> AudioExt = new(StringComparer.OrdinalIgnoreCase)
		{ ".mp3", ".m4a", ".aac", ".wav", ".aif", ".aiff", ".caf", ".flac", ".alac", ".opus", ".ogg" };

	readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, MPMediaItem> _songs = new();

	public static string DocumentsDir => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

	public Task<List<MediaItem>> ScanAsync(CancellationToken token = default)
	{
		var list = new List<MediaItem>();
		try { ScanPhotos(list, token); } catch { /* izin yok ya da hata: diğer kaynaklarla devam */ }
		try { ScanMusic(list, token); } catch { }
		try { ScanDocuments(list, token); } catch { }
		return Task.FromResult(list);
	}

	// ============================================================ Fotoğraflar

	static void ScanPhotos(List<MediaItem> list, CancellationToken token)
	{
		var status = PHPhotoLibrary.GetAuthorizationStatus(PHAccessLevel.ReadWrite);
		if (status != PHAuthorizationStatus.Authorized && status != PHAuthorizationStatus.Limited)
			return;

		var options = new PHFetchOptions
		{
			SortDescriptors = [new NSSortDescriptor("creationDate", false)],
		};
		var result = PHAsset.FetchAssets(PHAssetMediaType.Video, options);

		for (nint i = 0; i < result.Count; i++)
		{
			if (token.IsCancellationRequested) break;
			if (result[i] is not PHAsset asset) continue;

			var created = asset.CreationDate is { } d ? (long)d.SecondsSince1970 : 0;
			string name;
			try
			{
				name = PHAssetResource.GetAssetResources(asset).FirstOrDefault()?.OriginalFilename ?? "";
			}
			catch
			{
				name = "";
			}
			if (string.IsNullOrEmpty(name))
				name = $"Video {DateTimeOffset.FromUnixTimeSeconds(created).LocalDateTime:yyyy-MM-dd HH.mm}.mov";

			list.Add(new MediaItem
			{
				Key = PhotoPrefix + asset.LocalIdentifier,
				IsVideo = true,
				Uri = "phasset:" + asset.LocalIdentifier,
				DisplayName = name,
				Title = Path.GetFileNameWithoutExtension(name),
				DurationMs = (long)(asset.Duration * 1000),
				Width = (int)asset.PixelWidth,
				Height = (int)asset.PixelHeight,
				DateAdded = created,
				MimeType = "video/quicktime",
				Folder = "Fotoğraflar",
				FolderPath = "Fotoğraflar",
			});
		}
	}

	// ============================================================ Müzik arşivi

	void ScanMusic(List<MediaItem> list, CancellationToken token)
	{
		_songs.Clear();
		if (MPMediaLibrary.AuthorizationStatus != MPMediaLibraryAuthorizationStatus.Authorized)
			return;

		var items = MPMediaQuery.SongsQuery.Items;
		if (items is null) return;

		foreach (var song in items)
		{
			if (token.IsCancellationRequested) break;

			// Yalnızca cihazda gerçekten bulunan, korumasız dosyalar çalınabilir
			if (song.AssetURL is null || song.IsCloudItem || song.HasProtectedAsset)
				continue;

			var id = song.PersistentID;
			_songs[id] = song;
			var album = song.AlbumTitle ?? "";
			var isVideo = ((ulong)song.MediaType & 0xff00UL) != 0; // MPMediaTypeAnyVideo

			list.Add(new MediaItem
			{
				Key = MusicPrefix + id,
				StoreId = unchecked((long)id),
				IsVideo = isVideo,
				Uri = song.AssetURL.AbsoluteString ?? "",
				Title = song.Title ?? "",
				DisplayName = song.Title ?? "",
				Artist = song.Artist ?? "",
				Album = album,
				DurationMs = (long)(song.PlaybackDuration * 1000),
				DateAdded = song.DateAdded is { } d ? (long)d.SecondsSince1970 : 0,
				MimeType = "audio/mp4",
				Folder = string.IsNullOrWhiteSpace(album) ? "Müzik Arşivi" : album,
				FolderPath = "Müzik/" + (string.IsNullOrWhiteSpace(album) ? "-" : album),
			});
		}
	}

	// ============================================================ Dosyalar (Documents)

	static void ScanDocuments(List<MediaItem> list, CancellationToken token)
	{
		var root = DocumentsDir;
		if (!Directory.Exists(root)) return;

		foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
		{
			if (token.IsCancellationRequested) break;

			var ext = Path.GetExtension(file);
			var isVideo = VideoExt.Contains(ext);
			if (!isVideo && !AudioExt.Contains(ext)) continue;
			if (Path.GetFileName(file).StartsWith('.')) continue;

			var info = new FileInfo(file);
			var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
			var relDir = Path.GetDirectoryName(relative)?.Replace('\\', '/') ?? "";

			long durationMs = 0;
			string artist = "", album = "", title = Path.GetFileNameWithoutExtension(file);
			try
			{
				using var asset = AVUrlAsset.Create(NSUrl.FromFilename(file));
				var dur = asset.Duration;
				if (!dur.IsInvalid && !dur.IsIndefinite && dur.Seconds > 0)
					durationMs = (long)(dur.Seconds * 1000);

				if (!isVideo)
				{
					foreach (var meta in asset.CommonMetadata)
					{
						var key = meta.CommonKey?.ToString();
						var value = meta.StringValue;
						if (string.IsNullOrWhiteSpace(value)) continue;
						if (key == "title") title = value;
						else if (key == "artist") artist = value;
						else if (key == "albumName") album = value;
					}
				}
			}
			catch
			{
				// meta veri okunamazsa dosya adıyla devam
			}

			list.Add(new MediaItem
			{
				Key = DocPrefix + relative,
				IsVideo = isVideo,
				Uri = "doc:" + relative,
				DisplayName = Path.GetFileName(file),
				Title = title,
				Artist = artist,
				Album = album,
				DurationMs = durationMs,
				SizeBytes = info.Length,
				DateAdded = new DateTimeOffset(info.CreationTimeUtc).ToUnixTimeSeconds(),
				MimeType = isVideo ? "video/*" : "audio/*",
				Folder = string.IsNullOrEmpty(relDir) ? "Dosyalarım" : Path.GetFileName(relDir),
				FolderPath = string.IsNullOrEmpty(relDir) ? "Dosyalarım" : "Dosyalarım/" + relDir,
			});
		}
	}

	// ============================================================ oynatma adresi

	/// <summary>
	/// Kayıtlı adresi AVPlayer'ın açabileceği gerçek adrese çevirir.
	/// (Uygulama güncellenince klasör yolu değiştiği için dosya yolları göreli saklanır.)
	/// </summary>
	public async Task<string?> ResolvePlaybackUriAsync(MediaItem item, CancellationToken token = default)
	{
		if (item.Key.StartsWith(DocPrefix, StringComparison.Ordinal))
		{
			var full = Path.Combine(DocumentsDir, item.Key[DocPrefix.Length..]);
			return File.Exists(full) ? NSUrl.FromFilename(full).AbsoluteString : null;
		}

		if (item.Key.StartsWith(PhotoPrefix, StringComparison.Ordinal))
			return await ResolvePhotoAsync(item.Key[PhotoPrefix.Length..], token);

		return item.Uri; // ipod-library:// adresleri doğrudan çalınır
	}

	static async Task<string?> ResolvePhotoAsync(string localId, CancellationToken token)
	{
		var asset = PHAsset.FetchAssetsUsingLocalIdentifiers([localId], null).firstObject as PHAsset;
		if (asset is null) return null;

		var tcs = new TaskCompletionSource<AVAsset?>();
		var options = new PHVideoRequestOptions
		{
			NetworkAccessAllowed = true, // iCloud'daki videoyu indir
			Version = PHVideoRequestOptionsVersion.Current,
			DeliveryMode = PHVideoRequestOptionsDeliveryMode.HighQualityFormat,
		};
		PHImageManager.DefaultManager.RequestAVAsset(asset, options, (avAsset, _, _) => tcs.TrySetResult(avAsset));

		using var reg = token.Register(() => tcs.TrySetCanceled());
		var result = await tcs.Task;

		if (result is AVUrlAsset urlAsset)
			return urlAsset.Url.AbsoluteString;

		if (result is null)
			return null;

		// Ağır çekim vb. birleşik videolar: geçici MP4'e aktar
		var safe = string.Concat(localId.Where(char.IsLetterOrDigit));
		var target = Path.Combine(FileSystem.CacheDirectory, $"export_{safe}.mp4");
		if (File.Exists(target))
			return NSUrl.FromFilename(target).AbsoluteString;

		var export = new AVAssetExportSession(result, AVAssetExportSessionPreset.HighestQuality)
		{
			OutputUrl = NSUrl.FromFilename(target),
			OutputFileType = AVFileTypes.Mpeg4.GetConstant(),
		};
		await export.ExportTaskAsync();
		return export.Status == AVAssetExportSessionStatus.Completed
			? NSUrl.FromFilename(target).AbsoluteString
			: null;
	}

	// ============================================================ küçük resim

	public Task<bool> CreateThumbnailAsync(MediaItem item, string targetPath, CancellationToken token = default)
	{
		return Task.Run(() =>
		{
			try
			{
				using var image = LoadThumbnail(item);
				if (image is null) return false;
				using var data = image.AsJPEG(0.82f);
				return data is not null && data.Save(targetPath, true);
			}
			catch
			{
				return false;
			}
		}, token);
	}

	UIImage? LoadThumbnail(MediaItem item)
	{
		if (item.Key.StartsWith(PhotoPrefix, StringComparison.Ordinal))
		{
			var asset = PHAsset.FetchAssetsUsingLocalIdentifiers([item.Key[PhotoPrefix.Length..]], null).firstObject as PHAsset;
			if (asset is null) return null;

			UIImage? img = null;
			var opts = new PHImageRequestOptions
			{
				Synchronous = true,
				NetworkAccessAllowed = true,
				DeliveryMode = PHImageRequestOptionsDeliveryMode.HighQualityFormat,
				ResizeMode = PHImageRequestOptionsResizeMode.Fast,
			};
			PHImageManager.DefaultManager.RequestImageForAsset(asset, new CGSize(384, 216),
				PHImageContentMode.AspectFill, opts, (result, _) => img = result);
			return img;
		}

		if (item.Key.StartsWith(MusicPrefix, StringComparison.Ordinal))
		{
			if (!ulong.TryParse(item.Key[MusicPrefix.Length..], out var id))
				return null;
			if (!_songs.TryGetValue(id, out var song))
			{
				// Tarama henüz bitmediyse şarkıyı kimliğiyle bul
				var query = new MPMediaQuery();
				query.AddFilterPredicate(MPMediaPropertyPredicate.PredicateWithValue(
					NSNumber.FromUInt64(id), MPMediaItem.PersistentIDProperty));
				song = query.Items?.FirstOrDefault();
			}
			return song?.Artwork?.ImageWithSize(new CGSize(256, 256));
		}

		if (item.Key.StartsWith(DocPrefix, StringComparison.Ordinal))
		{
			var full = Path.Combine(DocumentsDir, item.Key[DocPrefix.Length..]);
			if (!File.Exists(full)) return null;
			using var asset = AVUrlAsset.Create(NSUrl.FromFilename(full));

			if (item.IsVideo)
			{
				using var gen = new AVAssetImageGenerator(asset)
				{
					AppliesPreferredTrackTransform = true,
					MaximumSize = new CGSize(384, 216),
				};
				var seconds = Math.Min(2.0, Math.Max(0, item.DurationMs / 3000.0));
				using var cg = gen.CopyCGImageAtTime(CMTime.FromSeconds(seconds, 600), out _, out var error);
				return cg is null || error is not null ? null : UIImage.FromImage(cg);
			}

			// Ses dosyasına gömülü kapak resmi
			foreach (var meta in asset.CommonMetadata)
			{
				if (meta.CommonKey?.ToString() == "artwork" && meta.DataValue is { } bytes)
					return UIImage.LoadFromData(bytes);
			}
		}

		return null;
	}
}
