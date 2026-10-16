using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace CraftConsole.SetupClassic
{
    public class Program
    {
        public const string VERSION = "1.0.2";
        public const string ARTCRAFT_MARK_PATH = "M 104.28,49.49 L 81.55,0 H 50.32 L 47.15,4.76 L 14.75,53.63 L 0,75.85 L 21.55,97.4 L 85.34,60.46 L 102.33,97.5 L 116.34,75.76 L 104.28,49.49 Z M 32.89,65.66 L 65.31,16.79 L 76.22,40.56 L 32.89,65.66 Z";
        public const string STARBURST_PATH = "M 80,0 L 95,65 L 160,80 L 95,95 L 80,160 L 65,95 L 0,80 L 65,65 Z";

        [STAThread]
        public static void Main(string[] args)
        {
            var app = new Application();
            app.Run(new ClassicInstallerWindow());
        }

        public static Stream GetResourceStream(string name)
        {
            try { return Assembly.GetExecutingAssembly().GetManifestResourceStream(name); }
            catch { return null; }
        }

        public static void CreateShortcut(string shortcutPath, string targetExePath, string description, string iconLocation = null)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetExePath });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { System.IO.Path.GetDirectoryName(targetExePath) });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
                string icon = !string.IsNullOrEmpty(iconLocation) ? iconLocation : (targetExePath + ",0");
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { icon });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch { }
        }

        public static string DetectEffectCraftExe()
        {
            // 1. Running process
            try
            {
                foreach (var p in Process.GetProcessesByName("effectcraft"))
                {
                    try
                    {
                        string fn = p.MainModule.FileName;
                        if (!string.IsNullOrEmpty(fn) && File.Exists(fn)) return fn;
                    }
                    catch { }
                }
            }
            catch { }

            // 2. Saved configuration in %APPDATA%\EffectCraft\splash.ini
            try
            {
                string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EffectCraft");
                string ini = System.IO.Path.Combine(appData, "splash.ini");
                if (File.Exists(ini))
                {
                    foreach (string line in File.ReadAllLines(ini))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("effectcraftexe", StringComparison.OrdinalIgnoreCase))
                        {
                            int idx = trimmed.IndexOf('=');
                            if (idx > 0 && idx < trimmed.Length - 1)
                            {
                                string val = trimmed.Substring(idx + 1).Trim().Trim('"', '\'');
                                if (File.Exists(val)) return val;
                            }
                        }
                    }
                }
            }
            catch { }

            // 3. Check relative developer/release builds
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] relativeCandidates = new string[] {
                System.IO.Path.Combine(baseDir, "effectcraft.exe"),
                System.IO.Path.Combine(baseDir, "..", "effectcraft.exe"),
                System.IO.Path.Combine(baseDir, "..", "target", "release", "effectcraft.exe"),
                System.IO.Path.Combine(baseDir, "..", "..", "effectcraft-main", "target", "release", "effectcraft.exe"),
                @"G:\local-ai\effectcraft-main\target\release\effectcraft.exe",
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "EffectCraft", "effectcraft.exe"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EffectCraft", "effectcraft.exe"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "EffectCraft", "effectcraft.exe")
            };

            foreach (string cand in relativeCandidates)
            {
                try
                {
                    if (File.Exists(cand)) return System.IO.Path.GetFullPath(cand);
                }
                catch { }
            }

            return null;
        }

        public static void SaveEffectCraftPathToIni(string ecPath)
        {
            if (string.IsNullOrEmpty(ecPath)) return;
            try
            {
                string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EffectCraft");
                if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
                string iniPath = System.IO.Path.Combine(appData, "splash.ini");

                var lines = new System.Collections.Generic.List<string>();
                bool foundKey = false;
                bool inCraftConsoleSection = false;

                if (File.Exists(iniPath))
                {
                    foreach (string line in File.ReadAllLines(iniPath))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                        {
                            inCraftConsoleSection = trimmed.Equals("[CraftConsole]", StringComparison.OrdinalIgnoreCase);
                        }

                        if (inCraftConsoleSection && trimmed.StartsWith("effectcraftexe", StringComparison.OrdinalIgnoreCase))
                        {
                            lines.Add("effectcraftexe = " + ecPath);
                            foundKey = true;
                        }
                        else
                        {
                            lines.Add(line);
                        }
                    }
                }

                if (!foundKey)
                {
                    if (!inCraftConsoleSection)
                    {
                        lines.Add("");
                        lines.Add("[CraftConsole]");
                    }
                    lines.Add("effectcraftexe = " + ecPath);
                }

                File.WriteAllLines(iniPath, lines.ToArray(), Encoding.UTF8);
            }
            catch { }
        }

        public static void RegisterUninstall(string installDir, string exePath, string uninstallExePath)
        {
            string[] subKeys = new string[] {
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CraftConsole",
                @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\CraftConsole"
            };

            int estSize = 3500;
            try
            {
                if (File.Exists(exePath))
                {
                    long sz = new FileInfo(exePath).Length;
                    if (File.Exists(uninstallExePath)) sz += new FileInfo(uninstallExePath).Length;
                    estSize = (int)(sz / 1024);
                }
            }
            catch { }

            string installDate = DateTime.Now.ToString("yyyyMMdd");
            string uninstallCmd = "\"" + uninstallExePath + "\"";
            string quietUninstallCmd = "\"" + uninstallExePath + "\" /quiet";

            foreach (string sk in subKeys)
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(sk))
                    {
                        if (key != null)
                        {
                            key.SetValue("DisplayName", "Craft Console");
                            key.SetValue("DisplayVersion", VERSION);
                            key.SetValue("DisplayIcon", exePath + ",0");
                            key.SetValue("InstallLocation", installDir);
                            key.SetValue("Publisher", "Storytold / EffectCraft Team");
                            key.SetValue("UninstallString", uninstallCmd);
                            key.SetValue("QuietUninstallString", quietUninstallCmd);
                            key.SetValue("InstallDate", installDate);
                            key.SetValue("EstimatedSize", estSize, RegistryValueKind.DWord);
                            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                            key.SetValue("WindowsInstaller", 0, RegistryValueKind.DWord);
                        }
                    }
                }
                catch { }

                try
                {
                    using (RegistryKey key = Registry.LocalMachine.CreateSubKey(sk))
                    {
                        if (key != null)
                        {
                            key.SetValue("DisplayName", "Craft Console");
                            key.SetValue("DisplayVersion", VERSION);
                            key.SetValue("DisplayIcon", exePath + ",0");
                            key.SetValue("InstallLocation", installDir);
                            key.SetValue("Publisher", "Storytold / EffectCraft Team");
                            key.SetValue("UninstallString", uninstallCmd);
                            key.SetValue("QuietUninstallString", quietUninstallCmd);
                            key.SetValue("InstallDate", installDate);
                            key.SetValue("EstimatedSize", estSize, RegistryValueKind.DWord);
                            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                            key.SetValue("WindowsInstaller", 0, RegistryValueKind.DWord);
                        }
                    }
                }
                catch { }
            }
        }
    }

    public class ClassicInstallerWindow : Window
    {
        private int _currentStep = 0;
        private const int TOTAL_STEPS = 7;

        // Container hosts
        private Grid _heroView;
        private Grid _innerView;
        private StackPanel _welcomePanel;
        private StackPanel _finishPanel;
        private Grid _innerPageHost;

        // Header controls (Inner pages)
        private TextBlock _lblHeaderTitle;
        private TextBlock _lblHeaderSub;

        // Pages array for inner steps (Step 1 to Step 5)
        private UIElement[] _innerPages;

        // Step 1: Destination
        private TextBox _txtDestDir;

        // Step 2: EffectCraft Location
        private TextBox _txtEcExe;
        private Button _btnBrowseEc;
        private TextBlock _lblEcStatus;
        private CheckBox _chkSkipEc;

        // Step 3: Existing Settings
        private RadioButton _rbKeep;
        private RadioButton _rbReset;

        // Step 4: Additional Tasks
        private CheckBox _chkDesk;
        private CheckBox _chkStart;
        private CheckBox _chkUnified;

        // Step 5: Installing
        private ProgressBar _progressBar;
        private TextBlock _lblProgressStatus;

        // Step 6: Finish Checkbox
        private CheckBox _chkLaunch;

        // Footer buttons
        private Button _btnBack;
        private Button _btnNext;
        private Button _btnCancel;

        public ClassicInstallerWindow()
        {
            this.Title = "Craft Console v" + Program.VERSION + " Setup";
            this.Width = 510;
            this.Height = 390;
            this.MinWidth = 510;
            this.MinHeight = 390;
            this.ResizeMode = ResizeMode.CanMinimize;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Background = SystemColors.ControlBrush;
            this.Foreground = SystemColors.ControlTextBrush;
            this.FontFamily = new FontFamily("Segoe UI, Tahoma, sans-serif");

            BuildClassicLayout();
            InitPages();
            ShowStep(0);
        }

        private FrameworkElement CreateHeroBanner()
        {
            var banner = new Grid
            {
                Width = 164,
                ClipToBounds = true
            };

            // Deep studio gradient background
            var bgGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            bgGrad.GradientStops.Add(new GradientStop(Color.FromRgb(26, 38, 54), 0.0));
            bgGrad.GradientStops.Add(new GradientStop(Color.FromRgb(16, 24, 36), 0.5));
            bgGrad.GradientStops.Add(new GradientStop(Color.FromRgb(10, 15, 22), 1.0));
            banner.Background = bgGrad;

            // Blueprint starburst watermark in background (similar to Image 1)
            var starView = new Viewbox
            {
                Width = 140,
                Height = 140,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(-20, 0, 0, 20),
                Opacity = 0.18
            };
            var starPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(Program.STARBURST_PATH),
                Stroke = new SolidColorBrush(Color.FromRgb(0, 180, 255)),
                StrokeThickness = 2.5,
                Fill = Brushes.Transparent
            };
            starView.Child = starPath;
            banner.Children.Add(starView);

            // Center emblem stack
            var centerStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Circular glass shield
            var badgeGrid = new Grid
            {
                Width = 78,
                Height = 78,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var badgeCircle = new Ellipse
            {
                Fill = new SolidColorBrush(Color.FromRgb(24, 34, 48)),
                Stroke = new SolidColorBrush(Color.FromRgb(46, 68, 96)),
                StrokeThickness = 1.5
            };
            badgeGrid.Children.Add(badgeCircle);

            var iconBox = new Viewbox
            {
                Width = 44,
                Height = 44,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var iconPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(Program.ARTCRAFT_MARK_PATH),
                Fill = new SolidColorBrush(Color.FromRgb(0, 210, 255))
            };
            iconBox.Child = iconPath;
            badgeGrid.Children.Add(iconBox);

            centerStack.Children.Add(badgeGrid);

            // Title below badge
            centerStack.Children.Add(new TextBlock
            {
                Text = "CRAFT CONSOLE",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 2)
            });

            centerStack.Children.Add(new TextBlock
            {
                Text = "EffectCraft Companion",
                FontSize = 8.5,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 155, 175)),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            banner.Children.Add(centerStack);

            // Right divider line
            var rightBorder = new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromRgb(210, 215, 222)),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            banner.Children.Add(rightBorder);

            return banner;
        }

        private void BuildClassicLayout()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });

            // ROW 0: Content Area
            var mainContainer = new Grid();

            // 1. Hero View (Used for Step 0 Welcome and Step 6 Finish)
            _heroView = new Grid();
            _heroView.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(164) });
            _heroView.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left Hero Banner
            var heroBanner = CreateHeroBanner();
            Grid.SetColumn(heroBanner, 0);
            _heroView.Children.Add(heroBanner);

            // Right Canvas
            var heroRightHost = new Border
            {
                Background = Brushes.White,
                Padding = new Thickness(24, 20, 20, 16)
            };

            var heroRightStack = new Grid();
            _welcomePanel = new StackPanel();
            _finishPanel = new StackPanel();

            heroRightStack.Children.Add(_welcomePanel);
            heroRightStack.Children.Add(_finishPanel);
            heroRightHost.Child = heroRightStack;

            Grid.SetColumn(heroRightHost, 1);
            _heroView.Children.Add(heroRightHost);

            mainContainer.Children.Add(_heroView);

            // 2. Inner View (Used for Steps 1 through 5)
            _innerView = new Grid { Visibility = Visibility.Collapsed };
            _innerView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
            _innerView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Classic White Header Band
            var headerBand = new Grid { Background = Brushes.White };
            headerBand.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerBand.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });

            var headTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(22, 0, 0, 0) };
            _lblHeaderTitle = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Text = "Select Destination Location" };
            _lblHeaderSub = new TextBlock { FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(70, 70, 70)), Margin = new Thickness(14, 2, 0, 0), Text = "Where should Craft Console be installed?" };
            headTextStack.Children.Add(_lblHeaderTitle);
            headTextStack.Children.Add(_lblHeaderSub);
            Grid.SetColumn(headTextStack, 0);
            headerBand.Children.Add(headTextStack);

            // Small header icon on top right
            var smallIconBox = new Viewbox
            {
                Width = 28,
                Height = 28,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
            var smallPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(Program.ARTCRAFT_MARK_PATH),
                Fill = new SolidColorBrush(Color.FromRgb(0, 130, 200))
            };
            smallIconBox.Child = smallPath;
            Grid.SetColumn(smallIconBox, 1);
            headerBand.Children.Add(smallIconBox);

            var sepHeader = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(215, 215, 215)),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            headerBand.Children.Add(sepHeader);

            Grid.SetRow(headerBand, 0);
            _innerView.Children.Add(headerBand);

            // Inner Page Host
            _innerPageHost = new Grid
            {
                Background = SystemColors.ControlBrush,
                Margin = new Thickness(24, 14, 24, 14)
            };
            Grid.SetRow(_innerPageHost, 1);
            _innerView.Children.Add(_innerPageHost);

            mainContainer.Children.Add(_innerView);

            Grid.SetRow(mainContainer, 0);
            root.Children.Add(mainContainer);

            // ROW 1: Footer Bar
            var footGrid = new Grid { Background = SystemColors.ControlBrush };
            var sepFoot = new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(215, 215, 215)), VerticalAlignment = VerticalAlignment.Top };
            footGrid.Children.Add(sepFoot);

            var btnStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            };

            _btnBack = new Button { Content = "< Back", Width = 76, Height = 23, Margin = new Thickness(0, 0, 6, 0) };
            _btnBack.Click += delegate { if (_currentStep > 0) ShowStep(_currentStep - 1); };
            btnStack.Children.Add(_btnBack);

            _btnNext = new Button { Content = "Next >", Width = 76, Height = 23, Margin = new Thickness(0, 0, 12, 0), IsDefault = true };
            _btnNext.Click += delegate { HandleNext(); };
            btnStack.Children.Add(_btnNext);

            _btnCancel = new Button { Content = "Cancel", Width = 76, Height = 23, IsCancel = true };
            _btnCancel.Click += delegate { Close(); };
            btnStack.Children.Add(_btnCancel);

            footGrid.Children.Add(btnStack);

            Grid.SetRow(footGrid, 1);
            root.Children.Add(footGrid);

            this.Content = root;
        }

        private void InitPages()
        {
            // --- Page 0: Welcome Panel ---
            _welcomePanel.Children.Add(new TextBlock
            {
                Text = "Welcome to the Craft Console Setup Wizard",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            });

            _welcomePanel.Children.Add(new TextBlock
            {
                Text = "This wizard will guide you through the installation of Craft Console v" + Program.VERSION + " for EffectCraft & Adobe After Effects.\n\n" +
                       "It is recommended that you close EffectCraft before starting Setup. This will make it possible to update relevant files without having to reboot your computer.\n\n" +
                       "Click Next to continue, or Cancel to exit Setup.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                LineHeight = 17,
                Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40))
            });

            // --- Inner Pages Array ---
            _innerPages = new UIElement[TOTAL_STEPS];

            // --- Step 1: Destination Location ---
            var p1 = new StackPanel();
            p1.Children.Add(new TextBlock
            {
                Text = "Setup will install Craft Console into the following folder.\n\nTo continue, click Next. If you would like to select a different folder, click Browse.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                LineHeight = 16,
                Margin = new Thickness(0, 0, 0, 14)
            });

            var defaultDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "CraftConsole");
            var rowDir = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            rowDir.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowDir.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            rowDir.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtDestDir = new TextBox { Text = defaultDir, Height = 23, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 11 };
            Grid.SetColumn(_txtDestDir, 0);
            rowDir.Children.Add(_txtDestDir);

            var btnBrowseDir = new Button { Content = "Browse...", Width = 76, Height = 23 };
            btnBrowseDir.Click += delegate {
                var fbd = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select Destination Directory for Craft Console",
                    SelectedPath = _txtDestDir.Text
                };
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _txtDestDir.Text = fbd.SelectedPath;
                }
            };
            Grid.SetColumn(btnBrowseDir, 2);
            rowDir.Children.Add(btnBrowseDir);
            p1.Children.Add(rowDir);

            p1.Children.Add(new TextBlock
            {
                Text = "At least 15 MB of free disk space is required.",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
            });
            _innerPages[1] = p1;

            // --- Step 2: EffectCraft Location Selection ---
            var p2 = new StackPanel();
            p2.Children.Add(new TextBlock
            {
                Text = "Setup links with your EffectCraft application so Craft Console can inject HUD overlays, launch the console capsule, and customize splash art.\n\nTo accept the detected executable, click Next. If you would like to select a different build or version, click Browse.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                LineHeight = 16,
                Margin = new Thickness(0, 0, 0, 12)
            });

            string detectedEc = Program.DetectEffectCraftExe();
            var rowEc = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            rowEc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowEc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            rowEc.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtEcExe = new TextBox { Text = detectedEc ?? "", Height = 23, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 11 };
            Grid.SetColumn(_txtEcExe, 0);
            rowEc.Children.Add(_txtEcExe);

            _btnBrowseEc = new Button { Content = "Browse...", Width = 76, Height = 23 };
            _btnBrowseEc.Click += delegate {
                var ofd = new OpenFileDialog
                {
                    Title = "Locate EffectCraft Executable",
                    Filter = "EffectCraft Executable (effectcraft.exe)|effectcraft.exe|Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                    InitialDirectory = !string.IsNullOrEmpty(_txtEcExe.Text) && Directory.Exists(System.IO.Path.GetDirectoryName(_txtEcExe.Text)) ? System.IO.Path.GetDirectoryName(_txtEcExe.Text) : Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };
                if (ofd.ShowDialog() == true)
                {
                    _txtEcExe.Text = ofd.FileName;
                    UpdateEcStatus();
                }
            };
            Grid.SetColumn(_btnBrowseEc, 2);
            rowEc.Children.Add(_btnBrowseEc);
            p2.Children.Add(rowEc);

            _lblEcStatus = new TextBlock { FontSize = 10.5, Margin = new Thickness(0, 0, 0, 14) };
            p2.Children.Add(_lblEcStatus);
            UpdateEcStatus();

            _chkSkipEc = new CheckBox
            {
                Content = "I primarily use Adobe After Effects (Skip EffectCraft configuration for now)",
                IsChecked = false,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            _chkSkipEc.Checked += delegate {
                _txtEcExe.IsEnabled = false;
                _btnBrowseEc.IsEnabled = false;
                _lblEcStatus.Text = "EffectCraft linking skipped. Craft Console will run in standalone AE mode.";
                _lblEcStatus.Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100));
            };
            _chkSkipEc.Unchecked += delegate {
                _txtEcExe.IsEnabled = true;
                _btnBrowseEc.IsEnabled = true;
                UpdateEcStatus();
            };
            p2.Children.Add(_chkSkipEc);
            _innerPages[2] = p2;

            // --- Step 3: User Configuration / Settings ---
            var p3 = new StackPanel();
            string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EffectCraft");
            string appDataIni = System.IO.Path.Combine(appData, "splash.ini");
            bool existingSettings = File.Exists(appDataIni);

            if (existingSettings)
            {
                p3.Children.Add(new TextBlock
                {
                    Text = "An existing Craft Console configuration was found in %APPDATA%\\EffectCraft.\n\nHow would you like Setup to handle your preferences and favorites?",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    LineHeight = 16,
                    Margin = new Thickness(0, 0, 0, 14)
                });

                _rbKeep = new RadioButton
                {
                    Content = "Keep existing settings, visual themes & favorites (Recommended)",
                    IsChecked = true,
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                _rbReset = new RadioButton
                {
                    Content = "Reset configuration to clean factory defaults",
                    IsChecked = false,
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 0, 14)
                };
                p3.Children.Add(_rbKeep);
                p3.Children.Add(_rbReset);
            }
            else
            {
                p3.Children.Add(new TextBlock
                {
                    Text = "User configuration and favorites will be stored in your user profile:\n\n%APPDATA%\\EffectCraft\\splash.ini\n\nNo administrator privileges are required.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    LineHeight = 16,
                    Margin = new Thickness(0, 0, 0, 14)
                });
            }
            _innerPages[3] = p3;

            // --- Step 4: Additional Tasks ---
            var p4 = new StackPanel();
            p4.Children.Add(new TextBlock
            {
                Text = "Select the additional shortcuts you would like Setup to create while installing Craft Console:",
                FontSize = 11,
                LineHeight = 16,
                Margin = new Thickness(0, 0, 0, 12)
            });

            _chkDesk = new CheckBox
            {
                Content = "Create a desktop shortcut for Craft Console",
                IsChecked = true,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8)
            };
            p4.Children.Add(_chkDesk);

            _chkStart = new CheckBox
            {
                Content = "Create Start Menu shortcuts & register in Windows Installed Apps",
                IsChecked = true,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8)
            };
            p4.Children.Add(_chkStart);

            _chkUnified = new CheckBox
            {
                Content = "Create Unified 'EffectCraft (with Console)' desktop shortcut",
                IsChecked = true,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8)
            };
            p4.Children.Add(_chkUnified);

            _innerPages[4] = p4;

            // --- Step 5: Installing ---
            var p5 = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _lblProgressStatus = new TextBlock
            {
                Text = "Extracting files...",
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8)
            };
            p5.Children.Add(_lblProgressStatus);

            _progressBar = new ProgressBar
            {
                Height = 16,
                IsIndeterminate = true
            };
            p5.Children.Add(_progressBar);
            _innerPages[5] = p5;

            // --- Page 6: Finish Panel ---
            _finishPanel.Children.Add(new TextBlock
            {
                Text = "Completing the Craft Console Setup Wizard",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            });

            _finishPanel.Children.Add(new TextBlock
            {
                Text = "Craft Console has been installed on your computer.\n\nThe application may be launched by selecting the installed shortcuts.\n\nClick Finish to exit Setup.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                LineHeight = 17,
                Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            _chkLaunch = new CheckBox
            {
                Content = "Launch Craft Console",
                IsChecked = true,
                FontSize = 11
            };
            _finishPanel.Children.Add(_chkLaunch);
        }

        private void UpdateEcStatus()
        {
            string path = (_txtEcExe.Text ?? "").Trim();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                _lblEcStatus.Text = "✓ Valid EffectCraft executable located: " + System.IO.Path.GetFileName(path);
                _lblEcStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 140, 60));
            }
            else if (string.IsNullOrEmpty(path))
            {
                _lblEcStatus.Text = "No executable selected. Please click Browse to pick effectcraft.exe or skip below.";
                _lblEcStatus.Foreground = new SolidColorBrush(Color.FromRgb(180, 110, 0));
            }
            else
            {
                _lblEcStatus.Text = "Specified file does not exist. Please browse for effectcraft.exe.";
                _lblEcStatus.Foreground = new SolidColorBrush(Color.FromRgb(200, 40, 40));
            }
        }

        private void ShowStep(int step)
        {
            _currentStep = step;

            if (step == 0 || step == 6)
            {
                // Hero View
                _innerView.Visibility = Visibility.Collapsed;
                _heroView.Visibility = Visibility.Visible;

                if (step == 0)
                {
                    _welcomePanel.Visibility = Visibility.Visible;
                    _finishPanel.Visibility = Visibility.Collapsed;

                    _btnBack.IsEnabled = false;
                    _btnNext.Content = "Next >";
                    _btnNext.IsEnabled = true;
                    _btnCancel.IsEnabled = true;
                }
                else
                {
                    _welcomePanel.Visibility = Visibility.Collapsed;
                    _finishPanel.Visibility = Visibility.Visible;

                    _btnBack.IsEnabled = false;
                    _btnNext.Content = "Finish";
                    _btnNext.IsEnabled = true;
                    _btnCancel.IsEnabled = false;
                }
            }
            else
            {
                // Inner View
                _heroView.Visibility = Visibility.Collapsed;
                _innerView.Visibility = Visibility.Visible;

                _innerPageHost.Children.Clear();
                if (_innerPages[step] != null)
                {
                    _innerPageHost.Children.Add(_innerPages[step]);
                }

                _btnBack.IsEnabled = (step > 1 && step < 5);
                _btnNext.IsEnabled = true;
                _btnCancel.IsEnabled = true;

                if (step == 1)
                {
                    _lblHeaderTitle.Text = "Select Destination Location";
                    _lblHeaderSub.Text = "Where should Craft Console be installed?";
                    _btnNext.Content = "Next >";
                }
                else if (step == 2)
                {
                    _lblHeaderTitle.Text = "Select EffectCraft Location";
                    _lblHeaderSub.Text = "Where is your EffectCraft application installed?";
                    _btnNext.Content = "Next >";
                    UpdateEcStatus();
                }
                else if (step == 3)
                {
                    _lblHeaderTitle.Text = "User Configuration";
                    _lblHeaderSub.Text = "Choose how to handle your settings and favorites";
                    _btnNext.Content = "Next >";
                }
                else if (step == 4)
                {
                    _lblHeaderTitle.Text = "Select Additional Tasks";
                    _lblHeaderSub.Text = "Which additional shortcuts should be created?";
                    _btnNext.Content = "Install";
                }
                else if (step == 5)
                {
                    _lblHeaderTitle.Text = "Installing";
                    _lblHeaderSub.Text = "Please wait while Setup installs Craft Console on your computer.";
                    _btnBack.IsEnabled = false;
                    _btnNext.IsEnabled = false;
                    _btnCancel.IsEnabled = false;
                    ExecuteInstallation();
                }
            }
        }

        private void HandleNext()
        {
            if (_currentStep == 0)
            {
                ShowStep(1);
            }
            else if (_currentStep == 1)
            {
                string targetDir = (_txtDestDir.Text ?? "").Trim();
                if (string.IsNullOrEmpty(targetDir))
                {
                    System.Windows.Forms.MessageBox.Show("Please choose a valid destination directory.", "Setup", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
                ShowStep(2);
            }
            else if (_currentStep == 2)
            {
                // EffectCraft location check
                if (_chkSkipEc.IsChecked != true)
                {
                    string ec = (_txtEcExe.Text ?? "").Trim();
                    if (!string.IsNullOrEmpty(ec) && !File.Exists(ec))
                    {
                        var res = System.Windows.Forms.MessageBox.Show(
                            "The specified EffectCraft executable was not found:\n" + ec + "\n\nDo you want to continue anyway?",
                            "EffectCraft Location Warning",
                            System.Windows.Forms.MessageBoxButtons.YesNo,
                            System.Windows.Forms.MessageBoxIcon.Question);
                        if (res != System.Windows.Forms.DialogResult.Yes) return;
                    }
                }
                ShowStep(3);
            }
            else if (_currentStep == 3)
            {
                ShowStep(4);
            }
            else if (_currentStep == 4)
            {
                ShowStep(5);
            }
            else if (_currentStep == 6)
            {
                // Finished
                if (_chkLaunch.IsChecked == true)
                {
                    string targetExe = System.IO.Path.Combine((_txtDestDir.Text ?? "").Trim(), "CraftConsole.exe");
                    if (File.Exists(targetExe))
                    {
                        try { Process.Start(targetExe); } catch { }
                    }
                }
                Close();
            }
        }

        private void ExecuteInstallation()
        {
            string targetDir = (_txtDestDir.Text ?? "").Trim();
            string ecExe = (_chkSkipEc.IsChecked == true) ? null : (_txtEcExe.Text ?? "").Trim();

            ThreadPool.QueueUserWorkItem(delegate {
                try
                {
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    string targetExe = System.IO.Path.Combine(targetDir, "CraftConsole.exe");
                    string uninstallExe = System.IO.Path.Combine(targetDir, "Uninstall.exe");

                    // 1. Extract payload executable
                    using (Stream src = Program.GetResourceStream("AppPayload"))
                    {
                        if (src != null)
                        {
                            using (FileStream fs = new FileStream(targetExe, FileMode.Create, FileAccess.Write))
                            {
                                byte[] buf = new byte[81920];
                                int read;
                                while ((read = src.Read(buf, 0, buf.Length)) > 0)
                                    fs.Write(buf, 0, read);
                            }
                        }
                    }

                    // 2. Extract uninstaller
                    using (Stream unSrc = Program.GetResourceStream("UninstallPayload"))
                    {
                        if (unSrc != null)
                        {
                            using (FileStream fs = new FileStream(uninstallExe, FileMode.Create, FileAccess.Write))
                            {
                                byte[] buf = new byte[81920];
                                int read;
                                while ((read = unSrc.Read(buf, 0, buf.Length)) > 0)
                                    fs.Write(buf, 0, read);
                            }
                        }
                    }

                    // 3. Setup %APPDATA%\EffectCraft
                    string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EffectCraft");
                    if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);

                    string appSplashDir = System.IO.Path.Combine(appData, "splash");
                    if (!Directory.Exists(appSplashDir)) Directory.CreateDirectory(appSplashDir);

                    string appArt = System.IO.Path.Combine(appSplashDir, "art.png");
                    if (!File.Exists(appArt))
                    {
                        using (Stream artRes = Program.GetResourceStream("SplashArt"))
                        {
                            if (artRes != null)
                            {
                                using (FileStream fs = new FileStream(appArt, FileMode.Create, FileAccess.Write))
                                {
                                    byte[] buf = new byte[81920];
                                    int read;
                                    while ((read = artRes.Read(buf, 0, buf.Length)) > 0)
                                        fs.Write(buf, 0, read);
                                }
                            }
                        }
                    }

                    // Handle reset
                    string appIni = System.IO.Path.Combine(appData, "splash.ini");
                    Dispatcher.Invoke(new Action(delegate {
                        if (_rbReset != null && _rbReset.IsChecked == true && File.Exists(appIni))
                        {
                            try { File.Delete(appIni); } catch { }
                        }
                    }));

                    // Save selected EffectCraft path into splash.ini
                    if (!string.IsNullOrEmpty(ecExe) && File.Exists(ecExe))
                    {
                        Program.SaveEffectCraftPathToIni(ecExe);
                    }

                    // 4. Shortcuts
                    bool doUnified = false, doDesk = false, doStart = false;
                    Dispatcher.Invoke(new Action(delegate {
                        doUnified = _chkUnified.IsChecked == true;
                        doDesk = _chkDesk.IsChecked == true;
                        doStart = _chkStart.IsChecked == true;
                    }));

                    if (doUnified && !string.IsNullOrEmpty(ecExe) && File.Exists(ecExe))
                    {
                        string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        string lnk = System.IO.Path.Combine(desk, "EffectCraft (with Console).lnk");
                        Program.CreateShortcut(lnk, targetExe, "Launch EffectCraft with Craft Console companion");
                    }
                    if (doDesk)
                    {
                        string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        string lnk = System.IO.Path.Combine(desk, "Craft Console.lnk");
                        Program.CreateShortcut(lnk, targetExe, "Craft Console — EffectCraft HUD Companion");
                    }
                    if (doStart)
                    {
                        string progs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

                        // Root Start Menu shortcuts
                        string lnk = System.IO.Path.Combine(progs, "Craft Console.lnk");
                        Program.CreateShortcut(lnk, targetExe, "Craft Console — EffectCraft HUD Companion");

                        string lnkUn = System.IO.Path.Combine(progs, "Uninstall Craft Console.lnk");
                        Program.CreateShortcut(lnkUn, uninstallExe, "Uninstall Craft Console", uninstallExe + ",0");

                        // Start Menu folder "Craft Console"
                        string folder = System.IO.Path.Combine(progs, "Craft Console");
                        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                        string fApp = System.IO.Path.Combine(folder, "Craft Console.lnk");
                        Program.CreateShortcut(fApp, targetExe, "Craft Console — EffectCraft HUD Companion");

                        string fUn = System.IO.Path.Combine(folder, "Uninstall Craft Console.lnk");
                        Program.CreateShortcut(fUn, uninstallExe, "Uninstall Craft Console", uninstallExe + ",0");
                    }

                    // 5. Always register in Windows Installed Apps & Control Panel
                    Program.RegisterUninstall(targetDir, targetExe, uninstallExe);

                    Thread.Sleep(300);

                    Dispatcher.Invoke(new Action(delegate {
                        _progressBar.IsIndeterminate = false;
                        _progressBar.Value = 100;
                        ShowStep(6);
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(new Action(delegate {
                        _lblProgressStatus.Text = "Error: " + ex.Message;
                        _btnCancel.IsEnabled = true;
                        System.Windows.Forms.MessageBox.Show("Install Error: " + ex.Message, "Setup Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }));
                }
            });
        }
    }
}
