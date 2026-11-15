using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace Nocturne
{
    public class AudioDeviceItem
    {
        public string DeviceId { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly Func<Task<List<AudioDeviceItem>>> _fetchWebViewDevicesAsync;
        private string _selectedLangCode;
        private string _selectedEngineMode;

        private uint _playMod, _playVk;
        private uint _nextMod, _nextVk;
        private uint _prevMod, _prevVk;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public SettingsWindow(AppSettings settings, Func<Task<List<AudioDeviceItem>>> fetchWebViewDevicesAsync)
        {
            InitializeComponent();
            IconHelper.ApplyWindowIcons(this);
            _settings = settings;
            _fetchWebViewDevicesAsync = fetchWebViewDevicesAsync;
            _selectedLangCode = string.IsNullOrWhiteSpace(_settings.Language) ? "auto" : _settings.Language;
            _selectedEngineMode = string.IsNullOrWhiteSpace(_settings.AudioEngineMode) ? "webview" : _settings.AudioEngineMode;

            MinimizeToTrayCheck.IsChecked = _settings.MinimizeToTrayOnClose;
            TrackOsdCheck.IsChecked = _settings.EnableTrackOsd;
            DiscordRpcCheck.IsChecked = _settings.EnableDiscordRpc;

            _playMod = _settings.PlayHotkeyModifiers;
            _playVk = _settings.PlayHotkeyVk;
            _nextMod = _settings.NextHotkeyModifiers;
            _nextVk = _settings.NextHotkeyVk;
            _prevMod = _settings.PrevHotkeyModifiers;
            _prevVk = _settings.PrevHotkeyVk;

            RefreshHotkeyTexts();

            SourceInitialized += (_, _) =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                int dark = 1;
                DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            };

            PopulateEngines();
            PopulateLanguages();
            ApplyLocalizedTexts();
            Loaded += async (_, _) => await LoadDevicesAsync();
        }

        private void RefreshHotkeyTexts()
        {
            PlayHotkeyBox.Text = FormatHotkey(_playMod, _playVk);
            NextHotkeyBox.Text = FormatHotkey(_nextMod, _nextVk);
            PrevHotkeyBox.Text = FormatHotkey(_prevMod, _prevVk);
        }

        public static string FormatHotkey(uint modifiers, uint vk)
        {
            if (vk == 0) return "None";
            var parts = new List<string>();
            if ((modifiers & 2) != 0) parts.Add("Ctrl");
            if ((modifiers & 1) != 0) parts.Add("Alt");
            if ((modifiers & 4) != 0) parts.Add("Shift");
            if ((modifiers & 8) != 0) parts.Add("Win");

            Key wpfKey = KeyInterop.KeyFromVirtualKey((int)vk);
            parts.Add(wpfKey.ToString());
            return string.Join(" + ", parts);
        }

        private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
                tb.Text = Localization.Get("HotkeyPressPrompt", _selectedLangCode);
        }

        private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
        {
            RefreshHotkeyTexts();
        }

        private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
                Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
                return;

            uint mod = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mod |= 2;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mod |= 1;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mod |= 4;

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

            if (sender == PlayHotkeyBox) { _playMod = mod; _playVk = vk; }
            else if (sender == NextHotkeyBox) { _nextMod = mod; _nextVk = vk; }
            else if (sender == PrevHotkeyBox) { _prevMod = mod; _prevVk = vk; }

            RefreshHotkeyTexts();
            Keyboard.ClearFocus();
        }

        private void PopulateEngines()
        {
            int idx = _selectedEngineMode switch
            {
                "wasapi_shared" => 1,
                "wasapi_exclusive" => 2,
                _ => 0
            };

            EngineComboBox.Items.Clear();
            EngineComboBox.Items.Add(new ComboBoxItem { Tag = "webview", Content = Localization.Get("EngineWebView", _selectedLangCode) });
            EngineComboBox.Items.Add(new ComboBoxItem { Tag = "wasapi_shared", Content = Localization.Get("EngineWasapiShared", _selectedLangCode) });
            EngineComboBox.Items.Add(new ComboBoxItem { Tag = "wasapi_exclusive", Content = Localization.Get("EngineWasapiExclusive", _selectedLangCode) });
            EngineComboBox.SelectedIndex = idx;
        }

        private void PopulateLanguages()
        {
            LanguageComboBox.Items.Clear();
            LanguageComboBox.Items.Add(new ComboBoxItem { Tag = "auto", Content = Localization.Get("LangAuto", _selectedLangCode) });
            LanguageComboBox.Items.Add(new ComboBoxItem { Tag = "tr", Content = "Türkçe" });
            LanguageComboBox.Items.Add(new ComboBoxItem { Tag = "en", Content = "English" });

            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if ((string)item.Tag == _selectedLangCode)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void ApplyLocalizedTexts()
        {
            Title = Localization.Get("SettingsTitle", _selectedLangCode);
            AudioEngineLabel.Text = Localization.Get("AudioEngineLabel", _selectedLangCode);
            AudioOutputLabel.Text = Localization.Get("AudioOutputLabel", _selectedLangCode);
            RefreshBtn.Content = Localization.Get("RefreshDevices", _selectedLangCode);
            LanguageLabel.Text = Localization.Get("LanguageLabel", _selectedLangCode);
            CloseBehaviorLabel.Text = Localization.Get("CloseBehaviorLabel", _selectedLangCode);
            MinimizeToTrayCheck.Content = Localization.Get("MinimizeToTrayCheck", _selectedLangCode);
            TrackOsdCheck.Content = Localization.Get("TrackOsdCheck", _selectedLangCode);
            DiscordRpcCheck.Content = Localization.Get("DiscordRpcCheck", _selectedLangCode);
            HotkeysLabel.Text = Localization.Get("HotkeysLabel", _selectedLangCode);
            HotkeyPlayLabel.Text = Localization.Get("HotkeyPlayLabel", _selectedLangCode);
            HotkeyNextLabel.Text = Localization.Get("HotkeyNextLabel", _selectedLangCode);
            HotkeyPrevLabel.Text = Localization.Get("HotkeyPrevLabel", _selectedLangCode);
            WasapiNoteText.Text = Localization.Get("WasapiNote", _selectedLangCode);
            WasapiNoteText.Visibility = _selectedEngineMode.StartsWith("wasapi") ? Visibility.Visible : Visibility.Collapsed;
            SaveBtn.Content = Localization.Get("SaveButton", _selectedLangCode);
            CancelBtn.Content = Localization.Get("CancelButton", _selectedLangCode);
        }

        private async Task LoadDevicesAsync()
        {
            RefreshBtn.IsEnabled = false;
            var items = new List<AudioDeviceItem>
            {
                new() { DeviceId = "", Label = Localization.Get("DefaultDevice", _selectedLangCode) }
            };

            string targetSavedId;
            if (_selectedEngineMode.StartsWith("wasapi"))
            {
                var wasapiDevices = WasapiAudioBridge.GetOutputDevices();
                items.AddRange(wasapiDevices.Select(d => new AudioDeviceItem { DeviceId = d.Id, Label = d.Name }));
                targetSavedId = _settings.WasapiDeviceId;
            }
            else
            {
                var wvDevices = await _fetchWebViewDevicesAsync();
                items.AddRange(wvDevices);
                targetSavedId = _settings.AudioOutputDeviceId;
            }

            DeviceComboBox.ItemsSource = items;
            var match = items.FirstOrDefault(d => d.DeviceId == targetSavedId) ?? items[0];
            DeviceComboBox.SelectedItem = match;
            RefreshBtn.IsEnabled = true;
        }

        private async void EngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EngineComboBox.SelectedItem is ComboBoxItem item && item.Tag is string mode && mode != _selectedEngineMode)
            {
                _selectedEngineMode = mode;
                ApplyLocalizedTexts();
                await LoadDevicesAsync();
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string code && code != _selectedLangCode)
            {
                _selectedLangCode = code;
                PopulateEngines();
                PopulateLanguages();
                ApplyLocalizedTexts();
                if (DeviceComboBox.ItemsSource is List<AudioDeviceItem> list && list.Count > 0)
                {
                    list[0].Label = Localization.Get("DefaultDevice", _selectedLangCode);
                    DeviceComboBox.Items.Refresh();
                }
            }
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadDevicesAsync();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            _settings.AudioEngineMode = _selectedEngineMode;
            _settings.MinimizeToTrayOnClose = MinimizeToTrayCheck.IsChecked == true;
            _settings.EnableTrackOsd = TrackOsdCheck.IsChecked == true;
            _settings.EnableDiscordRpc = DiscordRpcCheck.IsChecked == true;

            _settings.PlayHotkeyModifiers = _playMod;
            _settings.PlayHotkeyVk = _playVk;
            _settings.NextHotkeyModifiers = _nextMod;
            _settings.NextHotkeyVk = _nextVk;
            _settings.PrevHotkeyModifiers = _prevMod;
            _settings.PrevHotkeyVk = _prevVk;

            if (DeviceComboBox.SelectedItem is AudioDeviceItem selectedDevice)
            {
                if (_selectedEngineMode.StartsWith("wasapi"))
                    _settings.WasapiDeviceId = selectedDevice.DeviceId;
                else
                    _settings.AudioOutputDeviceId = selectedDevice.DeviceId;
            }

            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedLang && selectedLang.Tag is string code)
            {
                _settings.Language = code;
            }

            _settings.Save();
            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}