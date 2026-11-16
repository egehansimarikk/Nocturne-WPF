# Nocturne

<p align="center">
  <img src="Assets/nocturne.ico" alt="Nocturne Logo" width="96" height="96" />
</p>

<p align="center">
  <strong>A lightweight, audiophile-grade Windows desktop client for Deezer HiFi built with C#, .NET 10 WPF, and WebView2.</strong><br/>
  <em>C#, .NET 10 WPF ve WebView2 ile geliştirilmiş; odyofil odaklı, hafif ve modern Deezer masaüstü istemcisi.</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2F11%20(x64)-0078D6?style=flat-square&logo=windows" alt="Windows x64" />
  <img src="https://img.shields.io/badge/Audio-WASAPI%20Exclusive%20%2F%20Shared-A238FF?style=flat-square" alt="WASAPI" />
  <img src="https://img.shields.io/badge/Discord-Rich%20Presence-5865F2?style=flat-square&logo=discord" alt="Discord RPC" />
</p>

---

## English

### Features

- **Triple Audio Engine Architecture:**
  - **WebView2 Direct:** Standard output with per-app audio device routing via `setSinkId`.
  - **WASAPI Shared:** Low-latency audio bridge powered by NAudio.
  - **WASAPI Exclusive (Bit-Perfect):** Bypasses the Windows audio mixer to stream directly to external USB/Type-C DACs and AMPs.
- **Hot-Plug DAC Auto-Switching:** Automatically falls back to internal speakers when an external DAC is unplugged and seamlessly re-engages WASAPI routing when reconnected.
- **Discord Rich Presence:** Displays *Listening to Nocturne* with track title, artist, album cover art, and a live progress bar.
- **Windows SMTC Integration:** Full native support for Windows 10/11 media flyout cards, album thumbnails, and lock-screen controls.
- **Compact Mini Player & System Tray:** Detachable always-on-top mini player with seek bar and minimize-to-tray support.
- **Click-Through Track OSD:** Non-intrusive, focus-safe (`WS_EX_NOACTIVATE`) dark overlay notification when tracks change in the background or during full-screen gaming.
- **Customizable Global Hotkeys:** Supports physical media keys alongside customizable system-wide key combinations.
- **Bilingual UI:** Automatic or manual switching between English and Turkish.

### Requirements

- Windows 10 (19041+) or Windows 11 (x64)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (pre-installed on Windows 11)
- [.NET 10 SDK](https://dotnet.microsoft.com/) *(only required if building from source)*

### Build & Publish from Source

```powershell
# Clone the repository
git clone [https://github.com/egehansimarikk/Nocturne.git](https://github.com/egehansimarikk/Nocturne.git)
cd Nocturne

# Publish self-contained Windows x64 Release build
dotnet publish -c Release -r win-x64 --self-contained true -o .\publish\Nocturne-win-x64
```

To build the Windows installer (`NocturneSetup-1.0.0-win-x64.exe`), open `NocturneSetup.iss` with [Inno Setup](https://jrsoftware.org/isinfo.php) and compile (`Ctrl + F9`).

---

## Türkçe

### Özellikler

- **Üç Modlu Ses Motoru Mimarisi:**
  - **WebView2 Doğrudan:** `setSinkId` desteğiyle uygulama bazlı ses çıkış cihazı yönlendirme.
  - **WASAPI Shared:** NAudio tabanlı düşük gecikmeli ses köprüsü.
  - **WASAPI Exclusive (Bit-Perfect):** Windows ses karıştırıcısını (mixer) baypas ederek harici USB/Type-C DAC ve amfilere doğrudan kayıpsız ses aktarımı.
- **Otomatik DAC Tak/Çıkar Geçişi:** Harici DAC çıkarıldığında müziği kesmeden dahili hoparlöre döner, tekrar takıldığında otomatik olarak WASAPI moduna geri geçer.
- **Discord Rich Presence:** Dinlenen şarkı adını, sanatçıyı, albüm kapağını ve canlı ilerleme çubuğunu Discord profilinde *"Nocturne dinliyor"* olarak gösterir.
- **Windows Medya Kartı (SMTC) Entegrasyonu:** Windows 10/11 ses kontrol kartı, albüm kapağı ve kilit ekranı medya tuşlarıyla tam uyumludur.
- **Mini Oynatıcı ve Sistem Tepsisi:** Her zaman üstte kalabilen kompakt mini oynatıcı ve kapatıldığında sistem tepsisine küçültme desteği.
- **Oyun Dostu Şarkı Bildirimi (OSD):** Uygulama arka plandayken veya oyun içindeyken odak çalmayan (`WS_EX_NOACTIVATE`) koyu tema şarkı değişim bildirimi.
- **Özelleştirilebilir Global Kısayollar:** Klavye medya tuşlarının yanı sıra Ayarlar menüsünden atanabilen global tuş kombinasyonları.
- **Çift Dil Desteği:** Sistem diline göre otomatik veya manuel Türkçe / İngilizce arayüz.

### Gereksinimler

- Windows 10 (19041+) veya Windows 11 (x64)
- [Microsoft Edge WebView2 Çalışma Zamanı](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (Windows 11'de yüklü gelir)
- [.NET 10 SDK]([https://dotnet.microsoft.com/](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-10.0.404-windows-x64-installer)) *(yalnızca kaynak koddan derlemek için gereklidir)*

### Kaynak Koddan Derleme

```powershell
# Depoyu klonlayın
git clone [https://github.com/egehansimarikk/Nocturne.git](https://github.com/egehansimarikk/Nocturne.git)
cd Nocturne

# Bağımsız (self-contained) Windows x64 Release çıktısı alın
dotnet publish -c Release -r win-x64 --self-contained true -o .\publish\Nocturne-win-x64
```

Kurulum sihirbazı (`NocturneSetup-1.0.0-win-x64.exe`) oluşturmak için `NocturneSetup.iss` dosyasını [Inno Setup](https://jrsoftware.org/isinfo.php) ile açıp derleyin (`Ctrl + F9`).

---

## Tech Stack / Kullanılan Teknolojiler

- **Framework:** .NET 10 (WPF)
- **Web Engine:** Microsoft.Web.WebView2
- **Audio Engine:** NAudio (WASAPI Loopback & Exclusive/Shared Render)
- **Discord Integration:** DiscordRichPresence

## License / Lisans

MIT License — Copyright (c) 2026 [egehansimarikk](https://github.com/egehansimarikk)
