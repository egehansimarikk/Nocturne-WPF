using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        public static readonly float[] EqFrequencies =
        {
            32f, 64f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f
        };

        public static readonly IReadOnlyDictionary<string, float[]> BuiltInEqPresets =
            new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["flat"] = new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f },
                ["harman_over_ear"] = new[] { 4.5f, 3.5f, 1.0f, -1.0f, -0.5f, 0.0f, 1.5f, 3.0f, 2.0f, 1.0f },
                ["harman_iem"] = new[] { 5.0f, 3.0f, 0.0f, -1.5f, -0.5f, 0.5f, 2.5f, 3.5f, 1.5f, 0.5f },
                ["warm_acoustic"] = new[] { 3.0f, 2.5f, 1.5f, 0.5f, 0.0f, -0.5f, -1.0f, -2.0f, -3.0f, -3.5f },
                ["vocal_forward"] = new[] { -1.5f, -1.0f, 0.0f, 1.0f, 2.5f, 3.5f, 3.0f, 1.5f, 0.0f, -0.5f },
                ["bass_boost"] = new[] { 6.0f, 4.5f, 2.5f, 0.5f, 0.0f, 0.0f, 0.5f, 1.0f, 1.0f, 0.5f }
            };

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
        public Dictionary<string, float[]> CustomEqPresets { get; set; } = new();

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

        public bool TryGetEqPresetBands(string presetKey, out float[] bands)
        {
            if (BuiltInEqPresets.TryGetValue(presetKey, out var builtIn))
            {
                bands = (float[])builtIn.Clone();
                return true;
            }

            if (CustomEqPresets.TryGetValue(presetKey, out var custom) && custom != null && custom.Length == 10)
            {
                bands = (float[])custom.Clone();
                return true;
            }

            bands = new float[10];
            return false;
        }

        public static bool TryParseAutoEqText(string rawText, out float[] parsedBands)
        {
            parsedBands = new float[10];
            if (string.IsNullOrWhiteSpace(rawText))
                return false;

            float preamp = 0f;
            var preampMatch = Regex.Match(rawText, @"Preamp:\s*([+-]?\d+(?:\.\d+)?)\s*dB", RegexOptions.IgnoreCase);
            if (preampMatch.Success &&
                float.TryParse(preampMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedPreamp))
            {
                preamp = parsedPreamp;
            }

            var filterMatches = Regex.Matches(
                rawText,
                @"Filter\s+\d+:\s*ON\s+[A-Z]+\s+Fc\s+(\d+(?:\.\d+)?)\s*Hz\s+Gain\s+([+-]?\d+(?:\.\d+)?)\s*dB",
                RegexOptions.IgnoreCase);

            if (filterMatches.Count == 0)
                return false;

            var points = new List<(float Freq, float Gain)>();
            foreach (Match m in filterMatches)
            {
                if (float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float freq) &&
                    float.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float gain))
                {
                    points.Add((freq, gain + preamp));
                }
            }

            if (points.Count == 0)
                return false;

            for (int i = 0; i < EqFrequencies.Length; i++)
            {
                float targetFreq = EqFrequencies[i];
                float bestGain = 0f;
                float minLogDist = float.MaxValue;

                foreach (var pt in points)
                {
                    float dist = MathF.Abs(MathF.Log2(targetFreq) - MathF.Log2(MathF.Max(pt.Freq, 1f)));
                    if (dist < minLogDist)
                    {
                        minLogDist = dist;
                        bestGain = pt.Gain;
                    }
                }

                parsedBands[i] = Math.Clamp(MathF.Round(bestGain, 1), -12f, 12f);
            }

            return true;
        }

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
                    loaded.CustomEqPresets ??= new Dictionary<string, float[]>();

                    var normalizedCustom = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in loaded.CustomEqPresets)
                    {
                        if (!string.IsNullOrWhiteSpace(kv.Key) && kv.Value != null && kv.Value.Length == 10)
                            normalizedCustom[kv.Key] = kv.Value;
                    }
                    loaded.CustomEqPresets = normalizedCustom;

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