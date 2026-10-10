@echo off
chcp 65001 >nul
REM ============================================================
REM  TROÇKİ AURORA MEDIA PLAYER - APK üretimi (doğrudan kurulum)
REM  Gerekli: .NET 10 SDK + "dotnet workload install maui-android"
REM ============================================================
cd /d "%~dp0"

echo [1/2] Paketler geri yukleniyor...
dotnet restore AuroraMediaPlayer.csproj -p:AuroraTarget=android || goto :hata

echo [2/2] APK derleniyor (Release)...
dotnet publish AuroraMediaPlayer.csproj -f net10.0-android -c Release -p:AuroraTarget=android -p:AndroidPackageFormats=apk -o cikti || goto :hata

echo.
echo TAMAM! APK dosyasi: %~dp0cikti\com.trocki.auroramediaplayer-Signed.apk
echo Telefona kopyalayip acarak kurabilirsiniz.
explorer "%~dp0cikti"
pause
exit /b 0

:hata
echo.
echo HATA olustu. Yukaridaki mesajlari kontrol edin.
pause
exit /b 1
