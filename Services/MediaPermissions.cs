namespace AuroraMediaPlayer.Services;

/// <summary>
/// Android 13+ : READ_MEDIA_VIDEO + READ_MEDIA_AUDIO
/// Android 12 ve altı : READ_EXTERNAL_STORAGE
/// </summary>
public class ReadMediaPermission : Permissions.BasePlatformPermission
{
#if ANDROID
	public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
		OperatingSystem.IsAndroidVersionAtLeast(33)
			? new[]
			{
				("android.permission.READ_MEDIA_VIDEO", true),
				("android.permission.READ_MEDIA_AUDIO", true),
			}
			: new[]
			{
				("android.permission.READ_EXTERNAL_STORAGE", true),
			};
#endif
}

/// <summary>Android 13+ medya bildirimi (arka planda çalarken kilit ekranı kontrolleri).</summary>
public class NotificationPermission : Permissions.BasePlatformPermission
{
#if ANDROID
	public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
		OperatingSystem.IsAndroidVersionAtLeast(33)
			? new[] { ("android.permission.POST_NOTIFICATIONS", true) }
			: Array.Empty<(string, bool)>();
#endif
}

public static class MediaPermissions
{
	public static async Task<bool> EnsureMediaAsync()
	{
#if IOS
		// iPhone: Fotoğraflar (videolar) + Müzik arşivi. İkisi de reddedilse bile
		// uygulamanın kendi klasöründeki dosyalar çalınabildiği için kütüphane açılır.
		if (await Permissions.CheckStatusAsync<Permissions.Photos>() == PermissionStatus.Unknown)
			await Permissions.RequestAsync<Permissions.Photos>();
		if (await Permissions.CheckStatusAsync<Permissions.Media>() == PermissionStatus.Unknown)
			await Permissions.RequestAsync<Permissions.Media>();
		return true;
#else
		var status = await Permissions.CheckStatusAsync<ReadMediaPermission>();
		if (status == PermissionStatus.Granted || status == PermissionStatus.Limited)
			return true;

		status = await Permissions.RequestAsync<ReadMediaPermission>();
		return status == PermissionStatus.Granted || status == PermissionStatus.Limited;
#endif
	}

	public static async Task<bool> HasMediaAsync()
	{
#if IOS
		// İlk açılışta izinler henüz sorulmadıysa false → EnsureMediaAsync sorar
		var photos = await Permissions.CheckStatusAsync<Permissions.Photos>();
		var music = await Permissions.CheckStatusAsync<Permissions.Media>();
		return photos != PermissionStatus.Unknown && music != PermissionStatus.Unknown;
#else
		var status = await Permissions.CheckStatusAsync<ReadMediaPermission>();
		return status == PermissionStatus.Granted || status == PermissionStatus.Limited;
#endif
	}

	public static async Task EnsureNotificationsAsync()
	{
#if ANDROID
		try
		{
			var status = await Permissions.CheckStatusAsync<NotificationPermission>();
			if (status != PermissionStatus.Granted)
				await Permissions.RequestAsync<NotificationPermission>();
		}
		catch
		{
			// Bildirim izni zorunlu değil
		}
#else
		await Task.CompletedTask; // iOS'ta medya kontrolleri için bildirim izni gerekmez
#endif
	}
}
