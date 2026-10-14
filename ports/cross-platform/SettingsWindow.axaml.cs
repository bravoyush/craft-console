using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvPath = Avalonia.Controls.Shapes.Path;
using SysPath = System.IO.Path;

namespace CraftConsole.CrossPlatform
{
    public partial class SettingsWindow : Window
    {
        private SplashConfig _config;

        // Tabs
        private Button _tabBtnVisual;
        private Button _tabBtnPlugins;
        private Grid _viewVisual;
        private Grid _viewPlugins;

        // Visual Controls
        private ComboBox _cbSplashStyle;
        private TextBlock _lblImagePath;
        private Button _btnBrowseMedia;
        private RadioButton _rbThemeDark;
        private RadioButton _rbThemeLight;
        private StackPanel _paletteSwatchesStack;
        private Button _btnResetAccent;
        private TextBox _txtAccentHex;

        private TextBox _txtTitle;
        private Slider _sliderTitleSize;
        private TextBlock _lblTitleSizeVal;

        private TextBox _txtSubtitle;
        private Slider _sliderSubtitleSize;
        private TextBlock _lblSubtitleSizeVal;

        private TextBox _txtArtist;
        private Slider _sliderArtistSize;
        private TextBlock _lblArtistSizeVal;

        private Slider _sliderCornerRadius;
        private TextBlock _lblCornerRadiusVal;

        private CheckBox _cbEnableSplash;
        private CheckBox _cbLoadingBar;
        private CheckBox _cbLoadingText;
        private CheckBox _cbFooter;

        private Slider _sliderDuration;
        private TextBlock _lblDurationVal;
        private Button _btnResetVisuals;

        // Preview Controls
        private Panel _previewContainer;
        private Button _btnTestSplash;

        // Plugins Controls
        private Button _btnGlobalShortcut;
        private Button _btnResetGlobalShortcut;
        private CheckBox _cbEffects;
        private CheckBox _cbPresets;
        private CheckBox _cbLayers;
        private CheckBox _cbCommands;
        private CheckBox _cbRecent;

        private CheckBox _cbShowFavOnOpen;
        private Button _btnFavShortcut;
        private Button _btnResetFavShortcut;

        private TextBlock _lblLinkedExe;
        private Button _btnLocateExe;
        private Button _btnCreateShortcuts;

        // Window Header Controls
        private Button _btnCloseTop;

        // Bottom Action Buttons
        private Button _btnClose;
        private Button _btnSave;

        private bool _isRecordingGlobal = false;
        private bool _isRecordingFav = false;

        public SettingsWindow()
        {
            _config = SplashConfig.Load(AppContext.BaseDirectory);
            InitializeComponent();

            FindAllControls();
            PopulateFormFromConfig();
            BuildPaletteSwatches();
            WireEvents();
            RefreshPreview();

            // Intercept pointer pressed on top 80px before child controls to guarantee Wayland drag serial
            this.AddHandler(InputElement.PointerPressedEvent, (s, e) =>
            {
                var pt = e.GetCurrentPoint(this);
                if (pt.Properties.IsLeftButtonPressed && pt.Position.Y < 80)
                {
                    BeginMoveDrag(e);
                }
            }, RoutingStrategies.Tunnel);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void FindAllControls()
        {
            _tabBtnVisual = this.FindControl<Button>("TabBtnVisual")!;
            _tabBtnPlugins = this.FindControl<Button>("TabBtnPlugins")!;
            _viewVisual = this.FindControl<Grid>("ViewVisual")!;
            _viewPlugins = this.FindControl<Grid>("ViewPlugins")!;

            _cbSplashStyle = this.FindControl<ComboBox>("CbSplashStyle")!;
            _lblImagePath = this.FindControl<TextBlock>("LblImagePath")!;
            _btnBrowseMedia = this.FindControl<Button>("BtnBrowseMedia")!;
            _rbThemeDark = this.FindControl<RadioButton>("RbThemeDark")!;
            _rbThemeLight = this.FindControl<RadioButton>("RbThemeLight")!;
            _paletteSwatchesStack = this.FindControl<StackPanel>("PaletteSwatchesStack")!;
            _btnResetAccent = this.FindControl<Button>("BtnResetAccent")!;
            _txtAccentHex = this.FindControl<TextBox>("TxtAccentHex")!;

            _txtTitle = this.FindControl<TextBox>("TxtTitle")!;
            _sliderTitleSize = this.FindControl<Slider>("SliderTitleSize")!;
            _lblTitleSizeVal = this.FindControl<TextBlock>("LblTitleSizeVal")!;

            _txtSubtitle = this.FindControl<TextBox>("TxtSubtitle")!;
            _sliderSubtitleSize = this.FindControl<Slider>("SliderSubtitleSize")!;
            _lblSubtitleSizeVal = this.FindControl<TextBlock>("LblSubtitleSizeVal")!;

            _txtArtist = this.FindControl<TextBox>("TxtArtist")!;
            _sliderArtistSize = this.FindControl<Slider>("SliderArtistSize")!;
            _lblArtistSizeVal = this.FindControl<TextBlock>("LblArtistSizeVal")!;

            _sliderCornerRadius = this.FindControl<Slider>("SliderCornerRadius")!;
            _lblCornerRadiusVal = this.FindControl<TextBlock>("LblCornerRadiusVal")!;

            _cbEnableSplash = this.FindControl<CheckBox>("CbEnableSplash")!;
            _cbLoadingBar = this.FindControl<CheckBox>("CbLoadingBar")!;
            _cbLoadingText = this.FindControl<CheckBox>("CbLoadingText")!;
            _cbFooter = this.FindControl<CheckBox>("CbFooter")!;

            _sliderDuration = this.FindControl<Slider>("SliderDuration")!;
            _lblDurationVal = this.FindControl<TextBlock>("LblDurationVal")!;
            _btnResetVisuals = this.FindControl<Button>("BtnResetVisuals")!;

            _previewContainer = this.FindControl<Panel>("PreviewContainer")!;
            _btnTestSplash = this.FindControl<Button>("BtnTestSplash")!;

            _btnGlobalShortcut = this.FindControl<Button>("BtnGlobalShortcut")!;
            _btnResetGlobalShortcut = this.FindControl<Button>("BtnResetGlobalShortcut")!;
            _cbEffects = this.FindControl<CheckBox>("CbEffects")!;
            _cbPresets = this.FindControl<CheckBox>("CbPresets")!;
            _cbLayers = this.FindControl<CheckBox>("CbLayers")!;
            _cbCommands = this.FindControl<CheckBox>("CbCommands")!;
            _cbRecent = this.FindControl<CheckBox>("CbRecent")!;

            _cbShowFavOnOpen = this.FindControl<CheckBox>("CbShowFavOnOpen")!;
            _btnFavShortcut = this.FindControl<Button>("BtnFavShortcut")!;
            _btnResetFavShortcut = this.FindControl<Button>("BtnResetFavShortcut")!;

            _lblLinkedExe = this.FindControl<TextBlock>("LblLinkedExe")!;
            _btnLocateExe = this.FindControl<Button>("BtnLocateExe")!;
            _btnCreateShortcuts = this.FindControl<Button>("BtnCreateShortcuts")!;

            _btnCloseTop = this.FindControl<Button>("BtnCloseTop")!;
            _btnClose = this.FindControl<Button>("BtnClose")!;
            _btnSave = this.FindControl<Button>("BtnSave")!;
        }

        private void PopulateFormFromConfig()
        {
            // Splash Style
            bool isPoster = string.Equals(_config.SplashStyle, "Poster", StringComparison.OrdinalIgnoreCase);
            _cbSplashStyle.SelectedIndex = isPoster ? 1 : 0;

            // Artwork
            _lblImagePath.Text = SysPath.GetFileName(_config.ArtImage);
            if (string.IsNullOrEmpty(_lblImagePath.Text)) _lblImagePath.Text = "splash.png";

            // Theme
            bool isDark = !string.Equals(_config.Theme, "Light", StringComparison.OrdinalIgnoreCase);
            _rbThemeDark.IsChecked = isDark;
            _rbThemeLight.IsChecked = !isDark;

            // Accent
            _txtAccentHex.Text = _config.AccentColor;

            // Typography
            _txtTitle.Text = _config.Title;
            _sliderTitleSize.Value = _config.TitleFontSize;
            _lblTitleSizeVal.Text = _config.TitleFontSize.ToString("0.0") + "px";

            _txtSubtitle.Text = _config.Subtitle;
            _sliderSubtitleSize.Value = _config.SubtitleFontSize;
            _lblSubtitleSizeVal.Text = _config.SubtitleFontSize.ToString("0.0") + "px";

            _txtArtist.Text = _config.ArtistCredit;
            _sliderArtistSize.Value = _config.ArtistFontSize;
            _lblArtistSizeVal.Text = _config.ArtistFontSize.ToString("0.0") + "px";

            // Roundness
            _sliderCornerRadius.Value = _config.CornerRadius;
            _lblCornerRadiusVal.Text = _config.CornerRadius.ToString("0.0") + "px";

            // Toggles
            _cbEnableSplash.IsChecked = _config.ShowSplashScreen;
            _cbLoadingBar.IsChecked = _config.ShowLoadingBar;
            _cbLoadingText.IsChecked = _config.ShowLoadingText;
            _cbFooter.IsChecked = _config.ShowFooter;

            // Duration
            _sliderDuration.Value = _config.SplashDuration;
            _lblDurationVal.Text = _config.SplashDuration.ToString("0.0") + "s";

            // Shortcuts & Catalog
            _btnGlobalShortcut.Content = string.IsNullOrEmpty(_config.Shortcut) ? "Ctrl + Space" : _config.Shortcut;
            _btnFavShortcut.Content = string.IsNullOrEmpty(_config.FavoriteShortcut) ? "Ctrl + D" : _config.FavoriteShortcut;

            _cbEffects.IsChecked = _config.ConsoleShowEffects;
            _cbPresets.IsChecked = _config.ConsoleShowPresets;
            _cbLayers.IsChecked = _config.ConsoleShowLayers;
            _cbCommands.IsChecked = _config.ConsoleShowCommands;
            _cbRecent.IsChecked = _config.ConsoleShowRecent;
            _cbShowFavOnOpen.IsChecked = _config.ShowFavoritesOnOpen;

            // Linked executable
            string? foundExe = LinuxDesktopIntegration.FindEffectCraftBinary(_config.BinaryPath);
            _lblLinkedExe.Text = "Linked Executable: " + (foundExe ?? "Not found (Select via 'Locate EffectCraft Binary')");
        }

        private void BuildPaletteSwatches()
        {
            _paletteSwatchesStack.Children.Clear();
            string[] swatches = new[] { "#00D2FF", "#4E7BFB", "#9D4EDD", "#FF4757", "#00E676", "#FFAA00" };
            foreach (var hex in swatches)
            {
                var border = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(12),
                    Background = SolidColorBrush.Parse(hex),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1.5),
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                string curr = hex;
                border.PointerPressed += (s, e) =>
                {
                    _config.AccentColor = curr;
                    _txtAccentHex.Text = curr;
                    RefreshPreview();
                };
                _paletteSwatchesStack.Children.Add(border);
            }
        }

        private void WireEvents()
        {
            // Segmented Tabs
            _tabBtnVisual.Click += (s, e) => SwitchTab(true);
            _tabBtnPlugins.Click += (s, e) => SwitchTab(false);

            // Splash Style Dropdown
            _cbSplashStyle.SelectionChanged += (s, e) =>
            {
                if (_cbSplashStyle.SelectedItem is ComboBoxItem item)
                {
                    _config.SplashStyle = (item.Tag as string) ?? "Classic";
                    RefreshPreview();
                }
            };

            // Artwork Browse
            _btnBrowseMedia.Click += async (s, e) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Splash Screen Artwork or Video",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Media Files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.mp4", "*.wmv", "*.webm" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                    }
                });
                if (files != null && files.Count > 0)
                {
                    string localPath = files[0].Path.LocalPath;
                    _config.ArtImage = localPath;
                    _lblImagePath.Text = SysPath.GetFileName(localPath);
                    RefreshPreview();
                }
            };

            // Theme Radios
            _rbThemeDark.IsCheckedChanged += (s, e) =>
            {
                if (_rbThemeDark.IsChecked == true)
                {
                    _config.Theme = "Dark";
                    RefreshPreview();
                }
            };
            _rbThemeLight.IsCheckedChanged += (s, e) =>
            {
                if (_rbThemeLight.IsChecked == true)
                {
                    _config.Theme = "Light";
                    RefreshPreview();
                }
            };

            // Accent Hex
            _txtAccentHex.TextChanged += (s, e) =>
            {
                string text = (_txtAccentHex.Text ?? "").Trim();
                if (text.StartsWith("#") && (text.Length == 7 || text.Length == 9))
                {
                    _config.AccentColor = text;
                    RefreshPreview();
                }
            };
            _btnResetAccent.Click += (s, e) =>
            {
                _config.AccentColor = "#00D2FF";
                _txtAccentHex.Text = "#00D2FF";
                RefreshPreview();
            };

            // Typography Sliders & Inputs
            _txtTitle.TextChanged += (s, e) => { _config.Title = _txtTitle.Text ?? ""; RefreshPreview(); };
            _sliderTitleSize.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                {
                    _config.TitleFontSize = _sliderTitleSize.Value;
                    _lblTitleSizeVal.Text = _sliderTitleSize.Value.ToString("0.0") + "px";
                    RefreshPreview();
                }
            };

            _txtSubtitle.TextChanged += (s, e) => { _config.Subtitle = _txtSubtitle.Text ?? ""; RefreshPreview(); };
            _sliderSubtitleSize.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                {
                    _config.SubtitleFontSize = _sliderSubtitleSize.Value;
                    _lblSubtitleSizeVal.Text = _sliderSubtitleSize.Value.ToString("0.0") + "px";
                    RefreshPreview();
                }
            };

            _txtArtist.TextChanged += (s, e) => { _config.ArtistCredit = _txtArtist.Text ?? ""; RefreshPreview(); };
            _sliderArtistSize.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                {
                    _config.ArtistFontSize = _sliderArtistSize.Value;
                    _lblArtistSizeVal.Text = _sliderArtistSize.Value.ToString("0.0") + "px";
                    RefreshPreview();
                }
            };

            // Corner Radius
            _sliderCornerRadius.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                {
                    _config.CornerRadius = _sliderCornerRadius.Value;
                    _lblCornerRadiusVal.Text = _sliderCornerRadius.Value.ToString("0.0") + "px";
                    RefreshPreview();
                }
            };

            // Toggles
            _cbEnableSplash.IsCheckedChanged += (s, e) => { _config.ShowSplashScreen = _cbEnableSplash.IsChecked == true; RefreshPreview(); };
            _cbLoadingBar.IsCheckedChanged += (s, e) => { _config.ShowLoadingBar = _cbLoadingBar.IsChecked == true; RefreshPreview(); };
            _cbLoadingText.IsCheckedChanged += (s, e) => { _config.ShowLoadingText = _cbLoadingText.IsChecked == true; RefreshPreview(); };
            _cbFooter.IsCheckedChanged += (s, e) => { _config.ShowFooter = _cbFooter.IsChecked == true; RefreshPreview(); };

            // Duration
            _sliderDuration.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                {
                    _config.SplashDuration = _sliderDuration.Value;
                    _lblDurationVal.Text = _sliderDuration.Value.ToString("0.0") + "s";
                }
            };

            // Reset Button
            _btnResetVisuals.Click += (s, e) =>
            {
                _config.Theme = "Dark";
                _config.AccentColor = "#00D2FF";
                _config.Title = "EffectCraft";
                _config.Subtitle = "Motion & Visual Effects";
                _config.ArtistCredit = "Cosmic Horizon Edition";
                _config.TitleFontSize = 32.0;
                _config.SubtitleFontSize = 11.5;
                _config.ArtistFontSize = 10.0;
                _config.CornerRadius = 18.0;
                _config.ShowLoadingBar = true;
                _config.ShowLoadingText = true;
                _config.ShowSplashScreen = true;
                _config.ShowFooter = true;
                _config.SplashDuration = 3.5;
                _config.SplashStyle = "Classic";
                _config.ArtImage = "splash/art.png";

                PopulateFormFromConfig();
                RefreshPreview();
            };

            // Test Splash Button
            _btnTestSplash.Click += (s, e) =>
            {
                var splash = new SplashWindow(_config, () => { });
                splash.Show();
                Task.Delay((int)(Math.Max(1.5, _config.SplashDuration) * 1000)).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() => splash.Close());
                });
            };

            // Global shortcut recorder
            _btnGlobalShortcut.Click += (s, e) =>
            {
                _isRecordingGlobal = true;
                _btnGlobalShortcut.Content = "Press any key combo...";
                _btnGlobalShortcut.Focus();
            };
            _btnGlobalShortcut.KeyDown += (s, e) =>
            {
                if (!_isRecordingGlobal) return;
                e.Handled = true;
                string combo = FormatKeyCombo(e);
                if (!string.IsNullOrEmpty(combo))
                {
                    _config.Shortcut = combo;
                    _btnGlobalShortcut.Content = combo;
                    _isRecordingGlobal = false;
                }
            };
            _btnResetGlobalShortcut.Click += (s, e) =>
            {
                _config.Shortcut = "Ctrl + Space";
                _btnGlobalShortcut.Content = "Ctrl + Space";
                _isRecordingGlobal = false;
            };

            // Fav shortcut recorder
            _btnFavShortcut.Click += (s, e) =>
            {
                _isRecordingFav = true;
                _btnFavShortcut.Content = "Press any key combo...";
                _btnFavShortcut.Focus();
            };
            _btnFavShortcut.KeyDown += (s, e) =>
            {
                if (!_isRecordingFav) return;
                e.Handled = true;
                string combo = FormatKeyCombo(e);
                if (!string.IsNullOrEmpty(combo))
                {
                    _config.FavoriteShortcut = combo;
                    _btnFavShortcut.Content = combo;
                    _isRecordingFav = false;
                }
            };
            _btnResetFavShortcut.Click += (s, e) =>
            {
                _config.FavoriteShortcut = "Ctrl + D";
                _btnFavShortcut.Content = "Ctrl + D";
                _isRecordingFav = false;
            };

            // Catalog Checkboxes
            _cbEffects.IsCheckedChanged += (s, e) => _config.ConsoleShowEffects = _cbEffects.IsChecked == true;
            _cbPresets.IsCheckedChanged += (s, e) => _config.ConsoleShowPresets = _cbPresets.IsChecked == true;
            _cbLayers.IsCheckedChanged += (s, e) => _config.ConsoleShowLayers = _cbLayers.IsChecked == true;
            _cbCommands.IsCheckedChanged += (s, e) => _config.ConsoleShowCommands = _cbCommands.IsChecked == true;
            _cbRecent.IsCheckedChanged += (s, e) => _config.ConsoleShowRecent = _cbRecent.IsChecked == true;
            _cbShowFavOnOpen.IsCheckedChanged += (s, e) => _config.ShowFavoritesOnOpen = _cbShowFavOnOpen.IsChecked == true;

            // Locate binary button
            _btnLocateExe.Click += async (s, e) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Locate EffectCraft Executable Binary",
                    AllowMultiple = false
                });
                if (files != null && files.Count > 0)
                {
                    string localPath = files[0].Path.LocalPath;
                    _config.BinaryPath = localPath;
                    _lblLinkedExe.Text = "Linked Executable: " + localPath;
                }
            };

            // Re-generate desktop shortcuts button
            _btnCreateShortcuts.Click += (s, e) =>
            {
                bool ok = LinuxDesktopIntegration.CreateDesktopEntries(true, true);
                if (ok)
                {
                    _lblLinkedExe.Text = "✓ Desktop shortcuts regenerated with hotkey: " + _config.Shortcut;
                }
            };

            // Window Close Buttons
            _btnCloseTop.Click += (s, e) => Close();
            _btnClose.Click += (s, e) => Close();
            _btnSave.Click += (s, e) =>
            {
                _config.Save(AppContext.BaseDirectory);
                LinuxDesktopIntegration.EvdevShortcutListener.TargetShortcut = _config.Shortcut;
                LinuxDesktopIntegration.ConfigureInAppShortcut(_config.Shortcut);
                Close();
            };
        }

        private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private string FormatKeyCombo(KeyEventArgs e)
        {
            var key = e.Key;
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift)
            {
                return "";
            }

            var parts = new List<string>();
            if ((e.KeyModifiers & KeyModifiers.Control) != 0) parts.Add("Ctrl");
            if ((e.KeyModifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((e.KeyModifiers & KeyModifiers.Shift) != 0) parts.Add("Shift");
            parts.Add(key.ToString());
            return string.Join(" + ", parts);
        }

        private void SwitchTab(bool showVisual)
        {
            _viewVisual.IsVisible = showVisual;
            _viewPlugins.IsVisible = !showVisual;

            _tabBtnVisual.Background = showVisual ? SolidColorBrush.Parse("#2E2E38") : Brushes.Transparent;
            _tabBtnVisual.Foreground = showVisual ? Brushes.White : SolidColorBrush.Parse("#A5A5AC");
            _tabBtnVisual.FontWeight = showVisual ? FontWeight.SemiBold : FontWeight.Normal;

            _tabBtnPlugins.Background = !showVisual ? SolidColorBrush.Parse("#2E2E38") : Brushes.Transparent;
            _tabBtnPlugins.Foreground = !showVisual ? Brushes.White : SolidColorBrush.Parse("#A5A5AC");
            _tabBtnPlugins.FontWeight = !showVisual ? FontWeight.SemiBold : FontWeight.Normal;
        }

        private void RefreshPreview()
        {
            _previewContainer.Children.Clear();

            bool isLight = string.Equals(_config.Theme, "Light", StringComparison.OrdinalIgnoreCase);
            Color accent = _config.GetAccentColor();
            var accentBrush = new SolidColorBrush(accent);

            bool isPoster = string.Equals(_config.SplashStyle, "Poster", StringComparison.OrdinalIgnoreCase);

            if (isPoster)
            {
                // ================= STYLE 2: MODERN POSTER (NEON UNICORN GLASS) =================
                var posterCard = new Border
                {
                    Width = 720,
                    Height = 420,
                    CornerRadius = new CornerRadius(_config.CornerRadius),
                    BorderThickness = new Thickness(1),
                    BorderBrush = SolidColorBrush.Parse("#353545"),
                    ClipToBounds = true,
                    BoxShadow = new BoxShadows(new BoxShadow { Blur = 38, Spread = 0, OffsetX = 0, OffsetY = 8, Color = Color.FromArgb((byte)160, 0, 0, 0) })
                };

                var posterPanel = new Panel();

                // Background artwork
                var bgImg = new Image { Stretch = Stretch.UniformToFill };
                LoadArtworkImage(bgImg);
                posterPanel.Children.Add(bgImg);

                // Gradient overlay
                var gradOverlay = new Border
                {
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                        GradientStops = new GradientStops
                        {
                            new GradientStop(Color.FromArgb((byte)80, 0, 0, 0), 0.0),
                            new GradientStop(Color.FromArgb((byte)30, 0, 0, 0), 0.4),
                            new GradientStop(Color.FromArgb((byte)190, 14, 14, 18), 0.75),
                            new GradientStop(Color.FromArgb((byte)240, 14, 14, 18), 1.0)
                        }
                    }
                };
                posterPanel.Children.Add(gradOverlay);

                // Content
                var overlayGrid = new Grid { Margin = new Thickness(32), RowDefinitions = new RowDefinitions("Auto,*,Auto") };

                // Top row
                var topStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
                var badge = new Border
                {
                    Width = 42,
                    Height = 42,
                    CornerRadius = new CornerRadius(10),
                    Background = SolidColorBrush.Parse("#0F2338"),
                    BorderBrush = accentBrush,
                    BorderThickness = new Thickness(1.5),
                    Child = new TextBlock
                    {
                        Text = _config.BadgeText,
                        Foreground = accentBrush,
                        FontSize = 18,
                        FontWeight = FontWeight.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                topStack.Children.Add(badge);

                var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                titleStack.Children.Add(new TextBlock
                {
                    Text = _config.Title,
                    Foreground = Brushes.White,
                    FontSize = _config.TitleFontSize > 0 ? _config.TitleFontSize : 24,
                    FontWeight = FontWeight.SemiBold
                });
                titleStack.Children.Add(new TextBlock
                {
                    Text = _config.Subtitle,
                    Foreground = SolidColorBrush.Parse("#A0A0B0"),
                    FontSize = _config.SubtitleFontSize > 0 ? _config.SubtitleFontSize : 12
                });
                topStack.Children.Add(titleStack);
                Grid.SetRow(topStack, 0);
                overlayGrid.Children.Add(topStack);

                // Bottom row
                var bottomStack = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Bottom };
                if (_config.ShowLoadingText)
                {
                    var statusRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                    statusRow.Children.Add(new TextBlock
                    {
                        Text = "Initializing EffectCraft engine...",
                        Foreground = SolidColorBrush.Parse("#D0D0E0"),
                        FontSize = 12
                    });
                    var artistBlock = new TextBlock
                    {
                        Text = _config.ArtistCredit,
                        Foreground = SolidColorBrush.Parse("#707085"),
                        FontSize = _config.ArtistFontSize > 0 ? _config.ArtistFontSize : 11,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    Grid.SetColumn(artistBlock, 1);
                    statusRow.Children.Add(artistBlock);
                    bottomStack.Children.Add(statusRow);
                }

                if (_config.ShowLoadingBar)
                {
                    var pb = new ProgressBar
                    {
                        Height = 4,
                        Minimum = 0,
                        Maximum = 100,
                        Value = 28,
                        Foreground = accentBrush,
                        Background = SolidColorBrush.Parse("#252535"),
                        BorderThickness = new Thickness(0),
                        CornerRadius = new CornerRadius(2)
                    };
                    bottomStack.Children.Add(pb);
                }

                if (_config.ShowFooter)
                {
                    bottomStack.Children.Add(new TextBlock
                    {
                        Text = "ArtCraft Motion & Visual Effects Suite",
                        Foreground = SolidColorBrush.Parse("#606075"),
                        FontSize = 10.5,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 4, 0, 0)
                    });
                }

                Grid.SetRow(bottomStack, 2);
                overlayGrid.Children.Add(bottomStack);

                posterPanel.Children.Add(overlayGrid);
                posterCard.Child = posterPanel;
                _previewContainer.Children.Add(posterCard);
            }
            else
            {
                // ================= STYLE 1: CLASSIC SPLIT (1:1 WINDOWS STABLE) =================
                var classicCard = new Border
                {
                    Width = 730,
                    Height = 450,
                    CornerRadius = new CornerRadius(_config.CornerRadius),
                    BorderThickness = new Thickness(1),
                    Background = isLight ? Brushes.White : SolidColorBrush.Parse("#141416"),
                    BorderBrush = isLight ? SolidColorBrush.Parse("#E1E1E6") : SolidColorBrush.Parse("#2E2E32"),
                    ClipToBounds = true,
                    BoxShadow = new BoxShadows(new BoxShadow { Blur = 38, Spread = 0, OffsetX = 0, OffsetY = 6, Color = Color.FromArgb((byte)(isLight ? 70 : 160), 0, 0, 0) })
                };

                var splitGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("330,*") };

                // Left Column (330px)
                var leftGrid = new Grid { Margin = new Thickness(34, 30, 20, 26), RowDefinitions = new RowDefinitions("Auto,*,Auto") };

                // Top brand
                var brandStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
                var badgeBorder = new Border
                {
                    Width = 46,
                    Height = 46,
                    CornerRadius = new CornerRadius(8),
                    Background = SolidColorBrush.Parse(_config.BadgeBackground),
                    BorderBrush = accentBrush,
                    BorderThickness = new Thickness(1.6),
                    Child = new AvPath
                    {
                        Data = Geometry.Parse("M 104.28,49.49 L 81.55,0 H 50.32 L 47.15,4.76 L 14.75,53.63 L 0,75.85 L 21.55,97.4 L 85.34,60.46 L 102.33,97.5 L 116.34,75.76 L 104.28,49.49 Z M 32.89,65.66 L 65.31,16.79 L 76.22,40.56 L 32.89,65.66 Z"),
                        Fill = accentBrush,
                        Width = 26,
                        Height = 26,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                brandStack.Children.Add(badgeBorder);

                var brandMeta = new StackPanel { Margin = new Thickness(12, 2, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                var pubPill = new Border
                {
                    Background = isLight ? SolidColorBrush.Parse("#EBEBF0") : SolidColorBrush.Parse("#26262A"),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new TextBlock
                    {
                        Text = "ArtCraft",
                        FontSize = 10,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = isLight ? SolidColorBrush.Parse("#404045") : SolidColorBrush.Parse("#A0A0A5")
                    }
                };
                brandMeta.Children.Add(pubPill);

                brandMeta.Children.Add(new TextBlock
                {
                    Text = _config.Subtitle,
                    FontSize = _config.SubtitleFontSize > 0 ? _config.SubtitleFontSize : 11.5,
                    Foreground = isLight ? SolidColorBrush.Parse("#606065") : SolidColorBrush.Parse("#8C8C91"),
                    Margin = new Thickness(0, 3, 0, 0)
                });
                brandStack.Children.Add(brandMeta);
                Grid.SetRow(brandStack, 0);
                leftGrid.Children.Add(brandStack);

                // Middle: Title & Edition
                var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) };
                titleStack.Children.Add(new TextBlock
                {
                    Text = _config.Title,
                    FontSize = _config.TitleFontSize > 0 ? _config.TitleFontSize : 34,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = isLight ? Brushes.Black : Brushes.White,
                    LetterSpacing = 0.5
                });
                titleStack.Children.Add(new TextBlock
                {
                    Text = _config.ArtistCredit,
                    FontSize = _config.ArtistFontSize > 0 ? _config.ArtistFontSize : 10.0,
                    Foreground = isLight ? SolidColorBrush.Parse("#707075") : SolidColorBrush.Parse("#808088"),
                    Margin = new Thickness(0, 4, 0, 0)
                });
                Grid.SetRow(titleStack, 1);
                leftGrid.Children.Add(titleStack);

                // Bottom: Loading & Footer
                var bottomStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Spacing = 8 };
                if (_config.ShowLoadingText)
                {
                    bottomStack.Children.Add(new TextBlock
                    {
                        Text = "Initializing Engine Core...",
                        Foreground = isLight ? SolidColorBrush.Parse("#707075") : SolidColorBrush.Parse("#8C8C91"),
                        FontSize = 11
                    });
                }
                if (_config.ShowLoadingBar)
                {
                    bottomStack.Children.Add(new ProgressBar
                    {
                        Height = 3,
                        Minimum = 0,
                        Maximum = 100,
                        Value = 28,
                        Foreground = accentBrush,
                        Background = isLight ? SolidColorBrush.Parse("#E0E0E6") : SolidColorBrush.Parse("#282830"),
                        BorderThickness = new Thickness(0),
                        CornerRadius = new CornerRadius(1.5)
                    });
                }
                if (_config.ShowFooter)
                {
                    bottomStack.Children.Add(new TextBlock
                    {
                        Text = "ArtCraft Motion & Visual Effects Suite",
                        Foreground = isLight ? SolidColorBrush.Parse("#909095") : SolidColorBrush.Parse("#606065"),
                        FontSize = 10,
                        Margin = new Thickness(0, 4, 0, 0)
                    });
                }
                Grid.SetRow(bottomStack, 2);
                leftGrid.Children.Add(bottomStack);

                Grid.SetColumn(leftGrid, 0);
                splitGrid.Children.Add(leftGrid);

                // Right Column: Artwork Portal
                var artPortal = new Border
                {
                    Margin = new Thickness(14, 18, 22, 18),
                    CornerRadius = new CornerRadius(10),
                    Background = SolidColorBrush.Parse("#0D0D10"),
                    BorderBrush = SolidColorBrush.Parse("#25252A"),
                    BorderThickness = new Thickness(1),
                    ClipToBounds = true
                };
                var rightImg = new Image
                {
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                LoadArtworkImage(rightImg);
                artPortal.Child = rightImg;

                Grid.SetColumn(artPortal, 1);
                splitGrid.Children.Add(artPortal);

                classicCard.Child = splitGrid;
                _previewContainer.Children.Add(classicCard);
            }
        }

        private void LoadArtworkImage(Image img)
        {
            try
            {
                if (!string.IsNullOrEmpty(_config.ArtImage) && File.Exists(_config.ArtImage))
                {
                    img.Source = new Bitmap(_config.ArtImage);
                    return;
                }

                string localAsset = SysPath.Combine(AppContext.BaseDirectory, "assets", "splash.png");
                if (File.Exists(localAsset))
                {
                    img.Source = new Bitmap(localAsset);
                    return;
                }

                var uri = new Uri("avares://CraftConsole/assets/splash.png");
                if (Avalonia.Platform.AssetLoader.Exists(uri))
                {
                    using var stream = Avalonia.Platform.AssetLoader.Open(uri);
                    img.Source = new Bitmap(stream);
                    return;
                }
            }
            catch { }
        }
    }
}
