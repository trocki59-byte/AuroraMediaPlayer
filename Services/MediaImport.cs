namespace AuroraMediaPlayer.Services;

/// <summary>
/// iPhone'da "Dosya Ekle": seçilen video/müzik dosyalarını uygulamanın klasörüne kopyalar
/// (Dosyalar › iPhone'umda › Aurora). Tarayıcı bu klasörü otomatik okur.
/// </summary>
public static class MediaImport
{
	static readonly string[] VideoExt = [".mp4", ".m4v", ".mov", ".3gp", ".mkv", ".webm", ".avi", ".ts", ".mts"];

	/// <summary>Bu platformda içe aktarma gerekli mi? (Android tüm dosyaları zaten görür.)</summary>
	public static bool IsSupported => DeviceInfo.Platform == DevicePlatform.iOS;

	public static string LibraryRoot =>
		Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

	public static bool IsVideoFile(string path) =>
		VideoExt.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

	/// <summary>Dosya seçiciyi açar, seçilenleri kopyalar. Kopyalanan dosya sayısını döndürür.</summary>
	public static async Task<int> PickAndImportAsync()
	{
		var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
		{
			{ DevicePlatform.iOS, new[] { "public.movie", "public.audio" } },
			{ DevicePlatform.Android, new[] { "video/*", "audio/*" } },
		});

		var files = await FilePicker.Default.PickMultipleAsync(new PickOptions
		{
			PickerTitle = "Video veya müzik seçin",
			FileTypes = types,
		});

		var count = 0;
		foreach (var file in files ?? [])
		{
			if (file is null) continue;
			try
			{
				var target = UniquePath(Path.Combine(LibraryRoot, "Eklenenler"), file.FileName);
				await using var src = await file.OpenReadAsync();
				await using var dst = File.Create(target);
				await src.CopyToAsync(dst);
				count++;
			}
			catch
			{
				// tek dosyadaki hata diğerlerini durdurmasın
			}
		}
		return count;
	}

	/// <summary>Var olan bir dosyayı kütüphane klasörüne kopyalar (zaten oradaysa kopyalamaz).</summary>
	public static string CopyIntoLibrary(string sourcePath, string subfolder)
	{
		var root = Path.GetFullPath(LibraryRoot);
		var full = Path.GetFullPath(sourcePath);
		var inInbox = full.Contains($"{Path.DirectorySeparatorChar}Inbox{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
		if (full.StartsWith(root, StringComparison.Ordinal) && !inInbox)
			return full;

		var target = UniquePath(Path.Combine(root, subfolder), Path.GetFileName(full));
		File.Copy(full, target);
		return target;
	}

	static string UniquePath(string dir, string fileName)
	{
		Directory.CreateDirectory(dir);
		var name = Path.GetFileNameWithoutExtension(fileName);
		var ext = Path.GetExtension(fileName);
		var path = Path.Combine(dir, fileName);
		for (var i = 2; File.Exists(path); i++)
			path = Path.Combine(dir, $"{name} ({i}){ext}");
		return path;
	}
}
