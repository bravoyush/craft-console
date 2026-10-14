using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace CraftConsole.CrossPlatform
{
    public partial class SplashWindow : Window
    {
        private Grid _classicLayout;
        private Grid _posterLayout;

        private ProgressBar _progressBarClassic;
        private TextBlock _statusLabelClassic;
        private TextBlock _titleLabel;
        private TextBlock _subtitleLabel;
        private Border _badgeBorder;
        private Avalonia.Controls.Shapes.Path _badgeMark;

        private ProgressBar _progressBarPoster;
        private TextBlock _statusLabelPoster;
        private TextBlock _posterTitle;
        private TextBlock _posterSubtitle;

        private Action _onComplete;
        private Process? _engineProcess;
        private SplashConfig _config;

        public Process? EngineProcess => _engineProcess;

        public SplashWindow() : this(new SplashConfig(), () => { })
        {
        }

        public SplashWindow(SplashConfig config, Action onComplete)
        {
            _config = config;
            _onComplete = onComplete;
            InitializeComponent();

            _classicLayout = this.FindControl<Grid>("ClassicLayout")!;
            _posterLayout = this.FindControl<Grid>("PosterLayout")!;

            _progressBarClassic = this.FindControl<ProgressBar>("ProgressBarClassic")!;
            _statusLabelClassic = this.FindControl<TextBlock>("StatusLabelClassic")!;
            _titleLabel = this.FindControl<TextBlock>("TitleLabel")!;
            _subtitleLabel = this.FindControl<TextBlock>("SubtitleLabel")!;
            _badgeBorder = this.FindControl<Border>("BadgeBorder")!;
            _badgeMark = this.FindControl<Avalonia.Controls.Shapes.Path>("BadgeMark")!;

            _progressBarPoster = this.FindControl<ProgressBar>("ProgressBarPoster")!;
            _statusLabelPoster = this.FindControl<TextBlock>("StatusLabelPoster")!;
            _posterTitle = this.FindControl<TextBlock>("PosterTitle")!;
            _posterSubtitle = this.FindControl<TextBlock>("PosterSubtitle")!;

            ApplyConfigTheme();
            Loaded += OnLoaded;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ApplyConfigTheme()
        {
            bool isPoster = string.Equals(_config.SplashStyle, "Poster", StringComparison.OrdinalIgnoreCase);

            if (isPoster)
            {
                this.Width = 720;
                this.Height = 420;
                _classicLayout.IsVisible = false;
                _posterLayout.IsVisible = true;

                if (!string.IsNullOrEmpty(_config.Title)) _posterTitle.Text = _config.Title;
                if (!string.IsNullOrEmpty(_config.Subtitle)) _posterSubtitle.Text = _config.Subtitle;
            }
            else
            {
                // 1:1 Windows Classic Split Layout (Default)
                this.Width = 730;
                this.Height = 450;
                _classicLayout.IsVisible = true;
                _posterLayout.IsVisible = false;

                if (!string.IsNullOrEmpty(_config.Title)) _titleLabel.Text = _config.Title;
                if (!string.IsNullOrEmpty(_config.Subtitle)) _subtitleLabel.Text = _config.Subtitle;

                // Accent color & Badge background
                try
                {
                    var accent = Color.Parse(_config.AccentColor);
                    var accentBrush = new SolidColorBrush(accent);
                    _badgeBorder.BorderBrush = accentBrush;
                    _badgeMark.Fill = accentBrush;
                    _progressBarClassic.Foreground = accentBrush;

                    if (!string.IsNullOrEmpty(_config.BadgeBackground))
                    {
                        _badgeBorder.Background = new SolidColorBrush(Color.Parse(_config.BadgeBackground));
                    }
                }
                catch { }
            }
        }

        private async void OnLoaded(object? sender, EventArgs e)
        {
            await RunStartupFlow();
        }

        private async Task RunStartupFlow()
        {
            UpdateProgress(15, "Initializing Engine Core...");
            await Task.Delay(200);

            bool alreadyListening = EngineClient.IsEngineListening(300);
            if (alreadyListening)
            {
                UpdateProgress(60, "Connected to running EffectCraft instance...");
                await Task.Delay(300);
                UpdateProgress(100, "Ready!");
                await Task.Delay(400);
                CloseAndContinue();
                return;
            }

            // Engine not running, search for binary
            UpdateProgress(30, "Scanning Built-in Effects (306/306)...");
            string? binary = LinuxDesktopIntegration.FindEffectCraftBinary(_config.BinaryPath);

            if (!string.IsNullOrEmpty(binary))
            {
                UpdateProgress(45, $"Launching {System.IO.Path.GetFileName(binary)}...");
                try
                {
                    var psi = new ProcessStartInfo(binary)
                    {
                        Arguments = "--control " + EngineClient.ControlPort,
                        UseShellExecute = false,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(binary) ?? AppContext.BaseDirectory
                    };
                    psi.Environment["EFFECTCRAFT_CONTROL_PORT"] = EngineClient.ControlPort.ToString();
                    _engineProcess = Process.Start(psi);
                }
                catch (Exception ex)
                {
                    UpdateProgress(50, "Could not start engine: " + ex.Message);
                }

                UpdateProgress(60, "Binding EC Console Bridge...");

                // Poll for port 9877
                int checks = 0;
                while (checks < 60)
                {
                    await Task.Delay(100);
                    checks++;
                    double progress = 60 + (checks * 0.6);
                    if (progress > 95) progress = 95;

                    UpdateProgress(progress, "Starting Interface Session...");

                    if (EngineClient.IsEngineListening(100))
                    {
                        break;
                    }

                    if (_engineProcess != null && _engineProcess.HasExited)
                    {
                        UpdateProgress(80, "EffectCraft exited.");
                        break;
                    }
                }

                UpdateProgress(100, "Ready!");
                try
                {
                    LinuxDesktopIntegration.ConfigureInAppShortcut(_config.Shortcut);
                    EngineClient.SendTcpCommand("{\"id\":99,\"method\":\"ui.focus\",\"params\":{}}");
                }
                catch { }

                await Task.Delay(400);
                CloseAndContinue();
            }
            else
            {
                UpdateProgress(70, "EffectCraft not found locally. Running in standalone mode.");
                await Task.Delay(500);
                UpdateProgress(100, "Ready.");
                await Task.Delay(350);
                CloseAndContinue();
            }
        }

        private void UpdateProgress(double val, string status)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_progressBarClassic != null) _progressBarClassic.Value = val;
                if (_statusLabelClassic != null) _statusLabelClassic.Text = status;

                if (_progressBarPoster != null) _progressBarPoster.Value = val;
                if (_statusLabelPoster != null) _statusLabelPoster.Text = status;
            });
        }

        private void CloseAndContinue()
        {
            Dispatcher.UIThread.Post(() =>
            {
                Close();
                _onComplete?.Invoke();
            });
        }
    }
}
