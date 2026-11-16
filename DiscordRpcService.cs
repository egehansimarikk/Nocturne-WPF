using System;
using DiscordRPC;

namespace Nocturne
{
    public sealed class DiscordRpcService : IDisposable
    {
        private const string DefaultClientId = "1571632416483967036";
        private DiscordRpcClient? _client;
        private PlaybackStateInfo? _pendingState;
        private string _lastTitle = "";
        private string _lastArtist = "";
        private bool _lastIsPlaying = false;
        private double _lastDuration = 0;
        private DateTime _expectedEndUtc = DateTime.MinValue;
        private DateTime _lastSentUtc = DateTime.MinValue;

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
                _client.OnReady += (_, _) =>
                {
                    if (_pendingState != null)
                        UpdatePresence(_pendingState);
                };
                _client.Initialize();
            }
            catch
            {
                Stop();
            }
        }

        public void UpdatePresence(PlaybackStateInfo state)
        {
            _pendingState = state;

            if (_client == null || _client.IsDisposed || !_client.IsInitialized)
                return;

            try
            {
                if (string.IsNullOrWhiteSpace(state.Title))
                {
                    if (!string.IsNullOrEmpty(_lastTitle))
                    {
                        _lastTitle = "";
                        _lastArtist = "";
                        _lastDuration = 0;
                        _expectedEndUtc = DateTime.MinValue;
                        _client.ClearPresence();
                    }
                    return;
                }

                bool hasValidDuration = !double.IsNaN(state.Duration) && !double.IsInfinity(state.Duration) && state.Duration > 1.0;
                double safeCurrent = hasValidDuration
                    ? Math.Max(0, Math.Min(state.CurrentTime, state.Duration))
                    : Math.Max(0, state.CurrentTime);

                DateTime nowUtc = DateTime.UtcNow;
                DateTime calculatedEndUtc = hasValidDuration
                    ? nowUtc.AddSeconds(state.Duration - safeCurrent)
                    : DateTime.MinValue;

                bool trackChanged = state.Title != _lastTitle || (state.Artist ?? "") != _lastArtist;
                bool playStateChanged = state.IsPlaying != _lastIsPlaying;
                bool durationJustLoaded = _lastDuration <= 1.0 && hasValidDuration;
                bool seekDetected = state.IsPlaying && hasValidDuration && _expectedEndUtc != DateTime.MinValue &&
                                    Math.Abs((calculatedEndUtc - _expectedEndUtc).TotalSeconds) > 2.5;

                if (!trackChanged && !playStateChanged && !durationJustLoaded && !seekDetected)
                    return;

                // Şarkı değişimi veya süre ilk yüklendiğinde bekleme yapma
                if (!trackChanged && !durationJustLoaded && (nowUtc - _lastSentUtc).TotalMilliseconds < 800)
                    return;

                _lastTitle = state.Title;
                _lastArtist = state.Artist ?? "";
                _lastIsPlaying = state.IsPlaying;
                _lastDuration = hasValidDuration ? state.Duration : 0;
                _expectedEndUtc = calculatedEndUtc;
                _lastSentUtc = nowUtc;

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

                if (state.IsPlaying)
                {
                    DateTime startUtc = nowUtc.AddSeconds(-safeCurrent);
                    presence.Timestamps = hasValidDuration
                        ? new Timestamps(startUtc, calculatedEndUtc)
                        : new Timestamps(startUtc);
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
                _pendingState = null;
                _lastTitle = "";
                _lastArtist = "";
                _lastDuration = 0;
                _expectedEndUtc = DateTime.MinValue;
            }
        }

        public void Dispose() => Stop();
    }
}