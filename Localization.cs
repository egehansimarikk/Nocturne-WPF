using System.Collections.Generic;
using System.Globalization;

namespace Nocturne
{
    public static class Localization
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
        {
            ["tr"] = new Dictionary<string, string>
            {
                ["SettingsTitle"] = "Nocturne Ayarlar",
                ["AudioEngineLabel"] = "Ses Motoru Modu",
                ["EngineWebView"] = "WebView2 Doğrudan (Standart)",
                ["EngineWasapiShared"] = "WASAPI Shared (Düşük Gecikme)",
                ["EngineWasapiExclusive"] = "WASAPI Exclusive (DAC/AMP Bit-Perfect)",
                ["AudioOutputLabel"] = "Ses Çıkış Cihazı",
                ["DefaultDevice"] = "Sistem Varsayılanı",
                ["RefreshDevices"] = "Yenile",
                ["EqSectionLabel"] = "10-Bant Ekolayzır & DSP",
                ["EqEnableCheck"] = "Ekolayzırı Etkinleştir",
                ["AutoDeviceEqCheck"] = "Her ses cihazı için kendi EQ profilini hatırla",
                ["VolumeNormCheck"] = "Ses Normalizasyonu ve Yumuşak Limiter (Parça seviyesi dengeleme)",
                ["VisualizerCheck"] = "Üst barda canlı FFT spektrum analizörünü göster",
                ["EqPresetFlat"] = "Düz (Flat)",
                ["EqPresetBass"] = "Bas Vurgusu (Bass Boost)",
                ["EqPresetVocal"] = "Vokal & Akustik",
                ["EqPresetVShape"] = "V-Shape (IEM Dinamik)",
                ["EqPresetTreble"] = "Tiz Netliği",
                ["EqPresetCustom"] = "Özel",
                ["LanguageLabel"] = "Arayüz Dili",
                ["LangAuto"] = "Otomatik (Sistem Dili)",
                ["CloseBehaviorLabel"] = "Sistem ve Bildirimler",
                ["MinimizeToTrayCheck"] = "Pencere kapatıldığında (X) sistem tepsisine küçült",
                ["DiscordRpcCheck"] = "Discord Rich Presence (Dinlenen şarkıyı profilde göster)",
                ["TrackOsdCheck"] = "Arka planda şarkı değişince ekran bildirimi (OSD) göster",
                ["UpdateCheckBox"] = "Açılışta GitHub üzerinden yeni sürüm kontrol et",
                ["HotkeysLabel"] = "Global Medya Kısayolları (Tıkla ve Tuşa Bas)",
                ["HotkeyPlayLabel"] = "Oynat / Duraklat:",
                ["HotkeyNextLabel"] = "Sonraki Parça:",
                ["HotkeyPrevLabel"] = "Önceki Parça:",
                ["HotkeyPressPrompt"] = "Tuş kombinasyonuna basın...",
                ["WasapiNote"] = "Not: WASAPI Exclusive modunda Windows ses efektleri baypas edilir ve dahili hoparlör çıkışı otomatik susturulur.",
                ["SaveButton"] = "Kaydet ve Uygula",
                ["CancelButton"] = "İptal",
                ["SettingsTooltip"] = "Ayarlar",
                ["MiniPlayerTooltip"] = "Mini Oynatıcı",
                ["ClickThroughTooltip"] = "Tıklama Geçiren Mod (Kilitle - Kapatmak için tepsi simgesini kullan)",
                ["UpdateAvailableBadge"] = "Güncelleme Mevcut",
                ["TrayShow"] = "Nocturne'ü Aç",
                ["TrayMiniPlayer"] = "Mini Oynatıcıyı Aç",
                ["TrayUnlockMini"] = "Mini Oynatıcı Kilidini Aç",
                ["TrayExit"] = "Tamamen Çık",
                ["NothingPlaying"] = "Şu an çalan parça yok"
            },
            ["en"] = new Dictionary<string, string>
            {
                ["SettingsTitle"] = "Nocturne Settings",
                ["AudioEngineLabel"] = "Audio Engine Mode",
                ["EngineWebView"] = "WebView2 Direct (Standard)",
                ["EngineWasapiShared"] = "WASAPI Shared (Low Latency)",
                ["EngineWasapiExclusive"] = "WASAPI Exclusive (DAC/AMP Bit-Perfect)",
                ["AudioOutputLabel"] = "Audio Output Device",
                ["DefaultDevice"] = "System Default",
                ["RefreshDevices"] = "Refresh",
                ["EqSectionLabel"] = "10-Band Equalizer & DSP",
                ["EqEnableCheck"] = "Enable Equalizer",
                ["AutoDeviceEqCheck"] = "Remember separate EQ profile per audio device",
                ["VolumeNormCheck"] = "Volume Normalization & Soft Limiter (Track loudness leveling)",
                ["VisualizerCheck"] = "Show live FFT spectrum analyzer in top header bar",
                ["EqPresetFlat"] = "Flat",
                ["EqPresetBass"] = "Bass Boost",
                ["EqPresetVocal"] = "Vocal & Acoustic",
                ["EqPresetVShape"] = "V-Shape (IEM Dynamic)",
                ["EqPresetTreble"] = "Treble Clarity",
                ["EqPresetCustom"] = "Custom",
                ["LanguageLabel"] = "Interface Language",
                ["LangAuto"] = "Automatic (System Language)",
                ["CloseBehaviorLabel"] = "System & Notifications",
                ["MinimizeToTrayCheck"] = "Minimize to system tray when closing window (X)",
                ["DiscordRpcCheck"] = "Discord Rich Presence (Show active track on profile)",
                ["TrackOsdCheck"] = "Show on-screen notification (OSD) on track change in background",
                ["UpdateCheckBox"] = "Check for new GitHub releases on startup",
                ["HotkeysLabel"] = "Global Media Hotkeys (Click & Press Keys)",
                ["HotkeyPlayLabel"] = "Play / Pause:",
                ["HotkeyNextLabel"] = "Next Track:",
                ["HotkeyPrevLabel"] = "Previous Track:",
                ["HotkeyPressPrompt"] = "Press key combination...",
                ["WasapiNote"] = "Note: WASAPI Exclusive bypasses Windows audio effects and automatically mutes the internal speaker.",
                ["SaveButton"] = "Save & Apply",
                ["CancelButton"] = "Cancel",
                ["SettingsTooltip"] = "Settings",
                ["MiniPlayerTooltip"] = "Mini Player",
                ["ClickThroughTooltip"] = "Click-Through Overlay Mode (Unlock via System Tray)",
                ["UpdateAvailableBadge"] = "Update Available",
                ["TrayShow"] = "Show Nocturne",
                ["TrayMiniPlayer"] = "Open Mini Player",
                ["TrayUnlockMini"] = "Unlock Mini Player",
                ["TrayExit"] = "Exit Completely",
                ["NothingPlaying"] = "Nothing playing"
            }
        };

        public static string ResolveLanguageCode(string configuredLang)
        {
            if (!string.IsNullOrWhiteSpace(configuredLang) && configuredLang != "auto")
                return Strings.ContainsKey(configuredLang) ? configuredLang : "en";

            string sysLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            return Strings.ContainsKey(sysLang) ? sysLang : "en";
        }

        public static string Get(string key, string configuredLang = "auto")
        {
            string lang = ResolveLanguageCode(configuredLang);
            if (Strings.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var val))
                return val;
            return Strings["en"].TryGetValue(key, out var fallback) ? fallback : key;
        }
    }
}