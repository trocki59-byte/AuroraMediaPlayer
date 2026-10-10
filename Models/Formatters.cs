namespace AuroraMediaPlayer.Models;

public static class Formatters
{
	/// <summary>00:02:17 biçimi (tasarımdaki gibi her zaman saat:dakika:saniye).</summary>
	public static string Time(TimeSpan t)
	{
		if (t < TimeSpan.Zero) t = TimeSpan.Zero;
		return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
	}

	public static string Size(long bytes)
	{
		if (bytes <= 0) return "-";
		string[] units = ["B", "KB", "MB", "GB"];
		double v = bytes;
		int i = 0;
		while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
		return $"{v:0.#} {units[i]}";
	}

	public static string Date(long unixSeconds) =>
		unixSeconds <= 0 ? "-" : DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime.ToString("dd.MM.yyyy HH:mm");
}
