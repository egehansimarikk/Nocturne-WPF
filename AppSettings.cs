using System;
using System.IO;
using System.Text.Json;

namespace Nocturne
{
    public class AppSettings
    {
        public string AudioOutputDeviceId { get; set; } = "";
        public string Language { get; set; } = "auto";
        public bool MinimizeToTrayOnClose { get; set; } = true;
        public string AudioEngineMode { get; set; } = "webview"; // "webview" | "wasapi_shared" | "wasapi_exclusive"
        public string WasapiDeviceId { get; set; } = "";

        public bool EnableDiscordRpc { get; set; } = true;
        public bool EnableTrackOsd { get; set; } = true;

        public uint PlayHotkeyModifiers { get; set; } = 3;
        public uint PlayHotkeyVk { get; set; } = 0x20;

        public uint NextHotkeyModifiers { get; set; } = 3;
        public uint NextHotkeyVk { get; set; } = 0x27;

        public uint PrevHotkeyModifiers { get; set; } = 3;
        public uint PrevHotkeyVk { get; set; } = 0x25;

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nocturne",
            "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string? dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}