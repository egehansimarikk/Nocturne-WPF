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
                ["LanguageLabel"] = "Arayüz Dili",
                ["LangAuto"] = "Otomatik (Sistem Dili)",
                ["CloseBehaviorLabel"] = "Sistem ve Bildirimler",
                ["MinimizeToTrayCheck"] = "Pencere kapatıldığında (X) sistem tepsisine küçült",
                ["DiscordRpcCheck"] = "Discord Rich Presence (Dinlenen şarkıyı profilde göster)",
                ["DiscordClientIdLabel"] = "Discord Uygulama ID (İsteğe Bağlı)",
                ["TrackOsdCheck"] = "Arka planda şarkı değişince ekran bildirimi (OSD) göster",
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
                ["TrayShow"] = "Nocturne'ü Aç",
                ["TrayMiniPlayer"] = "Mini Oynatıcıyı Aç",
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
                ["LanguageLabel"] = "Interface Language",
                ["LangAuto"] = "Automatic (System Language)",
                ["CloseBehaviorLabel"] = "System & Notifications",
                ["MinimizeToTrayCheck"] = "Minimize to system tray when closing window (X)",
                ["DiscordRpcCheck"] = "Discord Rich Presence (Show active track on profile)",
                ["DiscordClientIdLabel"] = "Discord Application ID (Optional)",
                ["TrackOsdCheck"] = "Show on-screen notification (OSD) on track change in background",
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
                ["TrayShow"] = "Show Nocturne",
                ["TrayMiniPlayer"] = "Open Mini Player",
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