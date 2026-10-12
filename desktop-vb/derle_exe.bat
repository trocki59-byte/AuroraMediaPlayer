@echo off
chcp 65001 >nul
REM ============================================================
REM  TROÇKİ AURORA MEDIA PLAYER - Windows exe (VB.NET, tek dosya)
REM  Gerekli: .NET 10 SDK
REM ============================================================
cd /d "%~dp0"

echo Aurora derleniyor...
dotnet publish AuroraDesktopVB.vbproj -c Release -o cikti || goto :hata

echo.
echo TAMAM! Program: %~dp0cikti\AuroraMediaPlayer.exe
explorer "%~dp0cikti"
pause
exit /b 0

:hata
echo.
echo HATA olustu. Yukaridaki mesajlari kontrol edin.
pause
exit /b 1
