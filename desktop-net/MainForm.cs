using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace AuroraDesktop;

/// <summary>
/// Aurora'nın ana penceresi. Exe'nin içine gömülü web arayüzünü yerel bir klasöre çıkarır
/// ve WebView2 (Microsoft Edge altyapısı) ile açar. Dosyalar bilgisayardan okunur; adres, YouTube API
/// anahtarındaki "trocki59-byte.github.io" site kısıtlamasıyla uyumlu olsun diye aynı ad kullanılır
/// (hiçbir şey internetten indirilmez). İnternet yalnızca YouTube Müzik bölümü için gerekir.
/// </summary>
public sealed class MainForm : Form
{
	const string Host = "trocki59-byte.github.io";
	static readonly string AppUrl = $"https://{Host}/index.html";

	readonly WebView2 _web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(5, 8, 22) };
	FormWindowState _beforeFullscreen = FormWindowState.Normal;
	bool _fullscreen;

	static string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuroraMediaPlayer");

	public MainForm()
	{
		Text = "Aurora Media Player";
		BackColor = Color.FromArgb(5, 8, 22);
		MinimumSize = new Size(440, 620);
		StartPosition = FormStartPosition.CenterScreen;
		KeyPreview = true;
		try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!); } catch { /* simge olmadan devam */ }

		RestoreBounds_();
		Controls.Add(_web);

		Load += async (_, _) => await InitAsync();
		FormClosing += (_, _) => SaveBounds();
	}

	// ================================================================ başlatma

	async Task InitAsync()
	{
		try
		{
			var webDir = ExtractWebFiles();
			var options = new CoreWebView2EnvironmentOptions
			{
				// Şarkı bitince sıradakinin kendiliğinden başlayabilmesi için
				AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required",
				Language = "tr-TR",
			};
			var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(DataRoot, "WebView2"), options);
			await _web.EnsureCoreWebView2Async(env);

			var core = _web.CoreWebView2;
			core.SetVirtualHostNameToFolderMapping(Host, webDir, CoreWebView2HostResourceAccessKind.Allow);

			var s = core.Settings;
			s.AreDefaultContextMenusEnabled = false;
			s.AreDevToolsEnabled = Debugger.IsAttached;
			s.IsStatusBarEnabled = false;
			s.IsZoomControlEnabled = true;
			s.AreBrowserAcceleratorKeysEnabled = true;

			// Dış bağlantılar (YouTube Music vb.) varsayılan tarayıcıda açılsın
			core.NewWindowRequested += (_, e) =>
			{
				e.Handled = true;
				OpenExternal(e.Uri);
			};
			core.NavigationStarting += (_, e) =>
			{
				if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var u)) return;
				if (u.Host.Equals(Host, StringComparison.OrdinalIgnoreCase) || u.Scheme is "about" or "data" or "blob") return;
				e.Cancel = true;
				OpenExternal(e.Uri);
			};

			// Klasör bağlama, tam ekran, otomatik oynatma izinleri
			core.PermissionRequested += (_, e) =>
			{
				e.State = e.PermissionKind switch
				{
					CoreWebView2PermissionKind.Notifications => CoreWebView2PermissionState.Deny,
					CoreWebView2PermissionKind.Geolocation => CoreWebView2PermissionState.Deny,
					CoreWebView2PermissionKind.Camera => CoreWebView2PermissionState.Deny,
					CoreWebView2PermissionKind.Microphone => CoreWebView2PermissionState.Deny,
					_ => CoreWebView2PermissionState.Allow,
				};
				e.SavesInProfile = true;
				e.Handled = true;
			};

			// Videonun kendi tam ekran düğmesi
			core.ContainsFullScreenElementChanged += (_, _) => SetFullscreen(core.ContainsFullScreenElement);
			core.DocumentTitleChanged += (_, _) => Text = string.IsNullOrWhiteSpace(core.DocumentTitle) ? "Aurora Media Player" : core.DocumentTitle;

			_web.Source = new Uri(AppUrl);
		}
		catch (WebView2RuntimeNotFoundException)
		{
			var r = MessageBox.Show(this,
				"Aurora'nın çalışması için Microsoft Edge WebView2 gerekli; bu bilgisayarda bulunamadı.\n\n" +
				"İndirme sayfası açılsın mı? Kurduktan sonra Aurora'yı yeniden açın.",
				"Aurora Media Player", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
			if (r == DialogResult.Yes) OpenExternal("https://developer.microsoft.com/microsoft-edge/webview2/");
			Close();
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, $"Aurora başlatılamadı:\n{ex.Message}", "Aurora Media Player", MessageBoxButtons.OK, MessageBoxIcon.Error);
			Close();
		}
	}

	/// <summary>Exe'ye gömülü web dosyalarını %LOCALAPPDATA%\AuroraMediaPlayer\web klasörüne çıkarır (her açılışta güncellenir).</summary>
	static string ExtractWebFiles()
	{
		var dir = Path.Combine(DataRoot, "web");
		Directory.CreateDirectory(dir);
		var asm = Assembly.GetExecutingAssembly();
		foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith("web/", StringComparison.Ordinal) || n.StartsWith("web\\", StringComparison.Ordinal)))
		{
			var rel = name[4..].Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
			var target = Path.Combine(dir, rel);
			Directory.CreateDirectory(Path.GetDirectoryName(target)!);
			using var src = asm.GetManifestResourceStream(name)!;
			using var dst = File.Create(target);
			src.CopyTo(dst);
		}
		return dir;
	}

	static void OpenExternal(string url)
	{
		try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { /* yoksay */ }
	}

	// ================================================================ klavye

	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		switch (keyData)
		{
			case Keys.F11:
				SetFullscreen(!_fullscreen);
				return true;
			case Keys.Escape when _fullscreen:
				SetFullscreen(false);
				return true;
			case Keys.F5:
				_web.CoreWebView2?.Reload();
				return true;
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	void SetFullscreen(bool on)
	{
		if (on == _fullscreen) return;
		_fullscreen = on;
		if (on)
		{
			_beforeFullscreen = WindowState;
			FormBorderStyle = FormBorderStyle.None;
			WindowState = FormWindowState.Normal; // Maximized iken kenarlıksız geçiş için
			WindowState = FormWindowState.Maximized;
		}
		else
		{
			FormBorderStyle = FormBorderStyle.Sizable;
			WindowState = _beforeFullscreen;
		}
	}

	// ================================================================ pencere konumu

	static string BoundsFile => Path.Combine(DataRoot, "pencere.txt");

	void RestoreBounds_()
	{
		Size = new Size(1366, 860);
		try
		{
			var p = File.ReadAllText(BoundsFile).Split(';').Select(int.Parse).ToArray();
			var r = new Rectangle(p[0], p[1], p[2], p[3]);
			if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(r)))
			{
				StartPosition = FormStartPosition.Manual;
				Bounds = r;
				if (p.Length > 4 && p[4] == 1) WindowState = FormWindowState.Maximized;
			}
		}
		catch { /* ilk açılış */ }
	}

	void SaveBounds()
	{
		try
		{
			Directory.CreateDirectory(DataRoot);
			var r = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
			File.WriteAllText(BoundsFile, $"{r.X};{r.Y};{r.Width};{r.Height};{(WindowState == FormWindowState.Maximized ? 1 : 0)}");
		}
		catch { /* yoksay */ }
	}
}

internal static class NativeMethods
{
	[DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
	[DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	public static void BringExistingToFront()
	{
		var me = Process.GetCurrentProcess();
		var other = Process.GetProcessesByName(me.ProcessName).FirstOrDefault(p => p.Id != me.Id && p.MainWindowHandle != IntPtr.Zero);
		if (other is null) return;
		ShowWindow(other.MainWindowHandle, 9); // SW_RESTORE
		SetForegroundWindow(other.MainWindowHandle);
	}
}
