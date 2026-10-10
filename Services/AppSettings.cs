using AuroraMediaPlayer.Models;

namespace AuroraMediaPlayer.Services;

/// <summary>Kullanıcı ayarları (Preferences — küçük anahtar/değer deposu).</summary>
public static class AppSettings
{
	public static bool ResumePlayback
	{
		get => Preferences.Default.Get(nameof(ResumePlayback), true);
		set => Preferences.Default.Set(nameof(ResumePlayback), value);
	}

	public static bool AutoPlayNext
	{
		get => Preferences.Default.Get(nameof(AutoPlayNext), true);
		set => Preferences.Default.Set(nameof(AutoPlayNext), value);
	}

	public static bool BackgroundAudio
	{
		get => Preferences.Default.Get(nameof(BackgroundAudio), true);
		set => Preferences.Default.Set(nameof(BackgroundAudio), value);
	}

	public static bool KeepScreenOn
	{
		get => Preferences.Default.Get(nameof(KeepScreenOn), true);
		set => Preferences.Default.Set(nameof(KeepScreenOn), value);
	}

	public static bool AutoRotateFullscreen
	{
		get => Preferences.Default.Get(nameof(AutoRotateFullscreen), true);
		set => Preferences.Default.Set(nameof(AutoRotateFullscreen), value);
	}

	public static bool ScanOnStart
	{
		get => Preferences.Default.Get(nameof(ScanOnStart), true);
		set => Preferences.Default.Set(nameof(ScanOnStart), value);
	}

	public static double Volume
	{
		get => Preferences.Default.Get(nameof(Volume), 0.8);
		set => Preferences.Default.Set(nameof(Volume), value);
	}

	public static bool Shuffle
	{
		get => Preferences.Default.Get(nameof(Shuffle), false);
		set => Preferences.Default.Set(nameof(Shuffle), value);
	}

	public static RepeatMode Repeat
	{
		get => (RepeatMode)Preferences.Default.Get(nameof(Repeat), (int)RepeatMode.All);
		set => Preferences.Default.Set(nameof(Repeat), (int)value);
	}

	public static SortMode Sort
	{
		get => (SortMode)Preferences.Default.Get(nameof(Sort), (int)SortMode.DateAdded);
		set => Preferences.Default.Set(nameof(Sort), (int)value);
	}

	public static bool SortDescending
	{
		get => Preferences.Default.Get(nameof(SortDescending), true);
		set => Preferences.Default.Set(nameof(SortDescending), value);
	}

	public static string LastMediaKey
	{
		get => Preferences.Default.Get(nameof(LastMediaKey), "");
		set => Preferences.Default.Set(nameof(LastMediaKey), value);
	}
}
