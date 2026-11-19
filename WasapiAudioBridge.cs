using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Nocturne
{
    public class WasapiDeviceInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string FormatSummary { get; set; } = "";
    }

    public sealed class WasapiAudioBridge : IMMNotificationClient, IDisposable
    {
        private MMDeviceEnumerator? _deviceEnumerator;
        private WasapiLoopbackCapture? _capture;
        private WasapiOut? _output;
        private BufferedWaveProvider? _buffer;
        private MediaFoundationResampler? _resampler;
        private EqualizerSampleProvider? _equalizer;
        private string _mutedEndpointId = "";
        private bool _previousEndpointMuteState = false;
        private bool _eqEnabled;
        private bool _normEnabled;
        private float[] _eqGains = new float[10];
        private readonly object _lock = new();

        public bool IsRunning { get; private set; }
        public string ActiveCaptureDeviceId { get; private set; } = "";
        public string ActiveTargetDeviceId { get; private set; } = "";
        public string ActiveStreamBadge { get; private set; } = "WebView2 • Standard";

        public event Action? AudioDevicesChanged;

        public WasapiAudioBridge()
        {
            try
            {
                _deviceEnumerator = new MMDeviceEnumerator();
                _deviceEnumerator.RegisterEndpointNotificationCallback(this);
            }
            catch { }
        }

        public void SetDsp(bool eqEnabled, float[] gains, bool normalizationEnabled)
        {
            lock (_lock)
            {
                _eqEnabled = eqEnabled;
                _normEnabled = normalizationEnabled;
                _eqGains = gains != null && gains.Length == 10 ? (float[])gains.Clone() : new float[10];
                _equalizer?.UpdateDsp(_eqEnabled, _eqGains, _normEnabled);
            }
        }

        public float[] GetSpectrumSnapshot()
        {
            lock (_lock)
            {
                return _equalizer?.GetSpectrumSnapshot() ?? new float[EqualizerSampleProvider.SpectrumBarCount];
            }
        }

        public static string GetDeviceFormatSummary(string deviceId)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                MMDevice dev = string.IsNullOrWhiteSpace(deviceId)
                    ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                    : enumerator.GetDevice(deviceId);
                using (dev)
                {
                    var fmt = dev.AudioClient.MixFormat;
                    double khz = Math.Round(fmt.SampleRate / 1000.0, 1);
                    return $"{khz:0.#} kHz / {fmt.BitsPerSample}-bit";
                }
            }
            catch
            {
                return "48 kHz / 32-bit";
            }
        }

        public static List<WasapiDeviceInfo> GetOutputDevices()
        {
            var list = new List<WasapiDeviceInfo>();
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (var dev in endpoints)
                {
                    var fmt = dev.AudioClient.MixFormat;
                    double khz = Math.Round(fmt.SampleRate / 1000.0, 1);
                    list.Add(new WasapiDeviceInfo
                    {
                        Id = dev.ID,
                        Name = dev.FriendlyName,
                        FormatSummary = $"{khz:0.#} kHz / {fmt.BitsPerSample}-bit"
                    });
                }
            }
            catch { }
            return list;
        }

        private static IWaveProvider BuildExclusivePcmProvider(MMDevice targetDevice, ISampleProvider eqProvider, out int negotiatedBits)
        {
            int rate = eqProvider.WaveFormat.SampleRate;
            int channels = eqProvider.WaveFormat.Channels;

            var pcm24 = new WaveFormat(rate, 24, channels);
            if (targetDevice.AudioClient.IsFormatSupported(AudioClientShareMode.Exclusive, pcm24))
            {
                negotiatedBits = 24;
                return new SampleToWaveProvider24(eqProvider);
            }

            negotiatedBits = 16;
            return new SampleToWaveProvider16(eqProvider);
        }

        public (bool Success, string ErrorMessage) Start(string deviceId, bool exclusiveMode)
        {
            Stop();

            lock (_lock)
            {
                try
                {
                    using var enumerator = new MMDeviceEnumerator();
                    var allActive = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
                    if (allActive.Count == 0)
                        return (false, "Aktif ses çıkış cihazı bulunamadı.");

                    var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                    MMDevice targetDevice = string.IsNullOrWhiteSpace(deviceId)
                        ? defaultDevice
                        : (allActive.FirstOrDefault(d => d.ID == deviceId) ?? defaultDevice);

                    var mixFmt = targetDevice.AudioClient.MixFormat;
                    double khz = Math.Round(mixFmt.SampleRate / 1000.0, 1);

                    // Hedef dışındaki ikinci ses çıkışını yakalama köprüsü olarak kullan
                    MMDevice? captureDevice = allActive.FirstOrDefault(d => d.ID != targetDevice.ID);

                    // Tek cihazlı kurulum:
                    // WebView2 DRM korumalı sesi doğrudan seçilen cihaza verir;
                    // Exclusive kilidi açılırsa Windows Shared Mixer kapanıp WebView2'yi susturacağı için
                    // tek cihazda güvenli Shared Direct + Loopback analizör modunda kesintisiz çalar.
                    if (captureDevice == null)
                    {
                        _capture = new WasapiLoopbackCapture(targetDevice);
                        _buffer = new BufferedWaveProvider(_capture.WaveFormat)
                        {
                            BufferDuration = TimeSpan.FromMilliseconds(250),
                            DiscardOnBufferOverflow = true
                        };
                        _equalizer = new EqualizerSampleProvider(_buffer.ToSampleProvider());
                        _equalizer.UpdateDsp(_eqEnabled, _eqGains, false);

                        float[] monitorScratch = new float[2048];
                        _capture.DataAvailable += (_, e) =>
                        {
                            if (e.BytesRecorded > 0 && _buffer != null && _equalizer != null)
                            {
                                _buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                                int bytesPerSample = _buffer.WaveFormat.BitsPerSample / 8;
                                if (bytesPerSample > 0)
                                {
                                    int availableSamples = _buffer.BufferedBytes / bytesPerSample;
                                    while (availableSamples > 0)
                                    {
                                        int toRead = Math.Min(monitorScratch.Length, availableSamples);
                                        int read = _equalizer.Read(monitorScratch, 0, toRead);
                                        if (read <= 0) break;
                                        availableSamples -= read;
                                    }
                                }
                            }
                        };
                        _capture.StartRecording();

                        string singleLabel = exclusiveMode ? "WASAPI Shared (Single-Dev)" : "WASAPI Shared";
                        ActiveTargetDeviceId = targetDevice.ID;
                        ActiveStreamBadge = $"{khz:0.#} kHz / {mixFmt.BitsPerSample}-bit • {singleLabel}";
                        IsRunning = true;
                        return (true, "");
                    }

                    // Çift cihazlı kurulum (Harici DAC / Kulaklık + Dahili Hoparlör veya VB-Cable):
                    // WebView2 sesi sessize alınan captureDevice'a gider, C# motoru sesi alıp hedef cihaza Exclusive/Shared basar.
                    ActiveTargetDeviceId = targetDevice.ID;
                    ActiveCaptureDeviceId = captureDevice.ID;

                    _capture = new WasapiLoopbackCapture(captureDevice);
                    _buffer = new BufferedWaveProvider(_capture.WaveFormat)
                    {
                        BufferDuration = TimeSpan.FromMilliseconds(300),
                        DiscardOnBufferOverflow = true
                    };

                    var bridgeShareMode = exclusiveMode
                        ? AudioClientShareMode.Exclusive
                        : AudioClientShareMode.Shared;

                    _output = new WasapiOut(targetDevice, bridgeShareMode, useEventSync: true, latency: 50);

                    ISampleProvider bridgeChain;
                    if (_capture.WaveFormat.SampleRate != mixFmt.SampleRate ||
                        _capture.WaveFormat.Channels != mixFmt.Channels)
                    {
                        var targetFloatFmt = WaveFormat.CreateIeeeFloatWaveFormat(mixFmt.SampleRate, mixFmt.Channels);
                        _resampler = new MediaFoundationResampler(_buffer, targetFloatFmt)
                        {
                            ResamplerQuality = 60
                        };
                        bridgeChain = _resampler.ToSampleProvider();
                    }
                    else
                    {
                        bridgeChain = _buffer.ToSampleProvider();
                    }

                    _equalizer = new EqualizerSampleProvider(bridgeChain);
                    _equalizer.UpdateDsp(_eqEnabled, _eqGains, _normEnabled);

                    int bridgeBits = mixFmt.BitsPerSample;
                    if (exclusiveMode)
                    {
                        var pcmProvider = BuildExclusivePcmProvider(targetDevice, _equalizer, out bridgeBits);
                        _output.Init(pcmProvider);
                    }
                    else
                    {
                        _output.Init(_equalizer);
                    }

                    _capture.DataAvailable += (_, e) =>
                    {
                        if (e.BytesRecorded > 0 && _buffer != null)
                        {
                            _buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                        }
                    };

                    _output.Play();
                    _capture.StartRecording();

                    string modeLabel = exclusiveMode ? "WASAPI Exclusive" : "WASAPI Shared";
                    ActiveStreamBadge = $"{khz:0.#} kHz / {bridgeBits}-bit • {modeLabel}";
                    IsRunning = true;

                    _mutedEndpointId = captureDevice.ID;
                    _previousEndpointMuteState = captureDevice.AudioEndpointVolume.Mute;
                    captureDevice.AudioEndpointVolume.Mute = true;

                    return (true, "");
                }
                catch (Exception ex)
                {
                    Stop();
                    return (false, $"WASAPI başlatılamadı: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                IsRunning = false;
                ActiveCaptureDeviceId = "";
                ActiveTargetDeviceId = "";
                ActiveStreamBadge = "WebView2 • Standard";

                if (!string.IsNullOrWhiteSpace(_mutedEndpointId))
                {
                    try
                    {
                        using var enumerator = new MMDeviceEnumerator();
                        using var dev = enumerator.GetDevice(_mutedEndpointId);
                        dev.AudioEndpointVolume.Mute = _previousEndpointMuteState;
                    }
                    catch { }
                    _mutedEndpointId = "";
                }

                try { _capture?.StopRecording(); } catch { }
                try { _capture?.Dispose(); } catch { }
                _capture = null;

                try { _output?.Stop(); } catch { }
                try { _output?.Dispose(); } catch { }
                _output = null;

                try { _resampler?.Dispose(); } catch { }
                _resampler = null;

                _equalizer = null;
                _buffer = null;
            }
        }

        void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState)
            => AudioDevicesChanged?.Invoke();

        void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId)
            => AudioDevicesChanged?.Invoke();

        void IMMNotificationClient.OnDeviceRemoved(string deviceId)
            => AudioDevicesChanged?.Invoke();

        void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
                AudioDevicesChanged?.Invoke();
        }

        void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }

        public void Dispose()
        {
            Stop();
            try
            {
                if (_deviceEnumerator != null)
                {
                    _deviceEnumerator.UnregisterEndpointNotificationCallback(this);
                    _deviceEnumerator.Dispose();
                    _deviceEnumerator = null;
                }
            }
            catch { }
        }
    }
}