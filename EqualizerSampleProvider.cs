using System;
using NAudio.Dsp;
using NAudio.Wave;

namespace Nocturne
{
    public sealed class EqualizerSampleProvider : ISampleProvider
    {
        public static readonly float[] BandFrequencies =
        {
            31f, 62f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f
        };

        private const int FftLength = 1024;
        private const int FftLog2 = 10;
        public const int SpectrumBarCount = 16;

        private readonly ISampleProvider _source;
        private readonly int _channels;
        private readonly BiQuadFilter[,] _filters;
        private readonly float[] _gains = new float[10];
        private readonly object _filterLock = new();
        private bool _eqEnabled;
        private bool _normalizationEnabled;

        // Dinamik Ses Normalizasyonu (AGC) durumu
        private float _agcEnvelope = 0.15f;

        // Gerçek zamanlı FFT spektrum tamponu
        private readonly Complex[] _fftBuffer = new Complex[FftLength];
        private readonly float[] _spectrumBars = new float[SpectrumBarCount];
        private int _fftPos = 0;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public EqualizerSampleProvider(ISampleProvider source)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            _filters = new BiQuadFilter[_channels, BandFrequencies.Length];
            UpdateDsp(false, new float[10], false);
        }

        public void UpdateDsp(bool eqEnabled, float[] gains, bool normalizationEnabled)
        {
            lock (_filterLock)
            {
                _eqEnabled = eqEnabled;
                _normalizationEnabled = normalizationEnabled;
                for (int b = 0; b < BandFrequencies.Length; b++)
                {
                    float g = (gains != null && b < gains.Length) ? Math.Clamp(gains[b], -12f, 12f) : 0f;
                    _gains[b] = g;
                    for (int ch = 0; ch < _channels; ch++)
                    {
                        _filters[ch, b] = BiQuadFilter.PeakingEQ(
                            _source.WaveFormat.SampleRate,
                            BandFrequencies[b],
                            1.41f,
                            g);
                    }
                }
            }
        }

        public float[] GetSpectrumSnapshot()
        {
            lock (_filterLock)
            {
                var copy = new float[SpectrumBarCount];
                Array.Copy(_spectrumBars, copy, SpectrumBarCount);
                return copy;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);
            if (samplesRead <= 0)
                return samplesRead;

            lock (_filterLock)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    int ch = i % _channels;
                    float sample = buffer[offset + i];

                    if (_eqEnabled)
                    {
                        for (int b = 0; b < BandFrequencies.Length; b++)
                        {
                            if (Math.Abs(_gains[b]) > 0.05f)
                                sample = _filters[ch, b].Transform(sample);
                        }
                    }

                    if (_normalizationEnabled)
                    {
                        float abs = Math.Abs(sample);
                        float coeff = abs > _agcEnvelope ? 0.002f : 0.00015f;
                        _agcEnvelope += coeff * (abs - _agcEnvelope);

                        if (_agcEnvelope > 0.008f)
                        {
                            float targetGain = Math.Clamp(0.22f / _agcEnvelope, 0.65f, 2.2f);
                            sample *= targetGain;
                        }
                    }

                    // Yumuşak doygunluk sınırlayıcı (Soft Limiter)
                    if (sample > 0.98f) sample = 0.98f + 0.02f * MathF.Tanh((sample - 0.98f) * 10f);
                    else if (sample < -0.98f) sample = -0.98f + 0.02f * MathF.Tanh((sample + 0.98f) * 10f);

                    buffer[offset + i] = sample;

                    // Sol kanal örneklerini FFT penceresine besle
                    if (ch == 0)
                    {
                        double window = 0.54 - 0.46 * Math.Cos(2.0 * Math.PI * _fftPos / (FftLength - 1));
                        _fftBuffer[_fftPos].X = (float)(sample * window);
                        _fftBuffer[_fftPos].Y = 0f;
                        _fftPos++;

                        if (_fftPos >= FftLength)
                        {
                            _fftPos = 0;
                            ComputeFftBars();
                        }
                    }
                }
            }

            return samplesRead;
        }

        private void ComputeFftBars()
        {
            FastFourierTransform.FFT(true, FftLog2, _fftBuffer);

            int usableBins = FftLength / 2;
            for (int bar = 0; bar < SpectrumBarCount; bar++)
            {
                // İnsan kulağına uygun logaritmik frekans aralığı (30 Hz - 16 kHz)
                double startRatio = Math.Pow((double)bar / SpectrumBarCount, 1.85);
                double endRatio = Math.Pow((double)(bar + 1) / SpectrumBarCount, 1.85);

                int binStart = Math.Clamp((int)(startRatio * usableBins), 1, usableBins - 1);
                int binEnd = Math.Clamp((int)(endRatio * usableBins), binStart + 1, usableBins);

                float sumSq = 0f;
                int count = 0;
                for (int bin = binStart; bin < binEnd; bin++)
                {
                    float re = _fftBuffer[bin].X;
                    float im = _fftBuffer[bin].Y;
                    float mag = MathF.Sqrt(re * re + im * im);
                    sumSq += mag * mag;
                    count++;
                }

                float rmsMag = count > 0 ? MathF.Sqrt(sumSq / count) : 0f;

                // Yüksek frekanslara doğru doğal enerji düşüşünü dengeleyen tilt kazancı
                float tiltBoost = 1.0f + (bar * 0.45f);
                float scaled = rmsMag * tiltBoost;

                // Desibel (dB) tabanlı dinamik aralık (-52 dB ile -8 dB arası)
                float db = 20f * MathF.Log10(MathF.Max(scaled, 1e-6f));
                float normalized = Math.Clamp((db + 52f) / 44f, 0f, 1f);

                // Hızlı yükselme, akıcı düşüş (peak decay)
                _spectrumBars[bar] = normalized > _spectrumBars[bar]
                    ? (_spectrumBars[bar] * 0.35f + normalized * 0.65f)
                    : (_spectrumBars[bar] * 0.78f + normalized * 0.22f);
            }
        }
    }
}