@echo off
chcp 65001 >nul
REM ============================================================
REM  TROÇKİ AURORA MEDIA PLAYER - Google Play AAB (imzalı)
REM ------------------------------------------------------------
REM  İlk sefer anahtar oluşturun (bir kez, şifreyi KAYBETMEYİN):
REM    keytool -genkeypair -v -keystore trocki.keystore -alias trocki ^
REM            -keyalg RSA -keysize 2048 -validity 10000
REM ============================================================
cd /d "%~dp0"

set KEYSTORE=%~dp0trocki.keystore
set ALIAS=trocki

if not exist "%KEYSTORE%" (
  echo trocki.keystore bulunamadi. Once yukaridaki keytool komutunu calistirin.
  pause
  exit /b 1
)

set /p STOREPASS=Keystore sifresi:

dotnet publish AuroraMediaPlayer.csproj -f net10.0-android -c Release -p:AuroraTarget=android ^
  -p:AndroidPackageFormats=aab ^
  -p:AndroidSigningKeyStore="%KEYSTORE%" ^
  -p:AndroidSigningKeyAlias=%ALIAS% ^
  -p:AndroidSigningKeyPass=%STOREPASS% ^
  -p:AndroidSigningStorePass=%STOREPASS% ^
  -o cikti_play || goto :hata

echo.
echo TAMAM! Google Play Console'a yuklenecek dosya:
echo %~dp0cikti_play\com.trocki.auroramediaplayer-Signed.aab
explorer "%~dp0cikti_play"
pause
exit /b 0

:hata
echo HATA olustu.
pause
exit /b 1
