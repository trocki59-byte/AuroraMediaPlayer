namespace AuroraMediaPlayer.Services;

/// <summary>Başka bir uygulamadan "Birlikte aç" ile gelen dosya isteği.</summary>
public static class ExternalOpen
{
	static (string Uri, bool IsVideo)? _pending;

	public static event EventHandler? Requested;

	public static void Request(string uri, bool isVideo)
	{
		_pending = (uri, isVideo);
		Requested?.Invoke(null, EventArgs.Empty);
	}

	public static (string Uri, bool IsVideo)? Take()
	{
		var p = _pending;
		_pending = null;
		return p;
	}
}
