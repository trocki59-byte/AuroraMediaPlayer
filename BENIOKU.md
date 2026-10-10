# TROÇKİ® AURORA MEDIA PLAYER — Android + iPhone

*Sen İzle, Dünya Güzelleşsin…*

Android telefon/tablet ile iPhone/iPad için video + müzik oynatıcı. Tek ortak proje: C# / .NET 10 MAUI · SQLite · CommunityToolkit MediaElement (Android: ExoPlayer, iOS: AVPlayer).

## Özellikler
- **Otomatik tarama:** Android'de cihazdaki tüm video ve müzikler MediaStore'dan bulunur (SD kart dahil); iPhone için aşağıya bakın
- **SQLite veritabanı:** `aurora.db3` ilk açılışta kendiliğinden oluşur; favoriler, geçmiş ve kaldığınız yer burada saklanır
- **Sekmeler:** Ana Sayfa (yarım kalanlar + son eklenenler), Videolar, Müzikler, Klasörler, Favoriler, Son Oynatılanlar, Ayarlar
- **Oynatıcı:** oynat/duraklat, önceki/sonraki, ±10 sn, karıştır, tekrar (kapalı / tümü / tek), ses çubuğu + sessiz, hız (0.5x–2x), tam ekran (yatay döndürme), ekran yansıtma
- **Dokunma:** videoya çift dokunuş → sağ yarı +10 sn, sol yarı −10 sn; tam ekranda tek dokunuş kontrolleri gösterir/gizler
- **Kaldığı yerden devam**, otomatik sonrakine geçme, arka planda çalma + bildirim ve kilit ekranı kontrolleri
- **Küçük resimler:** video kareleri ve albüm kapakları arka planda üretilir
- **Arama + sıralama** (tarih / ad / süre / boyut)
- **"Birlikte aç":** dosya yöneticisi veya WhatsApp'tan video/müzik doğrudan Aurora'da açılır
- **Uyarlanabilir yerleşim:** tablet yatayda oynatıcı ve liste yan yana, telefonda alt alta

## iPhone / iPad'de neler farklı
iOS, uygulamaların telefondaki tüm dosyaları taramasına izin vermez. Aurora bu yüzden üç kaynaktan okur:
- **Fotoğraflar** uygulamasındaki videolar (iCloud'dakiler oynatırken indirilir)
- **Müzik** arşivindeki şarkılar (bilgisayardan eşitlenen / satın alınan; Apple Music abonelik şarkıları DRM nedeniyle çalınamaz)
- **Aurora klasörü:** menüdeki **Dosya Ekle** (Dosyalar, iCloud Drive, Google Drive…), telefonda *Dosyalar › iPhone'umda › Aurora*, bilgisayarda *Finder › iPhone › Dosyalar › Aurora*, AirDrop veya "Birlikte aç → Aurora"

Diğer her şey (tasarım, oynatıcı, favoriler, kaldığı yer, arka planda çalma, kilit ekranı kontrolleri) aynıdır. Ekran yansıtma butonu iPhone'da **AirPlay** seçicisini açar.

## Derleme — Android (Windows)
1. [.NET 10 SDK](https://dotnet.microsoft.com/download) kurun
2. Komut isteminde bir kez: `dotnet workload install maui-android`
3. **APK** (telefona doğrudan kurulum): `build_apk.bat` dosyasına çift tıklayın → `cikti\` klasörü açılır
4. **Google Play AAB:** önce imza anahtarı oluşturun (komut `build_aab.bat` içinde yazıyor), sonra `build_aab.bat`

Visual Studio 2022/2026 ile de açılabilir: `AuroraMediaPlayer.csproj` → hedef olarak Android cihazı seçin → ▶.

**Bilgisayara kurulum yapmadan:** projeyi GitHub'a yükleyin → **Actions → Android APK → Run workflow** → bitince APK'yı Artifacts bölümünden indirin.

## Derleme — iPhone
iPhone uygulamaları Apple kuralları gereği **yalnızca Mac üzerinde** derlenip imzalanabilir. Üç yol var:

| Yol | Gerekenler | Sonuç |
|---|---|---|
| **A. Kendi Mac'iniz** | Mac + Xcode + .NET 10 + `dotnet workload install maui-ios` + ücretsiz Apple ID | `./build_ios.sh calistir` → USB ile bağlı iPhone'a kurulur (ücretsiz hesapta 7 günde bir yeniden kurmak gerekir) |
| **B. GitHub Actions (Mac'siz)** | GitHub hesabı; imzalı kurulum için Apple Developer Program (yıllık 99 $) | Actions › **iOS** › Run workflow. Sertifika eklenmişse imzalı `.ipa` üretilir → TestFlight / App Store'a yüklenir |
| **C. Windows + Visual Studio** | Ağdaki bir Mac ("Pair to Mac") | Visual Studio'dan doğrudan iPhone'a dağıtım |

İlk kez ücretsiz Apple ID ile kurulumda: Xcode'da aynı Bundle ID (`com.trocki.auroramediaplayer`) ile boş bir uygulama oluşturup iPhone'a bir kez çalıştırın (imza profili böylece oluşur), sonra iPhone'da *Ayarlar › Genel › VPN ve Aygıt Yönetimi* bölümünden geliştiriciye güvenin.

**Hedef platformlar:** Windows'ta proje yalnızca **Android** olarak açılır (iOS iş yükü kurmanız gerekmez). Mac'te Android + iOS birlikte gelir. Windows'ta "Pair to Mac" ile iPhone'a derleyecekseniz komuta `-p:AuroraTarget=all` ekleyin.

## Sık karşılaşılan derleme hatası
**"İşlem, başka bir işlem tarafından kullanıldığından … appicon_round.png dosyasına erişemiyor"**: Simge üretilirken dosya kilitlenmiştir (genellikle antivirüs ya da OneDrive taraması). Kodla ilgisi yoktur. Çözüm:
1. Visual Studio'da **Derle › Çözümü Temizle**
2. Proje klasöründeki `bin` ve `obj` klasörlerini silin
3. **Derle › Çözümü Yeniden Derle**

Tekrarlarsa projeyi `İndirilenler` dışında bir klasöre taşıyın (ör. `C:\Projeler\AuroraMediaPlayer`) veya bu klasörü Windows Defender taramasından hariç tutun.

## Klasör yapısı
```
Models/        MediaItem, FolderInfo, biçimlendiriciler
Data/          AuroraDatabase (SQLite)
Services/      MediaLibrary, PlayerController, ayarlar, izinler
ViewModels/    MainViewModel
Views/         NavTabButton (cam kart sekme)
Platforms/Android/  MediaStore tarayıcı, tam ekran, manifest
Platforms/iOS/      Fotoğraflar + Müzik + Dosyalar tarayıcı, AirPlay, Info.plist
Resources/     simgeler, logo, aurora arka planları, Poppins yazı tipi
```

## Kimlik
- Paket adı / Bundle ID: `com.trocki.auroramediaplayer`
- En düşük sürüm: Android 8.0 (API 26) · iOS 15
- Sürüm: 1.0.0 (1) — yeni Play / App Store yüklemesinde `.csproj` içindeki `ApplicationVersion` değerini artırın

© TROÇKİ® Software — Düşün • Keşfet • Üret • Paylaş
