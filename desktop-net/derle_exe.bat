@echo off
chcp 65001 >nul
REM ============================================================
REM  TROÇKİ AURORA MEDIA PLAYER - Windows exe (tek dosya)
REM  Gerekli: .NET 10 SDK (bilgisayarınızda var)
REM ============================================================
cd /d "%~dp0"

echo Aurora derleniyor...
dotnet publish AuroraDesktop.csproj -c Release -o cikti || goto :hata

echo.
echo TAMAM! Program: %~dp0cikti\AuroraMediaPlayer.exe
echo Cift tiklayarak acabilir, masaustune kisayol olusturabilirsiniz.
explorer "%~dp0cikti"
pause
exit /b 0

:hata
echo.
echo HATA olustu. Yukaridaki mesajlari kontrol edin.
pause
exit /b 1
