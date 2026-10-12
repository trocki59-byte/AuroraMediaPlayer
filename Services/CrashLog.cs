namespace AuroraMediaPlayer.Services;

/// <summary>
/// Beklenmeyen hataları dosyaya yazar; uygulama bir sonraki açılışta hatayı ekranda gösterir.
/// Böylece "açılıp kapanıyor" durumunda sebebi görülebilir.
/// </summary>
public static class CrashLog
{
	static string FilePath => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "son_hata.txt");

	static int _hooked;

	public static void Hook()
	{
		if (Interlocked.Exchange(ref _hooked, 1) == 1) return;
		AppDomain.CurrentDomain.UnhandledException += (_, e) => Write(e.ExceptionObject as Exception);
		TaskScheduler.UnobservedTaskException += (_, e) => { Write(e.Exception); e.SetObserved(); };
#if ANDROID
		Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) => Write(e.Exception);
#endif
	}

	public static void Write(Exception? ex)
	{
		if (ex is null) return;
		try
		{
			File.WriteAllText(FilePath,
				$"{DateTime.Now:dd.MM.yyyy HH:mm:ss}\nSürüm {AppInfo.Current.VersionString}\n\n{ex}");
		}
		catch
		{
			// yoksay
		}
	}

	/// <summary>Önceki çalışmada kaydedilen hata varsa metnini döndürür ve dosyayı siler.</summary>
	public static string? TakeLast()
	{
		try
		{
			if (!File.Exists(FilePath)) return null;
			var text = File.ReadAllText(FilePath);
			File.Delete(FilePath);
			return text.Length > 1800 ? text[..1800] + "…" : text;
		}
		catch
		{
			return null;
		}
	}
}
