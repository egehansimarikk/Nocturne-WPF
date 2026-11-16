using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Nocturne
{
    public class DeviceEqProfile
    {
        public bool Enabled { get; set; }
        public string Preset { get; set; } = "flat";
        public float[] Bands { get; set; } = new float[10];
    }

    public class AppSettings
    {
        public string AudioOutputDeviceId { get; set; } = "";
        public string Language { get; set; } = "auto";
        public bool MinimizeToTrayOnClose { get; set; } = true;
        public string AudioEngineMode { get; set; } = "webview"; // "webview" | "wasapi_shared" | "wasapi_exclusive"
        public string WasapiDeviceId { get; set; } = "";

        public bool EnableDiscordRpc { get; set; } = true;
        public bool EnableTrackOsd { get; set; } = true;
        public bool CheckUpdatesOnStartup { get; set; } = true;

        public bool EqualizerEnabled { get; set; } = false;
        public string EqualizerPreset { get; set; } = "flat";
        public float[] EqualizerBands { get; set; } = new float[10];
        public bool AutoDeviceEqProfile { get; set; } = true;
        public Dictionary<string, DeviceEqProfile> DeviceEqProfiles { get; set; } = new();

        public bool EnableVolumeNormalization { get; set; } = false;
        public bool EnableSpectrumVisualizer { get; set; } = true;

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
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    if (loaded.EqualizerBands == null || loaded.EqualizerBands.Length != 10)
                        loaded.EqualizerBands = new float[10];
                    loaded.DeviceEqProfiles ??= new Dictionary<string, DeviceEqProfile>();
                    return loaded;
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