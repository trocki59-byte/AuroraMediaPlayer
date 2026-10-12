; ============================================================
;  TROÇKİ® AURORA MEDIA PLAYER — Windows Kurulum (Inno Setup 6)
;  Önce derle_exe.bat ile cikti\AuroraMediaPlayer.exe üretin,
;  sonra bu dosyayı Inno Setup ile derleyin (ya da kurulum_yap.bat).
; ============================================================

#define AppName      "Aurora Media Player"
#define AppVersion   "2.0.0"
#define AppPublisher "TROÇKİ Software"
#define AppExe       "AuroraMediaPlayer.exe"

[Setup]
AppId={{201B7BF3-5C06-4AF6-ADD0-FFA01795B076}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName=TROÇKİ® {#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://trocki22.com.tr
AppCopyright=© TROÇKİ Software
DefaultDirName={autopf}\TROÇKİ\Aurora Media Player
DefaultGroupName=TROÇKİ Aurora Media Player
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName=TROÇKİ® {#AppName}
SetupIconFile=..\icon.ico
OutputDir=..\kurulum
OutputBaseFilename=Aurora-Setup-{#AppVersion}
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
AppMutex=TROCKI.AuroraMediaPlayer.Tekil
CloseApplications=yes
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=TROÇKİ Aurora Media Player Kurulum

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstüne kısayol oluştur"; GroupDescription: "Ek kısayollar:"

[Files]
Source: "..\cikti\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\BENIOKU.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Aurora Media Player"; Filename: "{app}\{#AppExe}"
Name: "{group}\Aurora Media Player'ı Kaldır"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Aurora Media Player"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Aurora Media Player'ı şimdi başlat"; Flags: nowait postinstall skipifsilent

[Code]
{ .NET 10 Masaüstü Çalışma Zamanı yüklü mü? }
function DotNet10DesktopInstalled(): Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), FindRec);
  if Result then FindClose(FindRec);
end;

{ WebView2 yüklü mü? }
function WebView2Installed(): Boolean;
var
  V: String;
begin
  Result :=
    (RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', V) and (V <> '') and (V <> '0.0.0.0')) or
    (RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', V) and (V <> '') and (V <> '0.0.0.0'));
end;

procedure OpenUrl(Url: String);
var
  Code: Integer;
begin
  ShellExec('open', Url, '', '', SW_SHOWNORMAL, ewNoWait, Code);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;

  if not DotNet10DesktopInstalled() then
  begin
    if MsgBox('Aurora için ".NET 10 Masaüstü Çalışma Zamanı" (.NET Desktop Runtime 10, x64) gerekli ve bu bilgisayarda bulunamadı.' + #13#10#13#10 +
              'İndirme sayfası açılsın mı? Kurduktan sonra bu kurulumu yeniden çalıştırın.' + #13#10#13#10 +
              '(Hayır derseniz kurulum yine de devam eder.)',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      OpenUrl('https://dotnet.microsoft.com/download/dotnet/10.0');
      Result := False;
      Exit;
    end;
  end;

  if not WebView2Installed() then
  begin
    if MsgBox('Microsoft Edge WebView2 bulunamadı (Windows 10/11''de genelde yüklüdür).' + #13#10#13#10 +
              'İndirme sayfası açılsın mı?', mbConfirmation, MB_YESNO) = IDYES then
      OpenUrl('https://developer.microsoft.com/microsoft-edge/webview2/');
  end;
end;
