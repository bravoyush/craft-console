using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace CraftConsole.SetupClassic
{
    public class Program
    {
        public const string VERSION = "1.0.2";

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

        public static void RegisterUninstall(string installDir, string exePath)
        {
            try
            {
                string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CraftConsole";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "Craft Console for EffectCraft & After Effects");
                        key.SetValue("DisplayVersion", VERSION);
                        key.SetValue("DisplayIcon", exePath + ",0");
                        key.SetValue("InstallLocation", installDir);
                        key.SetValue("Publisher", "Storytold / EffectCraft Team");
                        key.SetValue("UninstallString", "\"" + exePath + "\" --uninstall");
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }
    }

    public class ClassicInstallerWindow : Window
    {
        private int _currentStep = 0;
        private const int TOTAL_STEPS = 5;

        // Header controls
        private Grid _headerGrid;
        private TextBlock _lblHeaderTitle;
        private TextBlock _lblHeaderSub;

        // Pages
        private Grid _contentHost;
        private UIElement[] _pages;

        // Page 0: Welcome
        // Page 1: Select Destination Location
        private TextBox _txtDestDir;
        // Page 2: Existing Settings
        private RadioButton _rbKeep;
        private RadioButton _rbReset;
        // Page 3: Additional Tasks
        private CheckBox _chkDesk;
        private CheckBox _chkStart;
        private CheckBox _chkUnified;
        // Page 4: Ready to Install / Progress
        private ProgressBar _progressBar;
        private TextBlock _lblProgressStatus;
        private CheckBox _chkLaunch;

        // Navigation
        private Button _btnBack;
        private Button _btnNext;
        private Button _btnCancel;

        public ClassicInstallerWindow()
        {
            this.Title = "Setup - Craft Console v" + Program.VERSION;
            this.Width = 520;
            this.Height = 400;
            this.MinWidth = 500;
            this.MinHeight = 380;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Background = SystemColors.ControlBrush;
            this.Foreground = SystemColors.ControlTextBrush;
            this.FontFamily = new FontFamily("Segoe UI");

            BuildClassicUI();
            ShowStep(0);
        }

        private void BuildClassicUI()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(65) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. Classic White Header Banner
            _headerGrid = new Grid { Background = Brushes.White };
            _headerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var headStack = new StackPanel { Margin = new Thickness(24, 10, 24, 8), VerticalAlignment = VerticalAlignment.Center };
            _lblHeaderTitle = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Text = "Select Destination Location" };
            _lblHeaderSub = new TextBlock { FontSize = 11, Margin = new Thickness(14, 2, 0, 0), Text = "Where should Craft Console be installed?" };
            headStack.Children.Add(_lblHeaderTitle);
            headStack.Children.Add(_lblHeaderSub);
            _headerGrid.Children.Add(headStack);

            var sepTop = new Separator { Background = SystemColors.ActiveBorderBrush, Height = 1, VerticalAlignment = VerticalAlignment.Bottom };
            _headerGrid.Children.Add(sepTop);

            Grid.SetRow(_headerGrid, 0);
            root.Children.Add(_headerGrid);

            // 2. Middle Content Host
            _contentHost = new Grid { Margin = new Thickness(24, 16, 24, 16) };
            Grid.SetRow(_contentHost, 1);
            root.Children.Add(_contentHost);

            // 3. Classic Footer Bar
            var footGrid = new Grid { Background = SystemColors.ControlBrush };
            footGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            footGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });

            var sepFoot = new Separator { Background = SystemColors.ActiveBorderBrush, Height = 1 };
            Grid.SetRow(sepFoot, 0);
            footGrid.Children.Add(sepFoot);

            var footActions = new Grid { Margin = new Thickness(16, 8, 16, 8) };
            Grid.SetRow(footActions, 1);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            _btnBack = new Button { Content = "< Back", Width = 75, Height = 24, Margin = new Thickness(0, 0, 6, 0) };
            _btnBack.Click += delegate { if (_currentStep > 0) ShowStep(_currentStep - 1); };
            btnStack.Children.Add(_btnBack);

            _btnNext = new Button { Content = "Next >", Width = 75, Height = 24, Margin = new Thickness(0, 0, 14, 0), IsDefault = true };
            _btnNext.Click += delegate { HandleNext(); };
            btnStack.Children.Add(_btnNext);

            _btnCancel = new Button { Content = "Cancel", Width = 75, Height = 24 };
            _btnCancel.Click += delegate { Close(); };
            btnStack.Children.Add(_btnCancel);

            footActions.Children.Add(btnStack);
            footGrid.Children.Add(footActions);

            Grid.SetRow(footGrid, 2);
            root.Children.Add(footGrid);

            this.Content = root;

            InitPages();
        }

        private void InitPages()
        {
            _pages = new UIElement[TOTAL_STEPS];

            // Page 0: Welcome
            var p0 = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            p0.Children.Add(new TextBlock
            {
                Text = "Welcome to the Craft Console Setup Wizard",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12)
            });
            p0.Children.Add(new TextBlock
            {
                Text = "This wizard will install Craft Console v" + Program.VERSION + " on your computer.\n\n" +
                       "Craft Console provides quick effect searching, preset browsing, and workflow tools for EffectCraft and Adobe After Effects.\n\n" +
                       "Click Next to continue, or Cancel to exit Setup.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11.5,
                LineHeight = 18
            });
            _pages[0] = p0;

            // Page 1: Destination Location
            var p1 = new StackPanel();
            p1.Children.Add(new TextBlock
            {
                Text = "Setup will install Craft Console into the following folder.",
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 8)
            });
            p1.Children.Add(new TextBlock
            {
                Text = "To continue, click Next. If you would like to select a different folder, click Browse.",
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var defaultDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "CraftConsole");
            var rowDir = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            rowDir.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowDir.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            rowDir.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtDestDir = new TextBox { Text = defaultDir, Height = 24, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 11.5 };
            Grid.SetColumn(_txtDestDir, 0);
            rowDir.Children.Add(_txtDestDir);

            var btnBrowse = new Button { Content = "Browse...", Width = 75, Height = 24 };
            btnBrowse.Click += delegate {
                var fbd = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = _txtDestDir.Text };
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    _txtDestDir.Text = fbd.SelectedPath;
            };
            Grid.SetColumn(btnBrowse, 2);
            rowDir.Children.Add(btnBrowse);
            p1.Children.Add(rowDir);

            p1.Children.Add(new TextBlock
            {
                Text = "At least 15.0 MB of free disk space is required.",
                FontSize = 11,
                Foreground = SystemColors.GrayTextBrush
            });
            _pages[1] = p1;

            // Page 2: Existing Settings Check
            string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EffectCraft");
            string appDataIni = System.IO.Path.Combine(appData, "splash.ini");
            bool existingSettings = File.Exists(appDataIni);

            var p2 = new StackPanel();
            p2.Children.Add(new TextBlock
            {
                Text = "User Configuration & Preferences",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            if (existingSettings)
            {
                p2.Children.Add(new TextBlock
                {
                    Text = "An existing Craft Console configuration was found in %APPDATA%\\EffectCraft.\n\nHow would you like Setup to handle your settings and favorites?",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11.5,
                    Margin = new Thickness(0, 0, 0, 14)
                });
                _rbKeep = new RadioButton
                {
                    Content = "Preserve existing settings, custom themes & favorites (Recommended)",
                    IsChecked = true,
                    Margin = new Thickness(10, 0, 0, 8),
                    FontSize = 11.5
                };
                _rbReset = new RadioButton
                {
                    Content = "Reset configuration to factory defaults",
                    IsChecked = false,
                    Margin = new Thickness(10, 0, 0, 0),
                    FontSize = 11.5
                };
                p2.Children.Add(_rbKeep);
                p2.Children.Add(_rbReset);
            }
            else
            {
                p2.Children.Add(new TextBlock
                {
                    Text = "User configuration and favorites will be stored in your user profile (%APPDATA%\\EffectCraft).\n\nNo administrator privileges are required.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11.5
                });
            }
            _pages[2] = p2;

            // Page 3: Additional Tasks (Shortcuts)
            var p3 = new StackPanel();
            p3.Children.Add(new TextBlock
            {
                Text = "Select Additional Tasks",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });
            p3.Children.Add(new TextBlock
            {
                Text = "Select the additional tasks you would like Setup to perform while installing Craft Console:",
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 12)
            });

            _chkUnified = new CheckBox
            {
                Content = "Create Unified 'EffectCraft (with Console)' Desktop Shortcut",
                IsChecked = true,
                Margin = new Thickness(10, 0, 0, 8),
                FontSize = 11.5
            };
            _chkDesk = new CheckBox
            {
                Content = "Create a Desktop shortcut for Craft Console",
                IsChecked = true,
                Margin = new Thickness(10, 0, 0, 8),
                FontSize = 11.5
            };
            _chkStart = new CheckBox
            {
                Content = "Create a Start Menu shortcut",
                IsChecked = true,
                Margin = new Thickness(10, 0, 0, 8),
                FontSize = 11.5
            };
            p3.Children.Add(_chkUnified);
            p3.Children.Add(_chkDesk);
            p3.Children.Add(_chkStart);
            _pages[3] = p3;

            // Page 4: Installing & Completion
            var p4 = new StackPanel();
            _lblProgressStatus = new TextBlock
            {
                Text = "Installing files...",
                FontSize = 11.5,
                Margin = new Thickness(0, 10, 0, 8)
            };
            p4.Children.Add(_lblProgressStatus);

            _progressBar = new ProgressBar
            {
                Height = 18,
                IsIndeterminate = false,
                Value = 0,
                Margin = new Thickness(0, 0, 0, 14)
            };
            p4.Children.Add(_progressBar);

            _chkLaunch = new CheckBox
            {
                Content = "Launch Craft Console",
                IsChecked = true,
                FontSize = 11.5,
                Visibility = Visibility.Collapsed
            };
            p4.Children.Add(_chkLaunch);
            _pages[4] = p4;
        }

        private void ShowStep(int step)
        {
            _currentStep = step;
            _contentHost.Children.Clear();
            _contentHost.Children.Add(_pages[step]);

            _btnBack.IsEnabled = (step > 0 && step < 4);

            if (step == 0)
            {
                _headerGrid.Visibility = Visibility.Collapsed;
                _btnNext.Content = "Next >";
            }
            else
            {
                _headerGrid.Visibility = Visibility.Visible;
                if (step == 1)
                {
                    _lblHeaderTitle.Text = "Select Destination Location";
                    _lblHeaderSub.Text = "Where should Craft Console be installed?";
                    _btnNext.Content = "Next >";
                }
                else if (step == 2)
                {
                    _lblHeaderTitle.Text = "User Configuration";
                    _lblHeaderSub.Text = "Choose how to handle your settings";
                    _btnNext.Content = "Next >";
                }
                else if (step == 3)
                {
                    _lblHeaderTitle.Text = "Select Additional Tasks";
                    _lblHeaderSub.Text = "Which additional shortcuts should be created?";
                    _btnNext.Content = "Install";
                }
                else if (step == 4)
                {
                    _lblHeaderTitle.Text = "Installing";
                    _lblHeaderSub.Text = "Please wait while Setup installs Craft Console on your computer.";
                    _btnNext.Content = "Next >";
                    _btnNext.IsEnabled = false;
                    _btnCancel.IsEnabled = false;
                    ExecuteInstallation();
                }
            }
        }

        private void HandleNext()
        {
            if (_currentStep < 3)
            {
                ShowStep(_currentStep + 1);
            }
            else if (_currentStep == 3)
            {
                ShowStep(4);
            }
            else if (_currentStep == 4)
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
            ThreadPool.QueueUserWorkItem(delegate {
                try
                {
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    string targetExe = System.IO.Path.Combine(targetDir, "CraftConsole.exe");

                    // 1. Extract payload
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

                    // 2. Setup %APPDATA%
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

                    // 3. Shortcuts
                    bool doUnified = false, doDesk = false, doStart = false;
                    Dispatcher.Invoke(new Action(delegate {
                        doUnified = _chkUnified.IsChecked == true;
                        doDesk = _chkDesk.IsChecked == true;
                        doStart = _chkStart.IsChecked == true;
                    }));

                    if (doUnified)
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
                        string lnk = System.IO.Path.Combine(progs, "Craft Console.lnk");
                        Program.CreateShortcut(lnk, targetExe, "Craft Console — EffectCraft HUD Companion");
                        Program.RegisterUninstall(targetDir, targetExe);
                    }

                    Thread.Sleep(300);

                    Dispatcher.Invoke(new Action(delegate {
                        _progressBar.Value = 100;
                        _lblProgressStatus.Text = "Completing the Craft Console Setup Wizard";
                        _lblHeaderTitle.Text = "Setup Complete";
                        _lblHeaderSub.Text = "Craft Console has been installed on your computer.";
                        _chkLaunch.Visibility = Visibility.Visible;
                        _btnNext.Content = "Finish";
                        _btnNext.IsEnabled = true;
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
