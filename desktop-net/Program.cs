namespace AuroraDesktop;

internal static class Program
{
	[STAThread]
	static void Main()
	{
		// Program ikinci kez açılırsa yenisini açma, mevcut pencere öne gelsin
		using var mutex = new Mutex(true, "TROCKI.AuroraMediaPlayer.Tekil", out var ilkKopya);
		if (!ilkKopya)
		{
			NativeMethods.BringExistingToFront();
			return;
		}

		ApplicationConfiguration.Initialize(); // yüksek DPI + görsel stiller (csproj ayarlarından)
		Application.Run(new MainForm());
	}
}
