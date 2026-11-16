using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Nocturne
{
    public class PlaybackStateInfo
    {
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Artwork { get; set; } = "";
        public bool IsPlaying { get; set; }
        public double CurrentTime { get; set; }
        public double Duration { get; set; }
    }

    public partial class MiniPlayerWindow : Window
    {
        private readonly Action<string> _sendCommand;
        private readonly Action _restoreMainWindow;
        private readonly Action<bool>? _onClickThroughChanged;
        private string _lastArtworkUrl = "";
        private bool _isDraggingSeek = false;
        private double _currentDuration = 0;
        private bool _isClickThrough = false;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        public MiniPlayerWindow(Action<string> sendCommand, Action restoreMainWindow, Action<bool>? onClickThroughChanged = null)
        {
            InitializeComponent();
            LoadWindowIcon();
            _sendCommand = sendCommand;
            _restoreMainWindow = restoreMainWindow;
            _onClickThroughChanged = onClickThroughChanged;

            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 16;
            Top = workArea.Bottom - Height - 16;
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

        public void SetLanguage(string lang)
        {
            ClickThroughBtn.ToolTip = Localization.Get("ClickThroughTooltip", lang);
        }

        public void SetClickThrough(bool enabled)
        {
            _isClickThrough = enabled;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                if (enabled)
                    SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT);
                else
                    SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
            }

            RootBorder.Opacity = enabled ? 0.58 : 1.0;
            ClickThroughBtn.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            ExpandBtn.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            CloseBtn.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            _onClickThroughChanged?.Invoke(enabled);
        }

        private void ClickThroughBtn_Click(object sender, RoutedEventArgs e)
        {
            SetClickThrough(true);
        }

        public void UpdatePlaybackState(PlaybackStateInfo state, string lang)
        {
            TitleText.Text = string.IsNullOrWhiteSpace(state.Title)
                ? Localization.Get("NothingPlaying", lang)
                : state.Title;

            ArtistText.Text = string.IsNullOrWhiteSpace(state.Artist)
                ? "Deezer"
                : state.Artist;

            PlayPauseBtn.Content = state.IsPlaying ? "⏸" : "▶";
            _currentDuration = state.Duration;

            if (!_isDraggingSeek && state.Duration > 0)
            {
                SeekSlider.Value = Math.Clamp((state.CurrentTime / state.Duration) * 100.0, 0, 100);
            }
            else if (state.Duration <= 0)
            {
                SeekSlider.Value = 0;
            }

            if (!string.IsNullOrWhiteSpace(state.Artwork) && state.Artwork != _lastArtworkUrl)
            {
                _lastArtworkUrl = state.Artwork;
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(state.Artwork, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    CoverImage.Source = bmp;
                }
                catch { }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isClickThrough && e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void PrevBtn_Click(object sender, RoutedEventArgs e) => _sendCommand("prev");
        private void PlayPauseBtn_Click(object sender, RoutedEventArgs e) => _sendCommand("toggle");
        private void NextBtn_Click(object sender, RoutedEventArgs e) => _sendCommand("next");

        private void SeekSlider_MouseDown(object sender, MouseButtonEventArgs e) => _isDraggingSeek = true;

        private void SeekSlider_MouseUp(object sender, RoutedEventArgs e)
        {
            _isDraggingSeek = false;
            if (_currentDuration > 0)
            {
                double targetSeconds = (SeekSlider.Value / 100.0) * _currentDuration;
                _sendCommand($"seek:{targetSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }
        }

        private void ExpandBtn_Click(object sender, RoutedEventArgs e)
        {
            SetClickThrough(false);
            Hide();
            _restoreMainWindow();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            SetClickThrough(false);
            Hide();
        }
    }
}