using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CraftConsole.CrossPlatform
{
    public partial class FirstLaunchWindow : Window
    {
        private CheckBox _cbAppMenu;
        private CheckBox _cbUnifiedLauncher;
        private CheckBox _cbDesktopShortcut;
        private TextBox _tbBinaryPath;
        private Button _btnBrowseBinary;
        private TextBlock _txtBinaryStatus;
        private Button _btnSkip;
        private Button _btnFinish;
        private Action _onFinished;

        public FirstLaunchWindow() : this(() => { })
        {
        }

        public FirstLaunchWindow(Action onFinished)
        {
            _onFinished = onFinished;
            InitializeComponent();

            _cbAppMenu = this.FindControl<CheckBox>("CbAppMenu")!;
            _cbUnifiedLauncher = this.FindControl<CheckBox>("CbUnifiedLauncher")!;
            _cbDesktopShortcut = this.FindControl<CheckBox>("CbDesktopShortcut")!;
            _tbBinaryPath = this.FindControl<TextBox>("TbBinaryPath")!;
            _btnBrowseBinary = this.FindControl<Button>("BtnBrowseBinary")!;
            _txtBinaryStatus = this.FindControl<TextBlock>("TxtBinaryStatus")!;
            _btnSkip = this.FindControl<Button>("BtnSkip")!;
            _btnFinish = this.FindControl<Button>("BtnFinish")!;

            // Auto-detect EffectCraft binary
            var existingConfig = SplashConfig.Load();
            string? found = LinuxDesktopIntegration.FindEffectCraftBinary(existingConfig.BinaryPath);
            if (!string.IsNullOrEmpty(found))
            {
                _tbBinaryPath.Text = found;
                UpdateBinaryStatus(found);
            }
            else
            {
                _txtBinaryStatus.Text = "⚠️ Not detected (portable path)";
                _txtBinaryStatus.Foreground = Avalonia.Media.Brushes.Orange;
            }

            _tbBinaryPath.TextChanged += (s, e) => UpdateBinaryStatus(_tbBinaryPath.Text);
            _btnBrowseBinary.Click += async (s, e) => await BrowseBinaryAsync();

            _btnSkip.Click += (s, e) => FinishSetup(false);
            _btnFinish.Click += (s, e) => FinishSetup(true);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnHeaderPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private void UpdateBinaryStatus(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                _txtBinaryStatus.Text = "✓ Ready";
                _txtBinaryStatus.Foreground = Avalonia.Media.Brushes.MediumSpringGreen;
            }
            else if (!string.IsNullOrWhiteSpace(path))
            {
                _txtBinaryStatus.Text = "⚠️ File does not exist";
                _txtBinaryStatus.Foreground = Avalonia.Media.Brushes.Coral;
            }
            else
            {
                _txtBinaryStatus.Text = "⚠️ None specified";
                _txtBinaryStatus.Foreground = Avalonia.Media.Brushes.Orange;
            }
        }

        private async System.Threading.Tasks.Task BrowseBinaryAsync()
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
                        _tbBinaryPath.Text = localPath;
                        UpdateBinaryStatus(localPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("File picker error: " + ex.Message);
            }
        }

        private void FinishSetup(bool applyShortcuts)
        {
            try
            {
                var config = SplashConfig.Load();
                if (!string.IsNullOrWhiteSpace(_tbBinaryPath.Text))
                {
                    config.BinaryPath = _tbBinaryPath.Text.Trim();
                    config.Save();
                }
            }
            catch { }

            if (applyShortcuts)
            {
                bool desktop = _cbDesktopShortcut.IsChecked == true;
                bool unified = _cbUnifiedLauncher.IsChecked == true;
                LinuxDesktopIntegration.CreateDesktopEntries(desktop, unified);
            }

            MarkFirstLaunchDone();
            Close();
            _onFinished?.Invoke();
        }

        public static bool IsFirstLaunch()
        {
            try
            {
                string markerPath = Path.Combine(SplashConfig.GetConfigDir(), ".firstlaunch_done");
                return !File.Exists(markerPath);
            }
            catch
            {
                return false;
            }
        }

        public static void MarkFirstLaunchDone()
        {
            try
            {
                string markerPath = Path.Combine(SplashConfig.GetConfigDir(), ".firstlaunch_done");
                File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            }
            catch { }
        }
    }
}
