using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace Nocturne
{
    public class WasapiDeviceInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public sealed class WasapiAudioBridge : IMMNotificationClient, IDisposable
    {
        private MMDeviceEnumerator? _deviceEnumerator;
        private WasapiLoopbackCapture? _capture;
        private WasapiOut? _output;
        private BufferedWaveProvider? _buffer;
        private MediaFoundationResampler? _resampler;
        private string _mutedEndpointId = "";
        private bool _previousEndpointMuteState = false;
        private readonly object _lock = new();

        public bool IsRunning { get; private set; }
        public string ActiveCaptureDeviceId { get; private set; } = "";
        public string ActiveTargetDeviceId { get; private set; } = "";

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

        public static List<WasapiDeviceInfo> GetOutputDevices()
        {
            var list = new List<WasapiDeviceInfo>();
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (var dev in endpoints)
                {
                    list.Add(new WasapiDeviceInfo
                    {
                        Id = dev.ID,
                        Name = dev.FriendlyName
                    });
                }
            }
            catch { }
            return list;
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

                    // Hedef DAC dışındaki dahili ses cihazını köprü (capture) olarak seç
                    MMDevice? captureDevice = allActive.FirstOrDefault(d => d.ID != targetDevice.ID);

                    if (captureDevice == null)
                    {
                        if (exclusiveMode)
                        {
                            return (false, "Sistemde yalnızca tek ses çıkış cihazı aktif. WASAPI Exclusive köprüsü için dahili hoparlörün de etkin olması gerekir.");
                        }

                        ActiveTargetDeviceId = targetDevice.ID;
                        IsRunning = true;
                        return (true, "");
                    }

                    ActiveTargetDeviceId = targetDevice.ID;
                    ActiveCaptureDeviceId = captureDevice.ID;

                    _capture = new WasapiLoopbackCapture(captureDevice);
                    _buffer = new BufferedWaveProvider(_capture.WaveFormat)
                    {
                        BufferDuration = TimeSpan.FromMilliseconds(300),
                        DiscardOnBufferOverflow = true
                    };

                    var shareMode = exclusiveMode
                        ? AudioClientShareMode.Exclusive
                        : AudioClientShareMode.Shared;

                    _output = new WasapiOut(targetDevice, shareMode, useEventSync: true, latency: 50);

                    var targetFormat = targetDevice.AudioClient.MixFormat;
                    if (_capture.WaveFormat.SampleRate != targetFormat.SampleRate ||
                        _capture.WaveFormat.Channels != targetFormat.Channels)
                    {
                        _resampler = new MediaFoundationResampler(_buffer, targetFormat)
                        {
                            ResamplerQuality = 60
                        };
                        _output.Init(_resampler);
                    }
                    else
                    {
                        _output.Init(_buffer);
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
                    IsRunning = true;

                    // Oturumu değil, yalnızca köprü cihazının fiziksel çıkışını sustur
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