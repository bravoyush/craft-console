using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace CraftConsole.CrossPlatform
{
    public partial class SettingsWindow : Window
    {
        private SplashConfig _config;

        private ComboBox _cbSplashStyle;
        private TextBox _tbTitle;
        private TextBox _tbSubtitle;
        private TextBox _tbAccentColor;
        private Border _accentPreviewDot;

        private TextBox _tbSettingsBinaryPath;
        private Button _btnSettingsBrowseBinary;
        private TextBlock _txtSettingsBinaryStatus;

        private CheckBox _cbInstantLaunch;
        private CheckBox _cbSoundChime;

        private Button _btnRecreateShortcuts;
        private TextBlock _shortcutsStatus;

        private CheckBox _cbPurgeConfig;
        private Button _btnUninstall;
        private TextBlock _uninstallStatus;

        private Button _btnSave;
        private Button _btnCloseTop;

        public SettingsWindow()
        {
            _config = SplashConfig.Load();
            InitializeComponent();

            _cbSplashStyle = this.FindControl<ComboBox>("CbSplashStyle")!;
            _tbTitle = this.FindControl<TextBox>("TbTitle")!;
            _tbSubtitle = this.FindControl<TextBox>("TbSubtitle")!;
            _tbAccentColor = this.FindControl<TextBox>("TbAccentColor")!;
            _accentPreviewDot = this.FindControl<Border>("AccentPreviewDot")!;

            _tbSettingsBinaryPath = this.FindControl<TextBox>("TbSettingsBinaryPath")!;
            _btnSettingsBrowseBinary = this.FindControl<Button>("BtnSettingsBrowseBinary")!;
            _txtSettingsBinaryStatus = this.FindControl<TextBlock>("TxtSettingsBinaryStatus")!;

            _cbInstantLaunch = this.FindControl<CheckBox>("CbInstantLaunch")!;
            _cbSoundChime = this.FindControl<CheckBox>("CbSoundChime")!;

            _btnRecreateShortcuts = this.FindControl<Button>("BtnRecreateShortcuts")!;
            _shortcutsStatus = this.FindControl<TextBlock>("ShortcutsStatus")!;

            _cbPurgeConfig = this.FindControl<CheckBox>("CbPurgeConfig")!;
            _btnUninstall = this.FindControl<Button>("BtnUninstall")!;
            _uninstallStatus = this.FindControl<TextBlock>("UninstallStatus")!;

            _btnSave = this.FindControl<Button>("BtnSave")!;
            _btnCloseTop = this.FindControl<Button>("BtnCloseTop")!;

            // Populate form values
            _tbTitle.Text = _config.Title ?? "EffectCraft";
            _tbSubtitle.Text = _config.Subtitle ?? "Motion Graphics & Compositing";
            _tbAccentColor.Text = _config.AccentColor ?? "#00D2FF";
            UpdateAccentPreview(_tbAccentColor.Text);

            // Splash Style dropdown selection
            bool isPoster = string.Equals(_config.SplashStyle, "Poster", StringComparison.OrdinalIgnoreCase);
            _cbSplashStyle.SelectedIndex = isPoster ? 1 : 0;

            _tbSettingsBinaryPath.Text = _config.BinaryPath ?? "";
            UpdateBinaryStatus(_tbSettingsBinaryPath.Text);

            _cbInstantLaunch.IsChecked = _config.InstantLaunch;
            _cbSoundChime.IsChecked = _config.SoundChime;

            // Events
            _tbAccentColor.TextChanged += (s, e) => UpdateAccentPreview(_tbAccentColor.Text);
            _tbSettingsBinaryPath.TextChanged += (s, e) => UpdateBinaryStatus(_tbSettingsBinaryPath.Text);
            _btnSettingsBrowseBinary.Click += async (s, e) => await BrowseBinaryAsync();

            _btnRecreateShortcuts.Click += (s, e) => RecreateShortcuts();
            _btnUninstall.Click += (s, e) => ExecuteUninstall();
            _btnSave.Click += (s, e) => SaveAndClose();
            _btnCloseTop.Click += (s, e) => Close();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void UpdateAccentPreview(string? hex)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                {
                    var color = Color.Parse(hex.Trim());
                    _accentPreviewDot.Background = new SolidColorBrush(color);
                }
            }
            catch { }
        }

        private void UpdateBinaryStatus(string? path)
        {
            string? resolved = LinuxDesktopIntegration.FindEffectCraftBinary(path);
            if (!string.IsNullOrEmpty(resolved) && System.IO.File.Exists(resolved))
            {
                _txtSettingsBinaryStatus.Text = "✓ Ready";
                _txtSettingsBinaryStatus.Foreground = Avalonia.Media.Brushes.MediumSpringGreen;
            }
            else
            {
                _txtSettingsBinaryStatus.Text = "⚠️ Not found";
                _txtSettingsBinaryStatus.Foreground = Avalonia.Media.Brushes.Coral;
            }
        }

        private async Task BrowseBinaryAsync()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.StorageProvider != null)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                    {
                        Title = "Select EffectCraft Executable",
                        AllowMultiple = false
                    });

                    if (files != null && files.Count > 0)
                    {
                        string localPath = files[0].Path.LocalPath;
                        _tbSettingsBinaryPath.Text = localPath;
                        UpdateBinaryStatus(localPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("File picker error: " + ex.Message);
            }
        }

        private void RecreateShortcuts()
        {
            bool ok = LinuxDesktopIntegration.CreateDesktopEntries(true, true);
            _shortcutsStatus.Text = ok ? "✓ Shortcuts updated successfully!" : "⚠️ Failed to update shortcuts";
        }

        private async void ExecuteUninstall()
        {
            bool purge = _cbPurgeConfig.IsChecked == true;
            _btnUninstall.IsEnabled = false;
            _uninstallStatus.Text = "Removing launchers and icons...";

            var (removedCount, configPurged) = LinuxDesktopIntegration.Uninstall(purge);

            string msg = $"✓ Removed {removedCount} files.";
            if (configPurged) msg += " Settings purged.";
            _uninstallStatus.Text = msg;

            await Task.Delay(1500);
            Environment.Exit(0);
        }

        private void SaveAndClose()
        {
            // Splash Style
            if (_cbSplashStyle.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _config.SplashStyle = tag;
            }
            else
            {
                _config.SplashStyle = _cbSplashStyle.SelectedIndex == 1 ? "Poster" : "Classic";
            }

            _config.Title = _tbTitle.Text?.Trim() ?? "EffectCraft";
            _config.Subtitle = _tbSubtitle.Text?.Trim() ?? "Motion Graphics & Compositing";
            _config.AccentColor = _tbAccentColor.Text?.Trim() ?? "#00D2FF";
            _config.BinaryPath = _tbSettingsBinaryPath.Text?.Trim() ?? "";
            _config.InstantLaunch = _cbInstantLaunch.IsChecked == true;
            _config.SoundChime = _cbSoundChime.IsChecked == true;
            _config.Save();
            Close();
        }
    }
}
