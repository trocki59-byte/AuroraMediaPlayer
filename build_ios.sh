#!/bin/bash
# ============================================================
#  TROÇKİ AURORA MEDIA PLAYER - iPhone (Mac'te çalıştırın)
#  Gerekli: Xcode + .NET 10 SDK + "dotnet workload install maui-ios"
# ------------------------------------------------------------
#  Kullanım:
#    ./build_ios.sh calistir      → USB ile bağlı iPhone'a kurar ve açar
#                                    (Xcode'da Apple ID'nizle oturum açmış olmanız yeterli)
#    ./build_ios.sh ipa           → App Store / TestFlight için imzalı .ipa
#                                    (Apple Developer Program üyeliği gerekir)
# ============================================================
set -e
cd "$(dirname "$0")"

case "$1" in
  calistir|"")
    echo "iPhone'a kuruluyor..."
    dotnet build AuroraMediaPlayer.csproj -f net10.0-ios -c Debug -p:AuroraTarget=ios \
      -p:RuntimeIdentifier=ios-arm64 -t:Run
    ;;
  ipa)
    read -p "İmza sertifikası adı (ör. Apple Distribution: Ad Soyad (TEAMID)): " KEY
    read -p "Provisioning profile adı: " PROFILE
    dotnet publish AuroraMediaPlayer.csproj -f net10.0-ios -c Release -p:AuroraTarget=ios \
      -p:CodesignKey="$KEY" -p:CodesignProvision="$PROFILE" -o cikti_ios
    echo "TAMAM: $(ls cikti_ios/*.ipa)"
    open cikti_ios
    ;;
  *)
    echo "Kullanım: ./build_ios.sh [calistir|ipa]"
    ;;
esac
