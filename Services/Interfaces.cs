using AuroraMediaPlayer.Models;

namespace AuroraMediaPlayer.Services;

/// <summary>Cihazdaki video ve müzik dosyalarını bulur (Android: MediaStore · iOS: Fotoğraflar, Müzik, Dosyalar).</summary>
public interface IMediaScanner
{
	Task<List<MediaItem>> ScanAsync(CancellationToken token = default);

	/// <summary>
	/// Kayıtlı adresi oynatıcının açabileceği gerçek adrese çevirir
	/// (Android: content:// aynen; iOS: Fotoğraflar videosu / uygulama klasöründeki dosya).
	/// </summary>
	Task<string?> ResolvePlaybackUriAsync(MediaItem item, CancellationToken token = default);

	/// <summary>Dosya için küçük resim üretip <paramref name="targetPath"/> konumuna kaydeder.</summary>
	Task<bool> CreateThumbnailAsync(MediaItem item, string targetPath, CancellationToken token = default);
}

/// <summary>Platforma özel ekran işlemleri.</summary>
public interface IPlatformUi
{
	/// <summary>Tam ekran (sistem çubukları gizli) ve isteğe bağlı yatay yön.</summary>
	void SetFullscreen(bool fullscreen, bool landscape);

	/// <summary>Ekran yansıtma / Cast ayarlarını açar.</summary>
	void OpenCastSettings();

	/// <summary>Uygulama izin ayarlarını açar.</summary>
	void OpenAppSettings();
}
