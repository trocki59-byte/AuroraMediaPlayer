using SQLite;

namespace AuroraMediaPlayer.Models;

/// <summary>
/// Cihazdaki tek bir video ya da müzik dosyası.
/// MediaStore'dan okunan alanlar + kullanıcıya ait alanlar (favori, geçmiş, kaldığı yer) SQLite'ta tutulur.
/// </summary>
[Table("Media")]
public class MediaItem : ObservableObject
{
	bool _isFavorite;
	bool _isNowPlaying;
	string? _thumbPath;
	long _lastPlayed;
	long _lastPositionMs;

	/// <summary>"v123" (video) ya da "a123" (müzik) — MediaStore kimliğinden türetilir.</summary>
	[PrimaryKey]
	public string Key { get; set; } = "";

	public long StoreId { get; set; }
	public bool IsVideo { get; set; }

	/// <summary>content:// adresi — oynatıcı doğrudan bu adresi açar.</summary>
	public string Uri { get; set; } = "";

	[Indexed]
	public string Title { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string Artist { get; set; } = "";
	public string Album { get; set; } = "";

	[Indexed]
	public string Folder { get; set; } = "";
	public string FolderPath { get; set; } = "";

	public string MimeType { get; set; } = "";
	public long DurationMs { get; set; }
	public int Width { get; set; }
	public int Height { get; set; }
	public long SizeBytes { get; set; }

	/// <summary>Unix saniye (MediaStore DATE_ADDED).</summary>
	public long DateAdded { get; set; }

	// ---- Kullanıcı verileri ----
	public bool IsFavorite
	{
		get => _isFavorite;
		set => SetProperty(ref _isFavorite, value);
	}

	/// <summary>Unix milisaniye. 0 = hiç oynatılmadı.</summary>
	public long LastPlayed
	{
		get => _lastPlayed;
		set => SetProperty(ref _lastPlayed, value);
	}

	public int PlayCount { get; set; }

	public long LastPositionMs
	{
		get => _lastPositionMs;
		set
		{
			if (SetProperty(ref _lastPositionMs, value))
				OnPropertyChanged(nameof(Progress));
		}
	}

	public string? ThumbPath
	{
		get => _thumbPath;
		set
		{
			if (SetProperty(ref _thumbPath, value))
				OnPropertyChanged(nameof(DisplayThumb));
		}
	}

	/// <summary>Küçük resim üretme denendi mi (başarısız olsa bile tekrar denememek için).</summary>
	public bool ThumbTried { get; set; }

	// ---- Arayüz için hesaplanan alanlar (veritabanına yazılmaz) ----

	[Ignore]
	public bool IsNowPlaying
	{
		get => _isNowPlaying;
		set => SetProperty(ref _isNowPlaying, value);
	}

	[Ignore]
	public string DisplayThumb =>
		!string.IsNullOrEmpty(ThumbPath) && File.Exists(ThumbPath)
			? ThumbPath!
			: (IsVideo ? "ic_video_small.png" : "ic_note_small.png");

	[Ignore]
	public bool HasThumb => !string.IsNullOrEmpty(ThumbPath) && File.Exists(ThumbPath);

	[Ignore]
	public TimeSpan Duration => TimeSpan.FromMilliseconds(DurationMs);

	[Ignore]
	public string DurationText => Formatters.Time(Duration);

	[Ignore]
	public string ResolutionText => IsVideo && Width > 0 && Height > 0 ? $"{Width} × {Height}" : "";

	[Ignore]
	public string FileName => string.IsNullOrWhiteSpace(DisplayName) ? Title : DisplayName;

	[Ignore]
	public string Subtitle
	{
		get
		{
			var parts = new List<string>();
			if (!IsVideo && !string.IsNullOrWhiteSpace(Artist) && Artist != "<unknown>")
				parts.Add(Artist);
			if (IsVideo && !string.IsNullOrEmpty(ResolutionText))
				parts.Add(ResolutionText);
			if (!string.IsNullOrWhiteSpace(Folder))
				parts.Add(Folder);
			parts.Add(DurationText);
			return string.Join("  •  ", parts);
		}
	}

	/// <summary>Kaldığı yer çubuğu için 0..1.</summary>
	[Ignore]
	public double Progress => DurationMs > 0 ? Math.Clamp((double)LastPositionMs / DurationMs, 0, 1) : 0;

	[Ignore]
	public bool HasProgress => Progress > 0.02 && Progress < 0.97;

	public void RefreshComputed()
	{
		OnPropertyChanged(nameof(DisplayThumb));
		OnPropertyChanged(nameof(Progress));
		OnPropertyChanged(nameof(HasProgress));
		OnPropertyChanged(nameof(Subtitle));
	}
}
