Imports System.Threading

Friend Module Program

    <STAThread>
    Sub Main()
        ' Program ikinci kez açılırsa yenisini açma, mevcut pencere öne gelsin
        Dim ilkKopya As Boolean = False
        Using mutex As New Mutex(True, "TROCKI.AuroraMediaPlayer.Tekil", ilkKopya)
            If Not ilkKopya Then
                NativeMethods.BringExistingToFront()
                Return
            End If

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            Application.Run(New MainForm())
        End Using
    End Sub

End Module

