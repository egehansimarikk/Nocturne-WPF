using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Nocturne
{
    public partial class TrackOsdWindow : Window
    {
        private readonly DispatcherTimer _hideTimer;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        public TrackOsdWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            };

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
            _hideTimer.Tick += (_, _) =>
            {
                _hideTimer.Stop();
                FadeOutAndHide();
            };
        }

        public void ShowNotification(PlaybackStateInfo state)
        {
            if (string.IsNullOrWhiteSpace(state.Title))
                return;

            TitleText.Text = state.Title;
            ArtistText.Text = string.IsNullOrWhiteSpace(state.Artist) ? "Deezer" : state.Artist;

            if (!string.IsNullOrWhiteSpace(state.Artwork))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(state.Artwork, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    CoverImg.Source = bmp;
                }
                catch { }
            }

            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 18;
            Top = workArea.Bottom - Height - 18;

            if (!IsVisible)
                Show();

            var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(220));
            CardBorder.BeginAnimation(OpacityProperty, fadeIn);

            _hideTimer.Stop();
            _hideTimer.Start();
        }

        private void FadeOutAndHide()
        {
            var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(320));
            fadeOut.Completed += (_, _) => Hide();
            CardBorder.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}