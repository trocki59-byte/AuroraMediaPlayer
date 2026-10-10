using AuroraMediaPlayer.Services;
using Foundation;

namespace AuroraMediaPlayer.Ios;

/// <summary>Başka uygulamalardan ("Birlikte aç", AirDrop) gelen dosyaları Aurora klasörüne alır.</summary>
public static class IosFileImport
{
	public static bool HandleIncoming(NSUrl url)
	{
		if (!url.IsFileUrl || string.IsNullOrEmpty(url.Path))
			return false;

		var scoped = url.StartAccessingSecurityScopedResource();
		try
		{
			var target = MediaImport.CopyIntoLibrary(url.Path, "Gelenler");
			var isVideo = MediaImport.IsVideoFile(target);
			ExternalOpen.Request(NSUrl.FromFilename(target).AbsoluteString ?? target, isVideo);
			return true;
		}
		catch
		{
			return false;
		}
		finally
		{
			if (scoped)
				url.StopAccessingSecurityScopedResource();
		}
	}
}
