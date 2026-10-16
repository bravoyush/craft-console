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

namespace CraftConsole.Uninstall
{
    public class Program
    {
        public const string VERSION = "1.0.2";
        public const string ARTCRAFT_MARK_PATH = "M 104.28,49.49 L 81.55,0 H 50.32 L 47.15,4.76 L 14.75,53.63 L 0,75.85 L 21.55,97.4 L 85.34,60.46 L 102.33,97.5 L 116.34,75.76 L 104.28,49.49 Z M 32.89,65.66 L 65.31,16.79 L 76.22,40.56 L 32.89,65.66 Z";

        [STAThread]
        public static void Main(string[] args)
        {
            bool quiet = false;
            foreach (string arg in args)
            {
                if (arg.Equals("/quiet", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--quiet", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/silent", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-s", StringComparison.OrdinalIgnoreCase))
                {
                    quiet = true;
                }
            }

            if (quiet)
            {
                PerformUninstall(false);
                return;
            }

            var app = new Application();
            app.Run(new UninstallWindow());
        }

        public static void PerformUninstall(bool deleteUserData)
        {
            string installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');

            // 1. Terminate any running CraftConsole process
            try
            {
                foreach (var p in Process.GetProcessesByName("CraftConsole"))
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }
            }
            catch { }

            // 2. Remove Shortcuts
            try
            {
                string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnkDesk1 = System.IO.Path.Combine(desk, "Craft Console.lnk");
                string lnkDesk2 = System.IO.Path.Combine(desk, "EffectCraft (with Console).lnk");
                if (File.Exists(lnkDesk1)) try { File.Delete(lnkDesk1); } catch { }
                if (File.Exists(lnkDesk2)) try { File.Delete(lnkDesk2); } catch { }

                string progs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string lnkStart1 = System.IO.Path.Combine(progs, "Craft Console.lnk");
                string lnkStart2 = System.IO.Path.Combine(progs, "Uninstall Craft Console.lnk");
                if (File.Exists(lnkStart1)) try { File.Delete(lnkStart1); } catch { }
                if (File.Exists(lnkStart2)) try { File.Delete(lnkStart2); } catch { }

                string startDir = System.IO.Path.Combine(progs, "Craft Console");
                if (Directory.Exists(startDir))
                {
                    try { Directory.Delete(startDir, true); } catch { }
                }
            }
            catch { }

            // 3. Remove Registry Entries
            RemoveRegistryKeys();

            // 4. Optionally remove AppData
            if (deleteUserData)
            {
                try
                {
                    string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EffectCraft");
                    string ini = System.IO.Path.Combine(appData, "splash.ini");
                    if (File.Exists(ini)) try { File.Delete(ini); } catch { }

                    string splash = System.IO.Path.Combine(appData, "splash");
                    if (Directory.Exists(splash)) try { Directory.Delete(splash, true); } catch { }
                }
                catch { }
            }

            // 5. Self-delete install directory via cmd
            try
            {
                if (!string.IsNullOrEmpty(installDir) && installDir.Length > 4)
                {
                    string root = System.IO.Path.GetPathRoot(installDir);
                    if (!string.Equals(root.TrimEnd('\\', '/'), installDir, StringComparison.OrdinalIgnoreCase))
                    {
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = "/c ping 127.0.0.1 -n 2 >nul & del /f /q \"" + installDir + "\\*.*\" & rd /s /q \"" + installDir + "\"",
                            WindowStyle = ProcessWindowStyle.Hidden,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(psi);
                    }
                }
            }
            catch { }
        }

        public static void RemoveRegistryKeys()
        {
            string[] subKeys = new string[] {
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CraftConsole",
                @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\CraftConsole"
            };

            foreach (string sk in subKeys)
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(sk, false); } catch { }
                try { Registry.LocalMachine.DeleteSubKeyTree(sk, false); } catch { }
            }
        }
    }

    public class UninstallWindow : Window
    {
        private CheckBox _chkDeleteUserData;
        private Button _btnUninstall;
        private Button _btnCancel;
        private TextBlock _statusText;
        private ProgressBar _progressBar;

        public UninstallWindow()
        {
            Title = "Uninstall Craft Console";
            Width = 500;
            Height = 350;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(20, 24, 32));
            Foreground = Brushes.White;
            FontFamily = new FontFamily("Segoe UI, -apple-system, sans-serif");
            ResizeMode = ResizeMode.NoResize;

            var rootGrid = new Grid { Margin = new Thickness(24) };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. Header
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconBox = new Viewbox { Width = 38, Height = 38, VerticalAlignment = VerticalAlignment.Center };
            var iconPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(Program.ARTCRAFT_MARK_PATH),
                Fill = new SolidColorBrush(Color.FromRgb(255, 90, 90))
            };
            iconBox.Child = iconPath;
            Grid.SetColumn(iconBox, 0);
            headerGrid.Children.Add(iconBox);

            var headStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            headStack.Children.Add(new TextBlock
            {
                Text = "Uninstall Craft Console",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            headStack.Children.Add(new TextBlock
            {
                Text = "EffectCraft & After Effects HUD Companion v" + Program.VERSION,
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 175)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(headStack, 1);
            headerGrid.Children.Add(headStack);

            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // 2. Body
            var bodyStack = new StackPanel();

            string installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            var infoCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 34, 46)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 55, 75)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var iStack = new StackPanel();
            iStack.Children.Add(new TextBlock
            {
                Text = "Are you sure you want to completely remove Craft Console and its shortcuts from this computer?",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 225, 235)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            iStack.Children.Add(new TextBlock
            {
                Text = "Install Location: " + installDir,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 150, 170)),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            infoCard.Child = iStack;
            bodyStack.Children.Add(infoCard);

            _chkDeleteUserData = new CheckBox
            {
                Content = "Also delete my custom splash media, favorites, and settings in %APPDATA%\\EffectCraft",
                IsChecked = false,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 210, 225)),
                FontSize = 11.5,
                Margin = new Thickness(2, 0, 0, 12)
            };
            bodyStack.Children.Add(_chkDeleteUserData);

            _statusText = new TextBlock
            {
                Text = "Ready to proceed with uninstallation.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 150, 165)),
                Margin = new Thickness(2, 0, 0, 6)
            };
            bodyStack.Children.Add(_statusText);

            _progressBar = new ProgressBar
            {
                Height = 6,
                Margin = new Thickness(0, 0, 0, 4),
                Visibility = Visibility.Collapsed
            };
            bodyStack.Children.Add(_progressBar);

            Grid.SetRow(bodyStack, 1);
            rootGrid.Children.Add(bodyStack);

            // 3. Footer
            var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal };
            _btnCancel = new Button
            {
                Content = "Cancel",
                Width = 90, Height = 32,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(38, 46, 62)),
                Foreground = Brushes.White,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _btnCancel.Click += delegate { Close(); };
            btnStack.Children.Add(_btnCancel);

            _btnUninstall = new Button
            {
                Content = "Uninstall",
                Width = 110, Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(215, 60, 60)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _btnUninstall.Click += delegate { ExecuteUninstall(); };
            btnStack.Children.Add(_btnUninstall);

            Grid.SetColumn(btnStack, 1);
            footer.Children.Add(btnStack);

            Grid.SetRow(footer, 2);
            rootGrid.Children.Add(footer);

            Content = rootGrid;
        }

        private void ExecuteUninstall()
        {
            _btnUninstall.IsEnabled = false;
            _btnCancel.IsEnabled = false;
            _progressBar.Visibility = Visibility.Visible;
            _progressBar.IsIndeterminate = true;
            _statusText.Text = "Removing Craft Console components and shortcuts...";
            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 180, 0));

            bool delUserData = _chkDeleteUserData.IsChecked == true;

            ThreadPool.QueueUserWorkItem(delegate {
                Program.PerformUninstall(delUserData);

                Thread.Sleep(500);

                Dispatcher.Invoke(new Action(delegate {
                    _progressBar.IsIndeterminate = false;
                    _progressBar.Value = 100;
                    _statusText.Text = "Craft Console was successfully removed from your computer.";
                    _statusText.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 150));

                    System.Windows.Forms.MessageBox.Show(
                        "Craft Console has been successfully uninstalled from your computer.",
                        "Uninstall Complete",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);

                    Close();
                }));
            });
        }
    }
}
