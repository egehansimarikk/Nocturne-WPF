using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Forms = System.Windows.Forms;

namespace Nocturne
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings = AppSettings.Load();
        private Forms.NotifyIcon? _trayIcon;
        private Forms.ToolStripMenuItem? _trayShowItem;
        private Forms.ToolStripMenuItem? _trayMiniItem;
        private Forms.ToolStripMenuItem? _trayUnlockMiniItem;
        private Forms.ToolStripMenuItem? _trayExitItem;
        private MiniPlayerWindow? _miniPlayer;
        private TrackOsdWindow? _osdWindow;
        private bool _isExplicitExit = false;
        private HwndSource? _hwndSource;
        private MediaPlayer? _smtcPlayer;
        private SystemMediaTransportControls? _smtc;
        private InMemoryRandomAccessStream? _silentWavStream;
        private string _lastSmtcTitle = "";
        private string _lastSmtcArtwork = "";
        private string _lastOsdTrackKey = "";
        private string _latestReleaseUrl = "https://github.com/egehansimarikk/Nocturne/releases/latest";
        private bool _isLastPlaying = false;
        private readonly WasapiAudioBridge _wasapiBridge = new();
        private readonly DiscordRpcService _discordRpc = new();
        private CancellationTokenSource? _deviceChangeCts;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_NOREPEAT = 0x4000;

        private const uint VK_MEDIA_NEXT_TRACK = 0xB0;
        private const uint VK_MEDIA_PREV_TRACK = 0xB1;
        private const uint VK_MEDIA_PLAY_PAUSE = 0xB3;

        private const int HOTKEY_MEDIA_PLAY = 9001;
        private const int HOTKEY_MEDIA_NEXT = 9002;
        private const int HOTKEY_MEDIA_PREV = 9003;
        private const int HOTKEY_COMBO_PLAY = 9004;
        private const int HOTKEY_COMBO_NEXT = 9005;
        private const int HOTKEY_COMBO_PREV = 9006;

        public MainWindow()
        {
            InitializeComponent();
            LoadWindowIcon();
            UpdateLocalizedTooltips();
            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            _wasapiBridge.AudioDevicesChanged += OnAudioDevicesChanged;
            _wasapiBridge.SetEqualizer(_settings.EqualizerEnabled, _settings.EqualizerBands);
            _discordRpc.Configure(_settings.EnableDiscordRpc);
            SetupTrayIcon();
        }

        private void LoadWindowIcon()
        {
            try
            {
                string icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "nocturne.ico");
                if (File.Exists(icoPath))
                    Icon = BitmapFrame.Create(new Uri(icoPath, UriKind.Absolute));
            }
            catch { }
        }

        private void UpdateAudioFormatBadge()
        {
            string eqSuffix = (_wasapiBridge.IsRunning && _settings.EqualizerEnabled) ? " • EQ" : "";
            AudioFormatText.Text = _wasapiBridge.ActiveStreamBadge + eqSuffix;
        }

        private void OnAudioDevicesChanged()
        {
            Dispatcher.BeginInvoke(async () =>
            {
                _deviceChangeCts?.Cancel();
                _deviceChangeCts?.Dispose();
                _deviceChangeCts = new CancellationTokenSource();
                var token = _deviceChangeCts.Token;

                try
                {
                    await Task.Delay(1000, token);
                    if (token.IsCancellationRequested)
                        return;

                    await HandleHardwareDeviceChangeAsync();
                }
                catch (TaskCanceledException) { }
            });
        }

        private async Task HandleHardwareDeviceChangeAsync()
        {
            var activeDevices = WasapiAudioBridge.GetOutputDevices();

            if (_wasapiBridge.IsRunning && !string.IsNullOrWhiteSpace(_wasapiBridge.ActiveTargetDeviceId))
            {
                bool targetStillConnected = activeDevices.Any(d => d.Id == _wasapiBridge.ActiveTargetDeviceId);
                if (!targetStillConnected)
                {
                    bool wasPlaying = _isLastPlaying;
                    _wasapiBridge.Stop();
                    UpdateAudioFormatBadge();

                    if (DeezerWebView?.CoreWebView2 != null)
                    {
                        await DeezerWebView.CoreWebView2.ExecuteScriptAsync("window.__applyNocturneSink && window.__applyNocturneSink('');");
                        if (wasPlaying)
                        {
                            await Task.Delay(250);
                            await DeezerWebView.CoreWebView2.ExecuteScriptAsync("window.__nocturneCommand && window.__nocturneCommand('play');");
                        }
                    }
                    return;
                }
            }

            if (_settings.AudioEngineMode.StartsWith("wasapi") && !string.IsNullOrWhiteSpace(_settings.WasapiDeviceId))
            {
                bool targetConnected = activeDevices.Any(d => d.Id == _settings.WasapiDeviceId);
                if (targetConnected && (!_wasapiBridge.IsRunning || _wasapiBridge.ActiveTargetDeviceId != _settings.WasapiDeviceId))
                {
                    await ApplyAudioRoutingAsync(silentOnError: true);
                }
            }
        }

        private void UpdateLocalizedTooltips()
        {
            SettingsBtn.ToolTip = Localization.Get("SettingsTooltip", _settings.Language);
            MiniPlayerBtn.ToolTip = Localization.Get("MiniPlayerTooltip", _settings.Language);
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int useDarkMode = 1;
            DwmSetWindowAttribute(hwnd, 20, ref useDarkMode, sizeof(int));

            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(WndProc);

            RegisterGlobalHotKeys();
            _ = InitializeWindowsSmtcAsync();
        }

        private void RegisterGlobalHotKeys()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            for (int id = 9001; id <= 9006; id++)
                UnregisterHotKey(hwnd, id);

            RegisterHotKey(hwnd, HOTKEY_MEDIA_PLAY, MOD_NOREPEAT, VK_MEDIA_PLAY_PAUSE);
            RegisterHotKey(hwnd, HOTKEY_MEDIA_NEXT, MOD_NOREPEAT, VK_MEDIA_NEXT_TRACK);
            RegisterHotKey(hwnd, HOTKEY_MEDIA_PREV, MOD_NOREPEAT, VK_MEDIA_PREV_TRACK);

            if (_settings.PlayHotkeyVk != 0)
                RegisterHotKey(hwnd, HOTKEY_COMBO_PLAY, _settings.PlayHotkeyModifiers | MOD_NOREPEAT, _settings.PlayHotkeyVk);

            if (_settings.NextHotkeyVk != 0)
                RegisterHotKey(hwnd, HOTKEY_COMBO_NEXT, _settings.NextHotkeyModifiers | MOD_NOREPEAT, _settings.NextHotkeyVk);

            if (_settings.PrevHotkeyVk != 0)
                RegisterHotKey(hwnd, HOTKEY_COMBO_PREV, _settings.PrevHotkeyModifiers | MOD_NOREPEAT, _settings.PrevHotkeyVk);
        }

        private async Task InitializeWindowsSmtcAsync()
        {
            try
            {
                _smtcPlayer = new MediaPlayer
                {
                    Volume = 0,
                    IsMuted = true
                };
                _smtcPlayer.CommandManager.IsEnabled = false;

                byte[] silentWav = CreateSilentWavBytes();
                _silentWavStream = new InMemoryRandomAccessStream();
                await _silentWavStream.WriteAsync(silentWav.AsBuffer());
                _silentWavStream.Seek(0);

                _smtcPlayer.Source = MediaSource.CreateFromStream(_silentWavStream, "audio/wav");
                _smtcPlayer.Play();
                _smtcPlayer.Pause();

                _smtc = _smtcPlayer.SystemMediaTransportControls;
                _smtc.IsEnabled = true;
                _smtc.IsPlayEnabled = true;
                _smtc.IsPauseEnabled = true;
                _smtc.IsNextEnabled = true;
                _smtc.IsPreviousEnabled = true;
                _smtc.PlaybackStatus = MediaPlaybackStatus.Paused;

                _smtc.ButtonPressed += (_, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        switch (args.Button)
                        {
                            case SystemMediaTransportControlsButton.Play:
                            case SystemMediaTransportControlsButton.Pause:
                                SendPlayerCommand("toggle");
                                break;
                            case SystemMediaTransportControlsButton.Next:
                                SendPlayerCommand("next");
                                break;
                            case SystemMediaTransportControlsButton.Previous:
                                SendPlayerCommand("prev");
                                break;
                        }
                    });
                };
            }
            catch { }
        }

        private static byte[] CreateSilentWavBytes()
        {
            const int sampleRate = 8000;
            const int dataSize = 800;
            byte[] wav = new byte[44 + dataSize];
            using var ms = new MemoryStream(wav);
            using var bw = new BinaryWriter(ms);

            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + dataSize);
            bw.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(sampleRate);
            bw.Write(sampleRate);
            bw.Write((short)1);
            bw.Write((short)8);
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(dataSize);
            for (int i = 0; i < dataSize; i++)
                bw.Write((byte)128);

            return wav;
        }

        private void UpdateWindowsSmtc(PlaybackStateInfo state)
        {
            if (_smtc == null)
                return;

            try
            {
                _smtc.IsEnabled = true;
                _smtc.PlaybackStatus = state.IsPlaying
                    ? MediaPlaybackStatus.Playing
                    : MediaPlaybackStatus.Paused;

                if (!string.IsNullOrWhiteSpace(state.Title) &&
                    (state.Title != _lastSmtcTitle || state.Artwork != _lastSmtcArtwork))
                {
                    _lastSmtcTitle = state.Title;
                    _lastSmtcArtwork = state.Artwork;

                    var updater = _smtc.DisplayUpdater;
                    updater.Type = MediaPlaybackType.Music;
                    updater.MusicProperties.Title = state.Title;
                    updater.MusicProperties.Artist = state.Artist ?? "";

                    if (!string.IsNullOrWhiteSpace(state.Artwork) &&
                        Uri.TryCreate(state.Artwork, UriKind.Absolute, out var artUri))
                    {
                        updater.Thumbnail = RandomAccessStreamReference.CreateFromUri(artUri);
                    }

                    updater.Update();
                }
            }
            catch { }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                switch (id)
                {
                    case HOTKEY_MEDIA_PLAY:
                    case HOTKEY_COMBO_PLAY:
                        SendPlayerCommand("toggle");
                        handled = true;
                        break;

                    case HOTKEY_MEDIA_NEXT:
                    case HOTKEY_COMBO_NEXT:
                        SendPlayerCommand("next");
                        handled = true;
                        break;

                    case HOTKEY_MEDIA_PREV:
                    case HOTKEY_COMBO_PREV:
                        SendPlayerCommand("prev");
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        private void SetupTrayIcon()
        {
            var menu = new Forms.ContextMenuStrip();
            _trayShowItem = new Forms.ToolStripMenuItem(Localization.Get("TrayShow", _settings.Language));
            _trayMiniItem = new Forms.ToolStripMenuItem(Localization.Get("TrayMiniPlayer", _settings.Language));
            _trayUnlockMiniItem = new Forms.ToolStripMenuItem(Localization.Get("TrayUnlockMini", _settings.Language))
            {
                Visible = false
            };
            _trayExitItem = new Forms.ToolStripMenuItem(Localization.Get("TrayExit", _settings.Language));

            _trayShowItem.Click += (_, _) => RestoreFromTray();
            _trayMiniItem.Click += (_, _) => OpenMiniPlayer(hideMain: false);
            _trayUnlockMiniItem.Click += (_, _) => _miniPlayer?.SetClickThrough(false);
            _trayExitItem.Click += (_, _) => ExitApplication();

            menu.Items.Add(_trayShowItem);
            menu.Items.Add(_trayMiniItem);
            menu.Items.Add(_trayUnlockMiniItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(_trayExitItem);

            Icon trayAppIcon = SystemIcons.Application;
            try
            {
                string icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "nocturne.ico");
                if (File.Exists(icoPath))
                    trayAppIcon = new Icon(icoPath);
            }
            catch { }

            _trayIcon = new Forms.NotifyIcon
            {
                Icon = trayAppIcon,
                Text = "Nocturne",
                ContextMenuStrip = menu,
                Visible = true
            };

            _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        }

        private void UpdateTrayTexts()
        {
            if (_trayShowItem != null)
                _trayShowItem.Text = Localization.Get("TrayShow", _settings.Language);
            if (_trayMiniItem != null)
                _trayMiniItem.Text = Localization.Get("TrayMiniPlayer", _settings.Language);
            if (_trayUnlockMiniItem != null)
                _trayUnlockMiniItem.Text = Localization.Get("TrayUnlockMini", _settings.Language);
            if (_trayExitItem != null)
                _trayExitItem.Text = Localization.Get("TrayExit", _settings.Language);
        }

        private void RestoreFromTray()
        {
            Show();
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Activate();
        }

        private void OpenMiniPlayer(bool hideMain)
        {
            if (_miniPlayer == null)
            {
                _miniPlayer = new MiniPlayerWindow(
                    SendPlayerCommand,
                    RestoreFromTray,
                    isLocked =>
                    {
                        if (_trayUnlockMiniItem != null)
                            _trayUnlockMiniItem.Visible = isLocked;
                    });
            }
            _miniPlayer.SetLanguage(_settings.Language);
            _miniPlayer.Show();
            _miniPlayer.Activate();

            if (hideMain)
                Hide();
        }

        private async void SendPlayerCommand(string cmd)
        {
            if (DeezerWebView?.CoreWebView2 == null)
                return;

            string escapedCmd = JsonSerializer.Serialize(cmd);
            await DeezerWebView.CoreWebView2.ExecuteScriptAsync($"window.__nocturneCommand && window.__nocturneCommand({escapedCmd});");
        }

        private void ExitApplication()
        {
            _isExplicitExit = true;
            _deviceChangeCts?.Cancel();
            _wasapiBridge.AudioDevicesChanged -= OnAudioDevicesChanged;

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                for (int id = 9001; id <= 9006; id++)
                    UnregisterHotKey(hwnd, id);
            }
            _hwndSource?.RemoveHook(WndProc);

            _discordRpc.Dispose();
            _wasapiBridge.Dispose();
            _smtcPlayer?.Dispose();
            _silentWavStream?.Dispose();
            _osdWindow?.Close();
            _miniPlayer?.Close();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            System.Windows.Application.Current.Shutdown();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!_isExplicitExit)
            {
                if (_settings.MinimizeToTrayOnClose)
                {
                    e.Cancel = true;
                    Hide();
                }
                else
                {
                    ExitApplication();
                }
            }
        }

        private async Task ApplyAudioRoutingAsync(bool silentOnError = false)
        {
            _wasapiBridge.SetEqualizer(_settings.EqualizerEnabled, _settings.EqualizerBands);

            if (_settings.AudioEngineMode.StartsWith("wasapi"))
            {
                var allDevices = WasapiAudioBridge.GetOutputDevices();

                if (!string.IsNullOrWhiteSpace(_settings.WasapiDeviceId) &&
                    !allDevices.Any(d => d.Id == _settings.WasapiDeviceId))
                {
                    _wasapiBridge.Stop();
                    UpdateAudioFormatBadge();
                    if (DeezerWebView?.CoreWebView2 != null)
                        await DeezerWebView.CoreWebView2.ExecuteScriptAsync("window.__applyNocturneSink && window.__applyNocturneSink('');");
                    return;
                }

                bool wasPlaying = _isLastPlaying;

                _wasapiBridge.Stop();
                if (wasPlaying && DeezerWebView?.CoreWebView2 != null)
                {
                    await DeezerWebView.CoreWebView2.ExecuteScriptAsync("window.__nocturneCommand && window.__nocturneCommand('pause');");
                    await Task.Delay(120);
                }

                var targetDev = string.IsNullOrWhiteSpace(_settings.WasapiDeviceId)
                    ? allDevices.FirstOrDefault()
                    : (allDevices.Find(d => d.Id == _settings.WasapiDeviceId) ?? allDevices.FirstOrDefault());
                var captureDev = targetDev != null
                    ? allDevices.Find(d => d.Id != targetDev.Id)
                    : null;

                if (DeezerWebView?.CoreWebView2 != null && captureDev != null)
                {
                    string bridgeSink = await FindWebViewSinkIdByDeviceNameAsync(captureDev.Name);
                    string escapedId = JsonSerializer.Serialize(bridgeSink);
                    await DeezerWebView.CoreWebView2.ExecuteScriptAsync($"window.__applyNocturneSink && window.__applyNocturneSink({escapedId});");
                    await Task.Delay(250);
                }

                bool exclusive = _settings.AudioEngineMode == "wasapi_exclusive";
                var (success, errorMsg) = _wasapiBridge.Start(_settings.WasapiDeviceId, exclusive);
                UpdateAudioFormatBadge();

                if (!success)
                {
                    if (!silentOnError)
                    {
                        MessageBox.Show(this, errorMsg, "Nocturne Audio", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _settings.AudioEngineMode = "webview";
                        _settings.Save();
                    }
                    return;
                }

                if (wasPlaying && DeezerWebView?.CoreWebView2 != null)
                {
                    await Task.Delay(150);
                    await DeezerWebView.CoreWebView2.ExecuteScriptAsync("window.__nocturneCommand && window.__nocturneCommand('play');");
                }
            }
            else
            {
                _wasapiBridge.Stop();
                UpdateAudioFormatBadge();
                if (DeezerWebView?.CoreWebView2 != null)
                {
                    string escapedId = JsonSerializer.Serialize(_settings.AudioOutputDeviceId ?? "");
                    await DeezerWebView.CoreWebView2.ExecuteScriptAsync($"window.__applyNocturneSink && window.__applyNocturneSink({escapedId});");
                }
            }
        }

        private async Task<string> FindWebViewSinkIdByDeviceNameAsync(string mmDeviceName)
        {
            try
            {
                var wvDevices = await GetAudioOutputDevicesAsync();
                if (wvDevices.Count == 0)
                    return "";

                var directMatch = wvDevices.Find(w =>
                    w.Label.Contains(mmDeviceName, StringComparison.OrdinalIgnoreCase) ||
                    mmDeviceName.Contains(w.Label, StringComparison.OrdinalIgnoreCase));
                if (directMatch != null)
                    return directMatch.DeviceId;

                var hwMatch = Regex.Match(mmDeviceName, @"\(([^)]+)\)");
                if (hwMatch.Success)
                {
                    string hwToken = hwMatch.Groups[1].Value.Trim();
                    var tokenMatch = wvDevices.Find(w => w.Label.Contains(hwToken, StringComparison.OrdinalIgnoreCase));
                    if (tokenMatch != null)
                        return tokenMatch.DeviceId;
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_settings.CheckUpdatesOnStartup)
                _ = CheckForAppUpdatesAsync();

            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Nocturne",
                "WebView2Data"
            );

            string targetLang = _settings.Language == "auto"
                ? CultureInfo.CurrentUICulture.Name
                : (_settings.Language == "tr" ? "tr-TR" : "en-US");

            var options = new CoreWebView2EnvironmentOptions
            {
                Language = targetLang,
                AdditionalBrowserArguments = "--disable-features=HardwareMediaKeyHandling,MediaSessionService"
            };

            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
            await DeezerWebView.EnsureCoreWebView2Async(environment);

            DeezerWebView.CoreWebView2.PermissionRequested += (_, args) =>
            {
                if (args.PermissionKind == CoreWebView2PermissionKind.Microphone ||
                    args.PermissionKind == CoreWebView2PermissionKind.Camera)
                {
                    args.State = CoreWebView2PermissionState.Allow;
                }
            };

            DeezerWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            DeezerWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            DeezerWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            await DeezerWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildCoreBridgeScript(""));

            DeezerWebView.Source = new Uri("https://www.deezer.com/");

            if (_settings.AudioEngineMode.StartsWith("wasapi") || !string.IsNullOrWhiteSpace(_settings.AudioOutputDeviceId))
            {
                await Task.Delay(800);
                await ApplyAudioRoutingAsync(silentOnError: true);
            }
        }

        private async Task CheckForAppUpdatesAsync()
        {
            var info = await UpdateChecker.CheckForUpdatesAsync();
            if (info.IsUpdateAvailable)
            {
                _latestReleaseUrl = info.ReleaseUrl;
                UpdateBtn.Content = $"⬆ {Localization.Get("UpdateAvailableBadge", _settings.Language)} ({info.LatestTag})";
                UpdateBtn.Visibility = Visibility.Visible;
            }
        }

        private void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _latestReleaseUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                string msg = args.TryGetWebMessageAsString();
                if (msg.StartsWith("NOCTURNE_STATE:"))
                {
                    string json = msg.Substring("NOCTURNE_STATE:".Length);
                    var state = JsonSerializer.Deserialize<PlaybackStateInfo>(json);
                    if (state != null)
                    {
                        _isLastPlaying = state.IsPlaying;
                        UpdateWindowsSmtc(state);
                        _discordRpc.UpdatePresence(state);

                        string currentTrackKey = $"{state.Title}|{state.Artist}";
                        if (!string.IsNullOrWhiteSpace(state.Title) && state.IsPlaying && currentTrackKey != _lastOsdTrackKey)
                        {
                            _lastOsdTrackKey = currentTrackKey;
                            if (_settings.EnableTrackOsd && (!IsVisible || !IsActive) && (_miniPlayer == null || !_miniPlayer.IsVisible))
                            {
                                _osdWindow ??= new TrackOsdWindow();
                                _osdWindow.ShowNotification(state);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(state.Title) && _trayIcon != null)
                        {
                            string trayTitle = $"{state.Title} - {state.Artist}";
                            _trayIcon.Text = trayTitle.Length > 63 ? trayTitle.Substring(0, 60) + "..." : trayTitle;
                        }

                        if (_miniPlayer != null && _miniPlayer.IsVisible)
                        {
                            _miniPlayer.UpdatePlaybackState(state, _settings.Language);
                        }
                    }
                }
            }
            catch { }
        }

        private static string BuildCoreBridgeScript(string deviceId)
        {
            string escapedId = JsonSerializer.Serialize(deviceId ?? "");
            return $@"
                (() => {{
                    window.__nocturneSinkId = {escapedId};
                    window.__nocturneMediaElements = new Set();
                    window.__nocturneAudioContexts = new Set();
                    window.__nocturnePosState = {{ duration: 0, position: 0, updatedAt: 0 }};

                    function bindMediaEvents(el) {{
                        if (!el || el.__nocturneBound) return;
                        el.__nocturneBound = true;
                        for (const evt of ['play', 'pause', 'loadedmetadata', 'durationchange', 'seeked']) {{
                            el.addEventListener(evt, () => {{
                                if (window.__pushNocturneState) window.__pushNocturneState();
                            }}, {{ passive: true }});
                        }}
                    }}

                    const OrigAudioContext = window.AudioContext || window.webkitAudioContext;
                    if (OrigAudioContext) {{
                        const PatchedAudioContext = function(...args) {{
                            const ctx = new OrigAudioContext(...args);
                            window.__nocturneAudioContexts.add(ctx);
                            if (window.__nocturneSinkId && typeof ctx.setSinkId === 'function') {{
                                ctx.setSinkId(window.__nocturneSinkId).catch(() => {{}});
                            }}
                            return ctx;
                        }};
                        PatchedAudioContext.prototype = OrigAudioContext.prototype;
                        window.AudioContext = PatchedAudioContext;
                        if (window.webkitAudioContext) window.webkitAudioContext = PatchedAudioContext;
                    }}

                    const origPlay = HTMLMediaElement.prototype.play;
                    HTMLMediaElement.prototype.play = function(...args) {{
                        window.__nocturneMediaElements.add(this);
                        bindMediaEvents(this);
                        const target = window.__nocturneSinkId || '';
                        if (typeof this.setSinkId === 'function' && this.sinkId !== target) {{
                            this.setSinkId(target).catch(() => {{}});
                        }}
                        return origPlay.apply(this, args);
                    }};

                    window.__applyNocturneSink = async (sinkId) => {{
                        window.__nocturneSinkId = sinkId || '';
                        const allMedia = new Set([
                            ...document.querySelectorAll('audio, video'),
                            ...window.__nocturneMediaElements
                        ]);
                        for (const el of allMedia) {{
                            if (typeof el.setSinkId === 'function') {{
                                try {{ await el.setSinkId(window.__nocturneSinkId); }} catch (_) {{}}
                            }}
                        }}
                        for (const ctx of window.__nocturneAudioContexts) {{
                            if (typeof ctx.setSinkId === 'function') {{
                                try {{ await ctx.setSinkId(window.__nocturneSinkId); }} catch (_) {{}}
                            }}
                        }}
                    }};

                    const origSetActionHandler = navigator.mediaSession?.setActionHandler?.bind(navigator.mediaSession);
                    window.__nocturneHandlers = {{}};
                    if (origSetActionHandler) {{
                        navigator.mediaSession.setActionHandler = function(action, handler) {{
                            window.__nocturneHandlers[action] = handler;
                            return origSetActionHandler(action, handler);
                        }};
                    }}

                    const origSetPositionState = navigator.mediaSession?.setPositionState?.bind(navigator.mediaSession);
                    if (origSetPositionState) {{
                        navigator.mediaSession.setPositionState = function(state) {{
                            if (state && typeof state.duration === 'number' && isFinite(state.duration) && state.duration > 0) {{
                                window.__nocturnePosState = {{
                                    duration: state.duration,
                                    position: typeof state.position === 'number' && isFinite(state.position) ? state.position : 0,
                                    updatedAt: Date.now()
                                }};
                            }}
                            const res = origSetPositionState(state);
                            if (window.__pushNocturneState) window.__pushNocturneState();
                            return res;
                        }};
                    }}

                    function parseClockSeconds(text) {{
                        if (!text) return 0;
                        const clean = text.trim().replace(/^-/, '');
                        const parts = clean.split(':').map(n => parseInt(n, 10));
                        if (parts.some(isNaN)) return 0;
                        if (parts.length === 2) return parts[0] * 60 + parts[1];
                        if (parts.length === 3) return parts[0] * 3600 + parts[1] * 60 + parts[2];
                        return 0;
                    }}

                    function readDeezerTimes(media) {{
                        let cur = media && isFinite(media.currentTime) ? media.currentTime : 0;
                        let dur = media && isFinite(media.duration) && media.duration > 0 ? media.duration : 0;

                        if (dur <= 0 && window.__nocturnePosState.duration > 0) {{
                            dur = window.__nocturnePosState.duration;
                            if (cur <= 0) {{
                                const elapsed = media && !media.paused ? (Date.now() - window.__nocturnePosState.updatedAt) / 1000 : 0;
                                cur = Math.min(dur, window.__nocturnePosState.position + elapsed);
                            }}
                        }}

                        if (dur <= 0) {{
                            const slider = document.querySelector('[role=""slider""][aria-valuemax]');
                            if (slider) {{
                                const maxVal = parseFloat(slider.getAttribute('aria-valuemax'));
                                const nowVal = parseFloat(slider.getAttribute('aria-valuenow'));
                                if (isFinite(maxVal) && maxVal > 1) {{
                                    dur = maxVal;
                                    if (cur <= 0 && isFinite(nowVal)) cur = nowVal;
                                }}
                            }}
                        }}

                        if (dur <= 0) {{
                            const remEl = document.querySelector('[data-testid=""remaining_time""]');
                            const elpEl = document.querySelector('[data-testid=""elapsed_time""]');
                            const remSec = parseClockSeconds(remEl?.textContent);
                            const elpSec = parseClockSeconds(elpEl?.textContent);
                            if (elpSec > 0 && cur <= 0) cur = elpSec;
                            if (remSec > 0) {{
                                dur = (remEl?.textContent?.trim().startsWith('-')) ? (cur + remSec) : remSec;
                            }}
                        }}

                        return {{ cur, dur }};
                    }}

                    function getActiveMedia() {{
                        const list = [...document.querySelectorAll('audio, video'), ...window.__nocturneMediaElements];
                        list.forEach(bindMediaEvents);
                        return list.find(m => !m.paused && m.readyState >= 2) || list.find(m => m.readyState >= 2) || list[0];
                    }}

                    window.__nocturneCommand = (cmd) => {{
                        const media = getActiveMedia();
                        if (cmd === 'toggle') {{
                            if (media) {{
                                if (media.paused) {{
                                    if (window.__nocturneHandlers['play']) window.__nocturneHandlers['play']();
                                    else media.play().catch(() => {{}});
                                }} else {{
                                    if (window.__nocturneHandlers['pause']) window.__nocturneHandlers['pause']();
                                    else media.pause();
                                }}
                            }} else {{
                                document.querySelector('[data-testid=""play_button_play""], [data-testid=""play_button_pause""]')?.click();
                            }}
                        }} else if (cmd === 'play') {{
                            if (media && media.paused) {{
                                if (window.__nocturneHandlers['play']) window.__nocturneHandlers['play']();
                                else media.play().catch(() => {{}});
                            }}
                        }} else if (cmd === 'pause') {{
                            if (media && !media.paused) {{
                                if (window.__nocturneHandlers['pause']) window.__nocturneHandlers['pause']();
                                else media.pause();
                            }}
                        }} else if (cmd === 'next') {{
                            if (window.__nocturneHandlers['nexttrack']) window.__nocturneHandlers['nexttrack']();
                            else document.querySelector('[data-testid=""next_track_button""]')?.click();
                        }} else if (cmd === 'prev') {{
                            if (window.__nocturneHandlers['previoustrack']) window.__nocturneHandlers['previoustrack']();
                            else document.querySelector('[data-testid=""previous_track_button""]')?.click();
                        }} else if (cmd.startsWith('seek:')) {{
                            const sec = parseFloat(cmd.slice(5));
                            if (!isNaN(sec)) {{
                                if (window.__nocturneHandlers['seekto']) window.__nocturneHandlers['seekto']({{ seekTime: sec }});
                                else if (media) media.currentTime = sec;
                            }}
                        }}
                    }};

                    window.__pushNocturneState = () => {{
                        try {{
                            const meta = navigator.mediaSession?.metadata;
                            const media = getActiveMedia();
                            let art = '';
                            if (meta && meta.artwork && meta.artwork.length > 0) {{
                                art = meta.artwork[meta.artwork.length - 1].src || meta.artwork[0].src || '';
                            }}
                            const times = readDeezerTimes(media);
                            const payload = {{
                                Title: meta?.title || '',
                                Artist: meta?.artist || '',
                                Artwork: art,
                                IsPlaying: media ? !media.paused : (navigator.mediaSession?.playbackState === 'playing'),
                                CurrentTime: times.cur,
                                Duration: times.dur
                            }};
                            window.chrome.webview.postMessage('NOCTURNE_STATE:' + JSON.stringify(payload));
                        }} catch (_) {{}}
                    }};

                    setInterval(window.__pushNocturneState, 500);
                }})();
            ";
        }

        private async Task<List<AudioDeviceItem>> GetAudioOutputDevicesAsync()
        {
            var result = new List<AudioDeviceItem>();
            if (DeezerWebView?.CoreWebView2 == null)
                return result;

            var tcs = new TaskCompletionSource<string>();

            void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
            {
                try
                {
                    string msg = args.TryGetWebMessageAsString();
                    if (msg.StartsWith("NOCTURNE_DEVICES:"))
                    {
                        tcs.TrySetResult(msg.Substring("NOCTURNE_DEVICES:".Length));
                    }
                }
                catch { }
            }

            DeezerWebView.CoreWebView2.WebMessageReceived += OnWebMessage;

            try
            {
                string script = @"
                    (async () => {
                        try {
                            try {
                                const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
                                stream.getTracks().forEach(t => t.stop());
                            } catch (_) {}

                            const devices = await navigator.mediaDevices.enumerateDevices();
                            const outputs = devices
                                .filter(d => d.kind === 'audiooutput' && d.deviceId && d.deviceId !== 'default' && d.deviceId !== 'communications')
                                .map(d => ({ DeviceId: d.deviceId, Label: d.label || ('Audio Output (' + d.deviceId.slice(0, 6) + ')') }));

                            window.chrome.webview.postMessage('NOCTURNE_DEVICES:' + JSON.stringify(outputs));
                        } catch (e) {
                            window.chrome.webview.postMessage('NOCTURNE_DEVICES:[]');
                        }
                    })();
                ";

                await DeezerWebView.CoreWebView2.ExecuteScriptAsync(script);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));
                if (completed == tcs.Task)
                {
                    string json = await tcs.Task;
                    var parsed = JsonSerializer.Deserialize<List<AudioDeviceItem>>(json);
                    if (parsed != null)
                        result.AddRange(parsed);
                }
            }
            catch { }
            finally
            {
                DeezerWebView.CoreWebView2.WebMessageReceived -= OnWebMessage;
            }

            return result;
        }

        private void MiniPlayerBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenMiniPlayer(hideMain: true);
        }

        private async void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            string prevMode = _settings.AudioEngineMode;
            string prevDevice = _settings.AudioOutputDeviceId;
            string prevWasapi = _settings.WasapiDeviceId;

            var dialog = new SettingsWindow(_settings, GetAudioOutputDevicesAsync)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                UpdateLocalizedTooltips();
                UpdateTrayTexts();
                RegisterGlobalHotKeys();
                _discordRpc.Configure(_settings.EnableDiscordRpc);
                _wasapiBridge.SetEqualizer(_settings.EqualizerEnabled, _settings.EqualizerBands);

                bool routingChanged = prevMode != _settings.AudioEngineMode ||
                                      prevDevice != _settings.AudioOutputDeviceId ||
                                      prevWasapi != _settings.WasapiDeviceId;

                if (routingChanged)
                    await ApplyAudioRoutingAsync();
                else
                    UpdateAudioFormatBadge();
            }
        }
    }
}