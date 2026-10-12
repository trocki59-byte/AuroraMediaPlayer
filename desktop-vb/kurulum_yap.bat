@echo off
chcp 65001 >nul
REM ============================================================
REM  TROÇKİ AURORA MEDIA PLAYER - Kurulum dosyası (Setup) üretir
REM  Gerekli: .NET 10 SDK  +  Inno Setup 6 (https://jrsoftware.org/isdl.php)
REM ============================================================
cd /d "%~dp0"

echo [1/2] Program derleniyor...
dotnet publish AuroraDesktopVB.vbproj -c Release -o cikti || goto :hata

set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
  echo.
  echo Inno Setup 6 bulunamadi. https://jrsoftware.org/isdl.php adresinden kurun.
  start https://jrsoftware.org/isdl.php
  pause
  exit /b 1
)

echo [2/2] Kurulum dosyasi hazirlaniyor...
"%ISCC%" setup\AuroraSetup.iss || goto :hata

echo.
echo TAMAM! Kurulum: %~dp0kurulum\Aurora-Setup-2.0.0.exe
explorer "%~dp0kurulum"
pause
exit /b 0

:hata
echo.
echo HATA olustu. Yukaridaki mesajlari kontrol edin.
pause
exit /b 1
