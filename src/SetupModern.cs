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

namespace CraftConsole.SetupModern
{
    public class Program
    {
        public const string VERSION = "1.0.2";
        public const string ARTCRAFT_MARK_PATH = "M 104.28,49.49 L 81.55,0 H 50.32 L 47.15,4.76 L 14.75,53.63 L 0,75.85 L 21.55,97.4 L 85.34,60.46 L 102.33,97.5 L 116.34,75.76 L 104.28,49.49 Z M 32.89,65.66 L 65.31,16.79 L 76.22,40.56 L 32.89,65.66 Z";

        [STAThread]
        public static void Main(string[] args)
        {
            var app = new Application();
            app.Run(new ModernInstallerWindow());
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

    public class ModernInstallerWindow : Window
    {
        private TextBox _txtInstallDir;
        private CheckBox _chkUnified;
        private CheckBox _chkDesktop;
        private CheckBox _chkStartMenu;
        private RadioButton _rbKeepSettings;
        private RadioButton _rbResetSettings;
        private ProgressBar _progressBar;
        private TextBlock _statusText;
        private Button _btnInstall;
        private Button _btnCancel;
        private CheckBox _chkLaunchAfter;

        public ModernInstallerWindow()
        {
            this.Title = "Craft Console v" + Program.VERSION + " — Modern Studio Setup";
            this.Width = 640;
            this.Height = 580;
            this.MinWidth = 580;
            this.MinHeight = 520;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Background = new SolidColorBrush(Color.FromRgb(20, 20, 24));
            this.Foreground = Brushes.White;
            this.FontFamily = new FontFamily("Segoe UI");

            BuildUI();
        }

        private void BuildUI()
        {
            var rootGrid = new Grid { Margin = new Thickness(24) };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. Header
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            var brandRow = new StackPanel { Orientation = Orientation.Horizontal };

            var badgeBorder = new Border
            {
                Width = 36, Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(15, 35, 56)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 210, 255)),
                BorderThickness = new Thickness(1.5),
                VerticalAlignment = VerticalAlignment.Center
            };
            var badgeMark = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(Program.ARTCRAFT_MARK_PATH),
                Fill = new SolidColorBrush(Color.FromRgb(0, 210, 255)),
                Width = 20, Height = 20,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeBorder.Child = badgeMark;
            brandRow.Children.Add(badgeBorder);

            var titleStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = "Craft Console Setup",
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "Modern Studio Installer • v" + Program.VERSION,
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 165)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            brandRow.Children.Add(titleStack);
            header.Children.Add(brandRow);

            header.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 50)),
                Margin = new Thickness(0, 14, 0, 0)
            });
            Grid.SetRow(header, 0);
            rootGrid.Children.Add(header);

            // 2. Body ScrollViewer
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var body = new StackPanel();

            // Destination Folder Card
            var destCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var destStack = new StackPanel();
            destStack.Children.Add(new TextBlock
            {
                Text = "INSTALLATION DIRECTORY",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 255)),
                Margin = new Thickness(0, 0, 0, 6)
            });

            var defaultDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "CraftConsole");
            var pathRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtInstallDir = new TextBox
            {
                Text = defaultDir,
                Height = 30,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(16, 16, 20)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 68)),
                Padding = new Thickness(8, 0, 8, 0),
                FontSize = 12
            };
            Grid.SetColumn(_txtInstallDir, 0);
            pathRow.Children.Add(_txtInstallDir);

            var btnBrowseDir = new Button
            {
                Content = "Browse...",
                Width = 85, Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 50)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 75)),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnBrowseDir.Click += delegate {
                var fbd = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select Installation Directory for Craft Console",
                    SelectedPath = _txtInstallDir.Text
                };
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _txtInstallDir.Text = fbd.SelectedPath;
                }
            };
            Grid.SetColumn(btnBrowseDir, 2);
            pathRow.Children.Add(btnBrowseDir);
            destStack.Children.Add(pathRow);
            destCard.Child = destStack;
            body.Children.Add(destCard);

            // Existing Settings Detection Card
            string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EffectCraft");
            string appDataIni = System.IO.Path.Combine(appData, "splash.ini");
            bool existingSettings = File.Exists(appDataIni);

            var settingsCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb((byte)(existingSettings ? 18 : 26), (byte)(existingSettings ? 30 : 26), (byte)(existingSettings ? 44 : 32))),
                BorderBrush = new SolidColorBrush(Color.FromRgb((byte)(existingSettings ? 0 : 42), (byte)(existingSettings ? 160 : 42), (byte)(existingSettings ? 220 : 52))),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var sStack = new StackPanel();
            sStack.Children.Add(new TextBlock
            {
                Text = existingSettings ? "⚡ EXISTING SETTINGS DETECTED" : "USER CONFIGURATION",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 255)),
                Margin = new Thickness(0, 0, 0, 6)
            });

            if (existingSettings)
            {
                sStack.Children.Add(new TextBlock
                {
                    Text = "Previous custom themes, hotkeys, and favorites were found in %APPDATA%\\EffectCraft.",
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 195, 215)),
                    Margin = new Thickness(0, 0, 0, 8)
                });
                _rbKeepSettings = new RadioButton
                {
                    Content = "Keep existing configuration, custom themes & favorites (Recommended)",
                    IsChecked = true,
                    GroupName = "ExistingSettings",
                    Foreground = Brushes.White,
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                _rbResetSettings = new RadioButton
                {
                    Content = "Reset configuration to clean factory defaults",
                    IsChecked = false,
                    GroupName = "ExistingSettings",
                    Foreground = new SolidColorBrush(Color.FromRgb(190, 190, 205)),
                    FontSize = 12
                };
                sStack.Children.Add(_rbKeepSettings);
                sStack.Children.Add(_rbResetSettings);
            }
            else
            {
                sStack.Children.Add(new TextBlock
                {
                    Text = "User settings and favorites will be stored in %APPDATA%\\EffectCraft (zero admin rights needed).",
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 175))
                });
            }
            settingsCard.Child = sStack;
            body.Children.Add(settingsCard);

            // Shortcuts Options Card
            var optCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var optStack = new StackPanel();
            optStack.Children.Add(new TextBlock
            {
                Text = "SHORTCUTS & INTEGRATION",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 255)),
                Margin = new Thickness(0, 0, 0, 8)
            });

            _chkUnified = new CheckBox
            {
                Content = "Create Unified 'EffectCraft (with Console)' Desktop Shortcut",
                IsChecked = true,
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _chkDesktop = new CheckBox
            {
                Content = "Create Desktop Shortcut (Craft Console)",
                IsChecked = true,
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _chkStartMenu = new CheckBox
            {
                Content = "Create Start Menu Shortcut & Register in Windows Installed Apps",
                IsChecked = true,
                Foreground = Brushes.White,
                FontSize = 12
            };
            optStack.Children.Add(_chkUnified);
            optStack.Children.Add(_chkDesktop);
            optStack.Children.Add(_chkStartMenu);
            optCard.Child = optStack;
            body.Children.Add(optCard);

            // Progress & Status
            _statusText = new TextBlock
            {
                Text = "Ready to install Craft Console.",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 155)),
                Margin = new Thickness(2, 0, 0, 6)
            };
            body.Children.Add(_statusText);

            _progressBar = new ProgressBar
            {
                Height = 6,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 38)),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 255)),
                BorderThickness = new Thickness(0),
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 10)
            };
            body.Children.Add(_progressBar);

            _chkLaunchAfter = new CheckBox
            {
                Content = "Launch Craft Console after installation completes",
                IsChecked = true,
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            };
            body.Children.Add(_chkLaunchAfter);

            scroll.Content = body;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            // 3. Footer
            var footer = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var hint = new TextBlock
            {
                Text = "No temporary files created in download folder.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 125)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(hint, 0);
            footer.Children.Add(hint);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal };
            _btnCancel = new Button
            {
                Content = "Cancel",
                Width = 85, Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(190, 190, 200)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 65)),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _btnCancel.Click += delegate { Close(); };
            btnStack.Children.Add(_btnCancel);

            _btnInstall = new Button
            {
                Content = "Install Now",
                Width = 130, Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 150, 255)),
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _btnInstall.Click += delegate { ExecuteInstall(); };
            btnStack.Children.Add(_btnInstall);

            Grid.SetColumn(btnStack, 1);
            footer.Children.Add(btnStack);

            Grid.SetRow(footer, 2);
            rootGrid.Children.Add(footer);

            this.Content = rootGrid;
        }

        private void ExecuteInstall()
        {
            string targetDir = (_txtInstallDir.Text ?? "").Trim();
            if (string.IsNullOrEmpty(targetDir))
            {
                System.Windows.Forms.MessageBox.Show("Please choose a valid install directory.", "Setup", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }

            _btnInstall.IsEnabled = false;
            _btnCancel.IsEnabled = false;
            _progressBar.Visibility = Visibility.Visible;
            _progressBar.IsIndeterminate = true;
            _statusText.Text = "Installing Craft Console to " + targetDir + "...";

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

                    // 2. Extract uninstaller executable
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

                    // 3. Setup %APPDATA%
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

                    // Handle existing settings
                    string appIni = System.IO.Path.Combine(appData, "splash.ini");
                    Dispatcher.Invoke(new Action(delegate {
                        if (_rbResetSettings != null && _rbResetSettings.IsChecked == true && File.Exists(appIni))
                        {
                            try { File.Delete(appIni); } catch { }
                        }
                    }));

                    // 4. Shortcuts
                    bool doUnified = false, doDesk = false, doStart = false, launch = false;
                    Dispatcher.Invoke(new Action(delegate {
                        doUnified = _chkUnified.IsChecked == true;
                        doDesk = _chkDesktop.IsChecked == true;
                        doStart = _chkStartMenu.IsChecked == true;
                        launch = _chkLaunchAfter.IsChecked == true;
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

                    Thread.Sleep(400);

                    Dispatcher.Invoke(new Action(delegate {
                        _progressBar.IsIndeterminate = false;
                        _progressBar.Value = 100;
                        _statusText.Text = "✓ Installation successfully completed!";
                        _statusText.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 150));

                        if (launch && File.Exists(targetExe))
                        {
                            try { Process.Start(targetExe); } catch { }
                        }

                        System.Windows.Forms.MessageBox.Show("Craft Console v" + Program.VERSION + " has been successfully installed!", "Setup Complete", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                        Close();
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(new Action(delegate {
                        _progressBar.Visibility = Visibility.Collapsed;
                        _statusText.Text = "Error during installation: " + ex.Message;
                        _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                        _btnInstall.IsEnabled = true;
                        _btnCancel.IsEnabled = true;
                        System.Windows.Forms.MessageBox.Show("Install Error: " + ex.Message, "Setup Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }));
                }
            });
        }
    }
}
