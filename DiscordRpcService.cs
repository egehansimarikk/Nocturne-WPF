using System;
using DiscordRPC;

namespace Nocturne
{
    public sealed class DiscordRpcService : IDisposable
    {
        private const string DefaultClientId = "1571632416483967036";
        private DiscordRpcClient? _client;
        private string _lastTitle = "";
        private string _lastArtist = "";
        private bool _lastIsPlaying = false;
        private double _lastDuration = 0;
        private double _lastRemainingSec = -1;

        public void Configure(bool enabled)
        {
            if (!enabled)
            {
                Stop();
                return;
            }

            if (_client != null && !_client.IsDisposed)
                return;

            Stop();

            try
            {
                _client = new DiscordRpcClient(DefaultClientId);
                _client.Initialize();
            }
            catch
            {
                Stop();
            }
        }

        public void UpdatePresence(PlaybackStateInfo state)
        {
            if (_client == null || _client.IsDisposed || !_client.IsInitialized)
                return;

            try
            {
                if (string.IsNullOrWhiteSpace(state.Title))
                {
                    if (!string.IsNullOrEmpty(_lastTitle))
                    {
                        _lastTitle = "";
                        _lastDuration = 0;
                        _client.ClearPresence();
                    }
                    return;
                }

                bool hasValidDuration = !double.IsNaN(state.Duration) && !double.IsInfinity(state.Duration) && state.Duration > 1;
                double remaining = hasValidDuration && state.Duration >= state.CurrentTime
                    ? state.Duration - state.CurrentTime
                    : 0;

                bool trackChanged = state.Title != _lastTitle || state.Artist != _lastArtist;
                bool playStateChanged = state.IsPlaying != _lastIsPlaying;
                bool durationJustLoaded = _lastDuration <= 1 && hasValidDuration;
                bool seekDetected = hasValidDuration && _lastRemainingSec >= 0 && Math.Abs(remaining - _lastRemainingSec) > 3.0;

                if (!trackChanged && !playStateChanged && !durationJustLoaded && !seekDetected)
                {
                    if (hasValidDuration)
                        _lastRemainingSec = remaining;
                    return;
                }

                _lastTitle = state.Title;
                _lastArtist = state.Artist ?? "";
                _lastIsPlaying = state.IsPlaying;
                _lastDuration = hasValidDuration ? state.Duration : 0;
                _lastRemainingSec = hasValidDuration ? remaining : -1;

                var presence = new RichPresence
                {
                    Type = ActivityType.Listening,
                    Details = Truncate(state.Title, 128),
                    State = Truncate(string.IsNullOrWhiteSpace(state.Artist) ? "Deezer" : state.Artist, 128),
                    Assets = new Assets
                    {
                        LargeImageKey = !string.IsNullOrWhiteSpace(state.Artwork) && state.Artwork.StartsWith("https://")
                            ? state.Artwork
                            : "nocturne_logo",
                        LargeImageText = "Nocturne HiFi"
                    }
                };

                if (state.IsPlaying && hasValidDuration)
                {
                    double safeCurrent = Math.Max(0, Math.Min(state.CurrentTime, state.Duration));
                    DateTime startUtc = DateTime.UtcNow.AddSeconds(-safeCurrent);
                    DateTime endUtc = startUtc.AddSeconds(state.Duration);
                    presence.Timestamps = new Timestamps(startUtc, endUtc);
                }

                _client.SetPresence(presence);
            }
            catch { }
        }

        private static string Truncate(string value, int maxLen)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLen ? value : value.Substring(0, maxLen - 3) + "...";
        }

        public void Stop()
        {
            try
            {
                if (_client != null && !_client.IsDisposed)
                {
                    _client.ClearPresence();
                    _client.Dispose();
                }
            }
            catch { }
            finally
            {
                _client = null;
                _lastTitle = "";
                _lastArtist = "";
                _lastDuration = 0;
                _lastRemainingSec = -1;
            }
        }

        public void Dispose() => Stop();
    }
}