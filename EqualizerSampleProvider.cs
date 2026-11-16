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

        private readonly ISampleProvider _source;
        private readonly int _channels;
        private readonly BiQuadFilter[,] _filters;
        private readonly float[] _gains = new float[10];
        private readonly object _filterLock = new();
        private bool _enabled;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public EqualizerSampleProvider(ISampleProvider source)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            _filters = new BiQuadFilter[_channels, BandFrequencies.Length];
            UpdateBands(false, new float[10]);
        }

        public void UpdateBands(bool enabled, float[] gains)
        {
            lock (_filterLock)
            {
                _enabled = enabled;
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

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);
            if (samplesRead <= 0)
                return samplesRead;

            lock (_filterLock)
            {
                if (!_enabled)
                    return samplesRead;

                for (int i = 0; i < samplesRead; i++)
                {
                    int ch = i % _channels;
                    float sample = buffer[offset + i];

                    for (int b = 0; b < BandFrequencies.Length; b++)
                    {
                        if (Math.Abs(_gains[b]) > 0.05f)
                            sample = _filters[ch, b].Transform(sample);
                    }

                    // Dijital kırpılmayı (clipping) önlemek için yumuşak sınırlama
                    if (sample > 0.99f) sample = 0.99f;
                    else if (sample < -0.99f) sample = -0.99f;

                    buffer[offset + i] = sample;
                }
            }

            return samplesRead;
        }
    }
}