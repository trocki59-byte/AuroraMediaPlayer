Imports System.Diagnostics
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.WinForms

''' <summary>
''' Aurora'nın ana penceresi. Exe'nin içine gömülü web arayüzünü yerel bir klasöre çıkarır
''' ve WebView2 (Microsoft Edge altyapısı) ile açar. Dosyalar bilgisayardan okunur; adres, YouTube API
''' anahtarındaki "trocki59-byte.github.io" site kısıtlamasıyla uyumlu olsun diye aynı ad kullanılır
''' (hiçbir şey internetten indirilmez). İnternet yalnızca YouTube Müzik bölümü için gerekir.
''' </summary>
Public NotInheritable Class MainForm
    Inherits Form

    Private Const HostName As String = "trocki59-byte.github.io"
    Private Shared ReadOnly AppUrl As String = $"https://{HostName}/index.html"

    Private ReadOnly _web As New WebView2() With {
        .Dock = DockStyle.Fill,
        .DefaultBackgroundColor = Color.FromArgb(5, 8, 22)
    }
    Private _beforeFullscreen As FormWindowState = FormWindowState.Normal
    Private _fullscreen As Boolean

    Private Shared ReadOnly Property DataRoot As String
        Get
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuroraMediaPlayer")
        End Get
    End Property

    Public Sub New()
        Text = "Aurora Media Player"
        BackColor = Color.FromArgb(5, 8, 22)
        MinimumSize = New Size(440, 620)
        StartPosition = FormStartPosition.CenterScreen
        KeyPreview = True
        Try
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath)
        Catch
            ' simge olmadan devam
        End Try

        RestoreWindowBounds()
        Controls.Add(_web)

        AddHandler Load, Async Sub(s, e) Await InitAsync()
        AddHandler FormClosing, Sub(s, e) SaveWindowBounds()
    End Sub

    ' ================================================================ başlatma

    Private Async Function InitAsync() As Task
        Try
            Dim webDir = ExtractWebFiles()
            Dim options As New CoreWebView2EnvironmentOptions() With {
                .AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required",
                .Language = "tr-TR"
            }
            Dim env = Await CoreWebView2Environment.CreateAsync(Nothing, Path.Combine(DataRoot, "WebView2"), options)
            Await _web.EnsureCoreWebView2Async(env)

            Dim core = _web.CoreWebView2
            core.SetVirtualHostNameToFolderMapping(HostName, webDir, CoreWebView2HostResourceAccessKind.Allow)

            With core.Settings
                .AreDefaultContextMenusEnabled = False
                .AreDevToolsEnabled = Debugger.IsAttached
                .IsStatusBarEnabled = False
                .IsZoomControlEnabled = True
                .AreBrowserAcceleratorKeysEnabled = True
            End With

            ' Dış bağlantılar (YouTube Music vb.) varsayılan tarayıcıda açılsın
            AddHandler core.NewWindowRequested,
                Sub(s, e)
                    e.Handled = True
                    OpenExternal(e.Uri)
                End Sub

            AddHandler core.NavigationStarting,
                Sub(s, e)
                    Dim u As Uri = Nothing
                    If Not Uri.TryCreate(e.Uri, UriKind.Absolute, u) Then Return
                    If u.Host.Equals(HostName, StringComparison.OrdinalIgnoreCase) Then Return
                    If u.Scheme = "about" OrElse u.Scheme = "data" OrElse u.Scheme = "blob" Then Return
                    e.Cancel = True
                    OpenExternal(e.Uri)
                End Sub

            ' Klasör bağlama, tam ekran, otomatik oynatma izinleri
            AddHandler core.PermissionRequested,
                Sub(s, e)
                    Select Case e.PermissionKind
                        Case CoreWebView2PermissionKind.Notifications,
                             CoreWebView2PermissionKind.Geolocation,
                             CoreWebView2PermissionKind.Camera,
                             CoreWebView2PermissionKind.Microphone
                            e.State = CoreWebView2PermissionState.Deny
                        Case Else
                            e.State = CoreWebView2PermissionState.Allow
                    End Select
                    e.SavesInProfile = True
                    e.Handled = True
                End Sub

            ' Videonun kendi tam ekran düğmesi
            AddHandler core.ContainsFullScreenElementChanged, Sub(s, e) SetFullscreen(core.ContainsFullScreenElement)
            AddHandler core.DocumentTitleChanged,
                Sub(s, e)
                    Text = If(String.IsNullOrWhiteSpace(core.DocumentTitle), "Aurora Media Player", core.DocumentTitle)
                End Sub

            _web.Source = New Uri(AppUrl)

        Catch ex As WebView2RuntimeNotFoundException
            Dim r = MessageBox.Show(Me,
                "Aurora'nın çalışması için Microsoft Edge WebView2 gerekli; bu bilgisayarda bulunamadı." & vbCrLf & vbCrLf &
                "İndirme sayfası açılsın mı? Kurduktan sonra Aurora'yı yeniden açın.",
                "Aurora Media Player", MessageBoxButtons.YesNo, MessageBoxIcon.Information)
            If r = DialogResult.Yes Then OpenExternal("https://developer.microsoft.com/microsoft-edge/webview2/")
            Close()

        Catch ex As Exception
            MessageBox.Show(Me, "Aurora başlatılamadı:" & vbCrLf & ex.Message, "Aurora Media Player",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
            Close()
        End Try
    End Function

    ''' <summary>Exe'ye gömülü web dosyalarını %LOCALAPPDATA%\AuroraMediaPlayer\web klasörüne çıkarır (her açılışta güncellenir).</summary>
    Private Shared Function ExtractWebFiles() As String
        Dim dir = Path.Combine(DataRoot, "web")
        Directory.CreateDirectory(dir)
        Dim asm = Assembly.GetExecutingAssembly()

        For Each name In asm.GetManifestResourceNames()
            If Not (name.StartsWith("web/", StringComparison.Ordinal) OrElse name.StartsWith("web\", StringComparison.Ordinal)) Then Continue For

            Dim rel = name.Substring(4).Replace("\"c, Path.DirectorySeparatorChar).Replace("/"c, Path.DirectorySeparatorChar)
            Dim target = Path.Combine(dir, rel)
            Directory.CreateDirectory(Path.GetDirectoryName(target))

            Using src = asm.GetManifestResourceStream(name)
                Using dst = File.Create(target)
                    src.CopyTo(dst)
                End Using
            End Using
        Next

        Return dir
    End Function

    Private Shared Sub OpenExternal(url As String)
        Try
            Process.Start(New ProcessStartInfo(url) With {.UseShellExecute = True})
        Catch
            ' yoksay
        End Try
    End Sub

    ' ================================================================ klavye

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        Select Case keyData
            Case Keys.F11
                SetFullscreen(Not _fullscreen)
                Return True
            Case Keys.Escape
                If _fullscreen Then
                    SetFullscreen(False)
                    Return True
                End If
            Case Keys.F5
                _web.CoreWebView2?.Reload()
                Return True
        End Select
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Private Sub SetFullscreen([on] As Boolean)
        If [on] = _fullscreen Then Return
        _fullscreen = [on]
        If [on] Then
            _beforeFullscreen = WindowState
            FormBorderStyle = FormBorderStyle.None
            WindowState = FormWindowState.Normal ' Maximized iken kenarlıksız geçiş için
            WindowState = FormWindowState.Maximized
        Else
            FormBorderStyle = FormBorderStyle.Sizable
            WindowState = _beforeFullscreen
        End If
    End Sub

    ' ================================================================ pencere konumu

    Private Shared ReadOnly Property BoundsFile As String
        Get
            Return Path.Combine(DataRoot, "pencere.txt")
        End Get
    End Property

    Private Sub RestoreWindowBounds()
        Size = New Size(1366, 860)
        Try
            Dim p = File.ReadAllText(BoundsFile).Split(";"c).Select(Function(x) Integer.Parse(x)).ToArray()
            Dim r As New Rectangle(p(0), p(1), p(2), p(3))
            If Screen.AllScreens.Any(Function(sc) sc.WorkingArea.IntersectsWith(r)) Then
                StartPosition = FormStartPosition.Manual
                Bounds = r
                If p.Length > 4 AndAlso p(4) = 1 Then WindowState = FormWindowState.Maximized
            End If
        Catch
            ' ilk açılış
        End Try
    End Sub

    Private Sub SaveWindowBounds()
        Try
            Directory.CreateDirectory(DataRoot)
            Dim r = If(WindowState = FormWindowState.Normal, Bounds, RestoreBounds)
            File.WriteAllText(BoundsFile, $"{r.X};{r.Y};{r.Width};{r.Height};{If(WindowState = FormWindowState.Maximized, 1, 0)}")
        Catch
            ' yoksay
        End Try
    End Sub

End Class

Friend Module NativeMethods

    <DllImport("user32.dll")>
    Private Function SetForegroundWindow(hWnd As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Function ShowWindow(hWnd As IntPtr, nCmdShow As Integer) As Boolean
    End Function

    Public Sub BringExistingToFront()
        Dim [me] = Process.GetCurrentProcess()
        Dim other = Process.GetProcessesByName([me].ProcessName).
            FirstOrDefault(Function(p) p.Id <> [me].Id AndAlso p.MainWindowHandle <> IntPtr.Zero)
        If other Is Nothing Then Return
        ShowWindow(other.MainWindowHandle, 9) ' SW_RESTORE
        SetForegroundWindow(other.MainWindowHandle)
    End Sub

End Module

