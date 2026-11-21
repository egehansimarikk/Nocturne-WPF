> **⚠️ Legacy / Archived Version:** This repository hosts the archived **v0.1.3** release of Nocturne built with C# (.NET 10 WPF). Active development continues in the new Rust + GPUI repository: **[Nocturne](https://github.com/egehansimarikk/Nocturne)**.
>
> **⚠️ Legacy / Arşiv Sürümü:** Bu depo Nocturne'ün C# (.NET 10 WPF) ile geliştirilmiş **v0.1.3** sürümünü barındırır ve arşivlenmiştir. Aktif geliştirme Rust + GPUI tabanlı yeni **[Nocturne](https://github.com/egehansimarikk/Nocturne)** deposunda devam etmektedir.

# Nocturne (WPF Legacy)

<p align="center">
  <img src="Assets/nocturne.ico" alt="Nocturne Logo" width="96" height="96" />
</p>

<p align="center">
  <strong>A lightweight, audiophile-grade Windows desktop client for Deezer HiFi built with C#, .NET 10 WPF, and WebView2.</strong><br/>
  <em>C#, .NET 10 WPF ve WebView2 ile geliştirilmiş; odyofil odaklı, hafif ve modern Deezer masaüstü istemcisi.</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Status-Archived%20(Legacy)-orange?style=flat-square" alt="Archived" />
  <img src="https://img.shields.io/badge/Version-v0.1.3-A238FF?style=flat-square" alt="Version v0.1.3" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2F11%20(x64)-0078D6?style=flat-square&logo=windows" alt="Windows x64" />
  <img src="https://img.shields.io/badge/Audio-WASAPI%20Exclusive%20%2F%20Shared-A238FF?style=flat-square" alt="WASAPI" />
  <img src="https://img.shields.io/badge/Discord-Rich%20Presence-5865F2?style=flat-square&logo=discord" alt="Discord RPC" />
</p>

---

## English

### What's New in v0.1.3

- **10-Band DSP Equalizer, AutoEQ & Custom Profiles:**
  - Built-in audiophile target curves including **Harman Over-Ear**, **Harman / Crinacle IEM**, **Warm Acoustic**, **Vocal Forward**, and **Bass Boost**.
  - Import standard **AutoEQ / EqualizerAPO (`.txt`)** profiles directly, or save and manage your own custom named presets.
  - **Per-Device EQ Memory:** Automatically remembers and switches to the assigned EQ curve for each DAC or headphone.
  - **System Tray Quick EQ Menu:** Switch between built-in and custom EQ presets in one click directly from the tray icon.
- **Single-Device WASAPI Process Loopback & Direct Exclusive Mode:**
  - Uses Windows Process Loopback (`VAD\Process_Loopback`) to capture Nocturne's audio stream in isolation—enabling **WASAPI Exclusive (Bit-Perfect)** and real-time DSP even on single-output systems (e.g., laptops with a single USB-C DAC/dongle).
  - Live hardware sample-rate and bit-depth badge (`44.1 / 48 / 96 / 192 kHz • 16 / 24 / 32-bit`) in Settings and the top bar.
- **Volume Normalization & Live FFT Spectrum Visualizer:**
  - Built-in RMS loudness leveling with a soft limiter (`NORM`) and an optional real-time FFT spectrum analyzer in the top bar.
- **Faster Cold Startup & Multi-Language Installer:**
  - Parallelized WebView2 environment initialization and offloaded COM audio device queries for significantly faster startup.
  - Installer (`NocturneSetup-0.1.3-win-x64.exe`) now supports 20+ languages with automatic Windows UI language detection.

### Core Features

- **Triple Audio Engine Architecture:**
  - **WebView2 Direct:** Standard output with per-app audio device routing via `setSinkId`.
  - **WASAPI Shared:** Low-latency audio bridge with full 10-band DSP and spectrum monitoring.
  - **WASAPI Exclusive (Bit-Perfect):** Bypasses the Windows audio mixer to stream directly to external USB/Type-C DACs and AMPs.
- **Hot-Plug DAC Auto-Switching:** Automatically falls back to internal speakers when an external DAC is unplugged and seamlessly re-engages WASAPI routing when reconnected.
- **Discord Rich Presence:** Displays *Listening to Nocturne* with track title, artist, album cover art, and a live progress bar.
- **Windows SMTC Integration:** Full native support for Windows 10/11 media flyout cards, album thumbnails, and lock-screen controls.
- **Compact Mini Player & System Tray:** Detachable always-on-top mini player with seek bar, click-through lock mode, and minimize-to-tray support.
- **Click-Through Track OSD:** Non-intrusive, focus-safe (`WS_EX_NOACTIVATE`) dark overlay notification when tracks change in the background or during full-screen gaming.
- **Customizable Global Hotkeys:** Supports physical media keys alongside customizable system-wide key combinations.
- **Bilingual UI:** Automatic or manual switching between English and Turkish.

### Requirements

- Windows 10 (19041+) or Windows 11 (x64)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (pre-installed on Windows 11)
- [.NET 10 SDK](https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.404/dotnet-sdk-10.0.404-win-x64.exe) *(only required if building from source)*

### Build & Publish from Source

```powershell
# Clone the repository
git clone https://github.com/egehansimarikk/Nocturne.git
cd Nocturne

# Publish self-contained Windows x64 Release build
dotnet publish -c Release -r win-x64 --self-contained true -o .\publish\Nocturne-win-x64
```

To build the Windows installer (`NocturneSetup-0.1.3-win-x64.exe`), open `NocturneSetup.iss` with [Inno Setup](https://jrsoftware.org/isinfo.php) and compile (`Ctrl + F9`).

---

## Türkçe

### v0.1.3 Sürüm Yenilikleri

- **10-Bant DSP Ekolayzır, AutoEQ ve Özel Profiller:**
  - **Harman Over-Ear**, **Harman / Crinacle IEM**, **Warm Acoustic**, **Vocal Forward** ve **Bass Boost** gibi yerleşik odyofil hedef eğrileri.
  - Standart **AutoEQ / EqualizerAPO (`.txt`)** dosyalarını doğrudan içe aktarma veya kendi özel profillerinizi isimlendirip kaydetme/silme desteği.
  - **Cihaz Bazlı Otomatik EQ Hafızası:** Bağladığınız her kulaklık veya DAC için atadığınız EQ profilini otomatik hatırlar ve uygular.
  - **Sistem Tepsisi Hızlı EQ Menüsü:** Sağ alt tepsi ikonuna sağ tıklayarak yerleşik ve özel EQ profilleri arasında tek tıkla geçiş yapabilirsiniz.
- **Tek-Cihaz WASAPI Process Loopback ve Direct Exclusive Modu:**
  - Windows Process Loopback (`VAD\Process_Loopback`) teknolojisiyle yalnızca Nocturne sürecinin sesini izole yakalar; böylece tek ses çıkışına sahip dizüstü bilgisayarlarda veya tek USB-C DAC/dongle kurulumlarında bile **WASAPI Exclusive (Bit-Perfect)** ve canlı DSP çalışır.
  - Ayarlar penceresinde ve üst barda donanımın anlık örnekleme hızı ve bit derinliği rozeti (`44.1 / 48 / 96 / 192 kHz • 16 / 24 / 32-bit`).
- **Ses Normalizasyonu ve Canlı FFT Spektrum Analizörü:**
  - Yumuşak limiter destekli RMS ses seviyesi dengeleme (`NORM`) ve üst barda gerçek zamanlı FFT frekans çubukları.
- **Hızlandırılmış Soğuk Açılış ve Çok Dilli Kurulum:**
  - Paralel WebView2 ortam başlatma ve arka plan COM cihaz taraması ile çok daha hızlı açılış süresi.
  - Kurulum sihirbazı (`NocturneSetup-0.1.3-win-x64.exe`) artık Windows dilini otomatik algılayan 20'den fazla dil desteği sunar.

### Temel Özellikler

- **Üç Modlu Ses Motoru Mimarisi:**
  - **WebView2 Doğrudan:** `setSinkId` desteğiyle uygulama bazlı ses çıkış cihazı yönlendirme.
  - **WASAPI Shared:** NAudio tabanlı düşük gecikmeli ses köprüsü, 10-bant DSP ve canlı spektrum izleme.
  - **WASAPI Exclusive (Bit-Perfect):** Windows ses karıştırıcısını (mixer) baypas ederek harici USB/Type-C DAC ve amfilere doğrudan kayıpsız ses aktarımı.
- **Otomatik DAC Tak/Çıkar Geçişi:** Harici DAC çıkarıldığında müziği kesmeden dahili hoparlöre döner, tekrar takıldığında otomatik olarak WASAPI moduna geri geçer.
- **Discord Rich Presence:** Dinlenen şarkı adını, sanatçıyı, albüm kapağını ve canlı ilerleme çubuğunu Discord profilinde *"Nocturne dinliyor"* olarak gösterir.
- **Windows Medya Kartı (SMTC) Entegrasyonu:** Windows 10/11 ses kontrol kartı, albüm kapağı ve kilit ekranı medya tuşlarıyla tam uyumludur.
- **Mini Oynatıcı ve Sistem Tepsisi:** Her zaman üstte kalabilen kompakt mini oynatıcı, tıklama kilidi ve kapatıldığında sistem tepsisine küçültme desteği.
- **Oyun Dostu Şarkı Bildirimi (OSD):** Uygulama arka plandayken veya oyun içindeyken odak çalmayan (`WS_EX_NOACTIVATE`) koyu tema şarkı değişim bildirimi.
- **Özelleştirilebilir Global Kısayollar:** Klavye medya tuşlarının yanı sıra Ayarlar menüsünden atanabilen global tuş kombinasyonları.
- **Çift Dil Desteği:** Sistem diline göre otomatik veya manuel Türkçe / İngilizce arayüz.

### Gereksinimler

- Windows 10 (19041+) veya Windows 11 (x64)
- [Microsoft Edge WebView2 Çalışma Zamanı](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (Windows 11'de yüklü gelir)
- [.NET 10 SDK](https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.404/dotnet-sdk-10.0.404-win-x64.exe) *(yalnızca kaynak koddan derlemek için gereklidir)*

### Kaynak Koddan Derleme

```powershell
# Depoyu klonlayın
git clone https://github.com/egehansimarikk/Nocturne.git
cd Nocturne

# Bağımsız (self-contained) Windows x64 Release çıktısı alın
dotnet publish -c Release -r win-x64 --self-contained true -o .\publish\Nocturne-win-x64
```

Kurulum sihirbazı (`NocturneSetup-0.1.3-win-x64.exe`) oluşturmak için `NocturneSetup.iss` dosyasını [Inno Setup](https://jrsoftware.org/isinfo.php) ile açıp derleyin (`Ctrl + F9`).

---

## Tech Stack / Kullanılan Teknolojiler

- **Framework:** .NET 10 (WPF)
- **Web Engine:** Microsoft.Web.WebView2
- **Audio Engine:** NAudio (WASAPI Process Loopback & Exclusive/Shared Render)
- **Discord Integration:** DiscordRichPresence

## License / Lisans

MIT License — Copyright (c) 2026 [egehansimarikk](https://github.com/egehansimarikk)
